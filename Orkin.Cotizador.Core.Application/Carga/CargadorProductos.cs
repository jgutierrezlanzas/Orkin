using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Carga el catálogo de productos desde un origen externo (Excel, SQL, lo que sea).
/// Hace upsert idempotente por <c>CodigoExterno</c>: inserta lo nuevo, actualiza lo que
/// cambió, omite lo que tiene el mismo hash.
/// </summary>
public sealed class CargadorProductos : ICargadorCatalogo
{
    private readonly CotizadorDbContext _contexto;

    public CargadorProductos(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public string Catalogo => "PRODUCTOS";

    // Cachés de nombres para el diff de la simulación. Viven en el cargador y no en el bucle
    // porque su vida útil es la de la carga: al terminar, se van con el servicio, que es
    // scoped, y no se filtran entre cargas de conexiones distintas.
    private readonly ConcurrentDictionary<Guid, string> _nombresCategoria = new();
    private readonly ConcurrentDictionary<Guid, string> _nombresUnidad = new();

    public async Task<ResultadoCarga> CargarAsync(
        IReadOnlyList<Dictionary<string, object?>> filas,
        ConfiguracionCarga configuracion,
        CancellationToken cancellationToken = default)
    {
        var insertados = 0;
        var actualizados = 0;
        var omitidos = 0;
        var rechazados = new List<RechazoCarga>();

        // Índice de unidades de medida por código, para resolver la FK sin consultar por cada fila.
        var unidades = await _contexto.UnidadMedida
            .AsNoTracking()
            .Where(u => u.IsActive)
            .ToDictionaryAsync(u => u.Codigo, u => u.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var categorias = await _contexto.CategoriaProducto
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToDictionaryAsync(c => c.Codigo, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // El origen del precio se resuelve por código y no por id fijo: el id depende del orden en
        // que la semilla insertó las filas. Del ERP viene "ERP"; de un archivo manual, "EXCEL".
        var codigoOrigenPrecio = configuracion.ErpConexionId != null ? "ERP" : "EXCEL";

        var origenPrecioId = await _contexto.OrigenPrecioCatalogo
            .AsNoTracking()
            .Where(o => o.Codigo == codigoOrigenPrecio)
            .Select(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (origenPrecioId == 0)
        {
            throw new InvalidOperationException(
                "No está sembrado el catálogo OrigenPrecio (falta EXCEL/ERP). Corra la semilla.");
        }

        // Filas existentes por CodigoExterno, para decidir insert vs update vs omit.
        var codigosExternos = new List<string>();
        foreach (var fila in filas)
        {
            var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
            var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"));
            if (codigo is not null) codigosExternos.Add(codigo);
        }

        // Dos búsquedas, y la diferencia entre ellas es el corazón de la idempotencia:
        //
        // 1. `existentes` — lo que YA es de esta conexión. Son las filas que trajo una corrida
        //    anterior. Se reconocen por (conexión, código externo) y, según el hash, se
        //    actualizan o se omiten. Acotar por conexión es lo que impide que dos ERPs distintos
        //    con el mismo "PQ-0001" se pisen entre sí: sin ese filtro, la segunda conexión
        //    encontraría la fila de la primera y la sobrescribiría con datos de otro cliente,
        //    dejando la fila diciendo que vino de un ERP del que no vino.
        //
        // 2. `huerfanas` — lo que entró a mano o por Excel antes de que existiera esta conexión
        //    y que nadie más ha reclamado. Son las que se adoptan en la primera corrida del ERP.
        var existentes = await _contexto.Producto
            .Where(p => p.ErpConexionId == configuracion.ErpConexionId
                     && p.CodigoExterno != null
                     && codigosExternos.Contains(p.CodigoExterno))
            .ToDictionaryAsync(p => p.CodigoExterno!, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // La adopción es lo que hace que el ERP pueda llegar después que el Excel sin romper. El
        // Excel original y el ERP del cliente traen el MISMO artículo con el mismo código, y
        // `Codigo` es único en `dbo.Producto`: sin este paso, la corrida intentaría insertar
        // los productos nuevos con códigos que ya existen y la base los rechazaría a todos con
        // UX_Producto_Codigo, que es el síntoma clásico de "el Excel y el ERP traen lo mismo".
        //
        // Solo se adoptan filas sin conexión (`ErpConexionId == NULL`). Una fila que ya tiene
        // conexión no se toca: si el mismo código aparece en dos ERPs, son dos productos
        // distintos y el segundo tiene que ser una fila nueva.
        var huerfanas = await _contexto.Producto
            .Where(p => p.ErpConexionId == null && codigosExternos.Contains(p.Codigo))
            .ToDictionaryAsync(p => p.Codigo, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var numeroFila = 1;

        // Lo que la carga haría, fila por fila. Solo se llena en simulación.
        var simulados = new List<CambioSimulado>();

        // En simulación las entidades no se tocan, así que tampoco se puede usar el hash de la
        // entidad como memoria de lo que ya se "decidió" por esa fila. Sin esto, un código que
        // aparece dos veces en el origen daría dos actualizaciones en la simulación y en la
        // carga real daría una actualización y una omisión: la simulación estaría prometiendo un
        // número distinto del que la carga realmente produce, que es justo el daño que una
        // simulación no puede permisear. Este diccionario lleva el mismo registro, aparte del
        // change tracker.
        var hashesSimulados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in filas)
        {
            try
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);

                // ── Validar campos obligatorios ─────────────────────────────────────────
                var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"))
                    ?? throw new FormatException("El campo 'Codigo' es obligatorio.");

                var nombre = Normalizacion.AString(mapeada.GetValueOrDefault("Nombre"))
                    ?? throw new FormatException("El campo 'Nombre' es obligatorio.");

                var costo = Normalizacion.ADecimal(mapeada.GetValueOrDefault("CostoActual"), "CostoActual");

                var unidadCodigo = Normalizacion.AString(mapeada.GetValueOrDefault("UnidadMedidaCodigo"))
                    ?? throw new FormatException("El campo 'UnidadMedidaCodigo' es obligatorio.");

                if (!unidades.TryGetValue(unidadCodigo, out var unidadId))
                {
                    throw new FormatException($"La unidad de medida '{unidadCodigo}' no existe. Cárguela primero.");
                }

                var categoriaCodigo = Normalizacion.AString(mapeada.GetValueOrDefault("CategoriaCodigo"));
                Guid? categoriaId = null;
                if (categoriaCodigo is not null)
                {
                    if (!categorias.TryGetValue(categoriaCodigo, out var catId))
                    {
                        throw new FormatException($"La categoría '{categoriaCodigo}' no existe. Cárguela primero.");
                    }
                    categoriaId = catId;
                }

                var precioVenta = Normalizacion.ADecimalOpcional(mapeada.GetValueOrDefault("PrecioVentaActual")) ?? 0m;
                var descripcion = Normalizacion.AString(mapeada.GetValueOrDefault("Descripcion"));
                var vidaUtil = Normalizacion.AEnteroOpcional(mapeada.GetValueOrDefault("VidaUtilMeses"));

                // ── Hash para detectar cambios ────────────────────────────────────────
                var hash = Normalizacion.HashContenido(mapeada);

                // ── Decidir: insert / update / omit ────────────────────────────────────
                //
                // La decisión se toma SIEMPRE igual, y lo único que se bifurca es si se aplica.
                // Esa es la razón de que la simulación sirva: no es una segunda implementación
                // parecida a la carga, es la misma con el último paso cortado. Si se escribiera
                // un "modo simulación" aparte, con el tiempo divergiría del real, y una
                // simulación que no predice la carga real es peor que no tener simulación.
                if (existentes.TryGetValue(codigo, out var existente))
                {
                    // Prioridad al hash simulado: si esta fila ya se procesó en esta misma
                    // corrida, el valor que vale es el que *quedaría*, no el que tiene hoy.
                    var hashExistente = hashesSimulados.TryGetValue(codigo, out var yaSimulado)
                        ? yaSimulado
                        : existente?.HashContenido is { Length: > 0 }
                            ? Convert.ToHexString(existente.HashContenido)
                            : null;

                    if (hashExistente == hash)
                    {
                        omitidos++;

                        if (configuracion.EsSimulacion)
                        {
                            simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Omitir, []));
                        }
                    }
                    else
                    {
                        // Los campos que de verdad cambian, para el diff de la simulación. Se
                        // calculan antes de escribir, no después, porque comparar después de
                        // asignar siempre da "no cambió".
                        var cambios = new List<CampoSimulado>();

                        if (existente.Nombre != nombre)
                            cambios.Add(new CampoSimulado("Nombre", existente.Nombre, nombre));
                        if (existente.Descripcion != descripcion)
                            cambios.Add(new CampoSimulado("Descripción", existente.Descripcion, descripcion));
                        if (existente.CategoriaId != categoriaId)
                            cambios.Add(new CampoSimulado("Categoría", await NombreCategoriaAsync(categoriaId, cancellationToken), await NombreCategoriaAsync(existente.CategoriaId, cancellationToken)));
                        if (existente.UnidadMedidaId != unidadId)
                            cambios.Add(new CampoSimulado("Unidad de medida", await NombreUnidadAsync(unidadId, cancellationToken), await NombreUnidadAsync(existente.UnidadMedidaId, cancellationToken)));
                        if (existente.CostoActual != costo)
                            cambios.Add(new CampoSimulado("Costo", Format(existente.CostoActual), Format(costo)));
                        if (existente.PrecioVentaActual != precioVenta)
                            cambios.Add(new CampoSimulado("Precio venta", Format(existente.PrecioVentaActual), Format(precioVenta)));
                        if (existente.VidaUtilMeses != vidaUtil)
                            cambios.Add(new CampoSimulado("Vida útil (meses)", Format(existente.VidaUtilMeses), Format(vidaUtil)));

                        if (configuracion.EsSimulacion)
                        {
                            simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Actualizar, cambios));
                            hashesSimulados[codigo] = hash;
                        }
                        else
                        {
                            existente.Nombre = nombre;
                            existente.Descripcion = descripcion;
                            existente.CategoriaId = categoriaId;
                            existente.UnidadMedidaId = unidadId;
                            existente.CostoActual = costo;
                            existente.PrecioVentaActual = precioVenta;
                            existente.VidaUtilMeses = vidaUtil;
                            existente.HashContenido = Convert.FromHexString(hash);
                            existente.UltimaSyncUtc = DateTime.UtcNow;
                            existente.UpdatedAt = DateTime.UtcNow;
                        }

                        actualizados++;
                    }
                }
                else if (huerfanas.Remove(codigo, out var huerfana))
                {
                    // ── Adopción ────────────────────────────────────────────────────────
                    // La fila ya existía (vía Excel o captura manual) y el ERP trae el mismo
                    // artículo. Se reescribe con los datos del ERP y se le pone de qué conexión
                    // vino: a partir de aquí el ERP es la fuente de verdad de esta fila.
                    //
                    // Se cuenta como actualizado y no como insertado porque la fila ya estaba
                    // en la base: el contador tiene que responder "¿cuántas filas nuevas hay en
                    // el catálogo?", que es la pregunta que se hace uno al mirar el resultado.
                    //
                    // Se escribe siempre, sin mirar el hash: adoptar la fila ya cambia (le
                    // cambian la conexión y el origen del precio), así que omitirla por tener la
                    // misma huella dejaría la conexión sin reclamar y la próxima corrida
                    // intentaría adoptarla otra vez, para siempre.
                    if (configuracion.EsSimulacion)
                    {
                        var cambiosAdopcion = new List<CampoSimulado>
                        {
                            new("Origen del precio", "El que tuviera", codigoOrigenPrecio),
                            new("Procedencia", "Ninguna (ingresada a mano o por Excel)",
                                $"La conexión «{configuracion.ErpConexionId}»")
                        };

                        if (huerfana.Nombre != nombre)
                            cambiosAdopcion.Add(new CampoSimulado("Nombre", huerfana.Nombre, nombre));
                        if (huerfana.Descripcion != descripcion)
                            cambiosAdopcion.Add(new CampoSimulado("Descripción", huerfana.Descripcion, descripcion));
                        if (huerfana.CategoriaId != categoriaId)
                            cambiosAdopcion.Add(new CampoSimulado("Categoría", await NombreCategoriaAsync(categoriaId, cancellationToken), await NombreCategoriaAsync(huerfana.CategoriaId, cancellationToken)));
                        if (huerfana.UnidadMedidaId != unidadId)
                            cambiosAdopcion.Add(new CampoSimulado("Unidad de medida", await NombreUnidadAsync(unidadId, cancellationToken), await NombreUnidadAsync(huerfana.UnidadMedidaId, cancellationToken)));
                        if (huerfana.CostoActual != costo)
                            cambiosAdopcion.Add(new CampoSimulado("Costo", Format(huerfana.CostoActual), Format(costo)));
                        if (huerfana.PrecioVentaActual != precioVenta)
                            cambiosAdopcion.Add(new CampoSimulado("Precio venta", Format(huerfana.PrecioVentaActual), Format(precioVenta)));
                        if (huerfana.VidaUtilMeses != vidaUtil)
                            cambiosAdopcion.Add(new CampoSimulado("Vida útil (meses)", Format(huerfana.VidaUtilMeses), Format(vidaUtil)));

                        simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Adoptar, cambiosAdopcion));
                        hashesSimulados[codigo] = hash;
                    }
                    else
                    {
                        huerfana.CodigoExterno = codigo;
                        huerfana.ErpConexionId = configuracion.ErpConexionId;
                        huerfana.OrigenPrecioId = origenPrecioId;
                        huerfana.Nombre = nombre;
                        huerfana.Descripcion = descripcion;
                        huerfana.CategoriaId = categoriaId;
                        huerfana.UnidadMedidaId = unidadId;
                        huerfana.CostoActual = costo;
                        huerfana.PrecioVentaActual = precioVenta;
                        huerfana.VidaUtilMeses = vidaUtil;
                        huerfana.HashContenido = Convert.FromHexString(hash);
                        huerfana.UltimaSyncUtc = DateTime.UtcNow;
                        huerfana.UpdatedAt = DateTime.UtcNow;
                    }

                    existentes[codigo] = huerfana;
                    actualizados++;
                }
                else
                {
                    // La entidad se construye igual en los dos modos. La diferencia es si se
                    // adjunta al contexto. Construirla también en simulación no es un descuido
                    // hacia la base: es lo que hace que un código repetido en el origen se
                    // comporte igual que en la carga real —la segunda vez encuentra la fila que
                    // la primera acaba de "crear" y la trata como existente, en vez de contarse
                    // como dos altas—. Es la misma memoria que usa la carga real, sin que nada
                    // llegue al change tracker.
                    var nuevo = new Producto
                    {
                        Id = Guid.NewGuid(),
                        Codigo = codigo,
                        Nombre = nombre,
                        Descripcion = descripcion,
                        CategoriaId = categoriaId,
                        UnidadMedidaId = unidadId,
                        CostoActual = costo,
                        PrecioVentaActual = precioVenta,
                        VidaUtilMeses = vidaUtil,
                        OrigenPrecioId = origenPrecioId,
                        ErpConexionId = configuracion.ErpConexionId,
                        CodigoExterno = codigo,
                        HashContenido = Convert.FromHexString(hash),
                        UltimaSyncUtc = DateTime.UtcNow,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    if (configuracion.EsSimulacion)
                    {
                        simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Insertar, []));
                    }
                    else
                    {
                        _contexto.Producto.Add(nuevo);
                    }

                    existentes[codigo] = nuevo;
                    insertados++;
                }
            }
            catch (Exception ex)
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
                var codigoExterno = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"));
                rechazados.Add(new RechazoCarga(
                    numeroFila,
                    codigoExterno,
                    ex.Message,
                    null));
            }

            numeroFila++;
        }

        // En simulación no se guarda, y además se comprueba que no haya nada pendiente de
        // guardar. La comprobación no es desconfianza del cargador de hoy, que sí respeta la
        // bandera: es una red para el cargador que se escriba dentro de seis meses y se olvide
        // de ella. Sin esta comprobación, ese cargador publicaría una simulación que no fue tal
        // cosa y dejaría medio catálogo cargado sin que nadie lo pidiera — que es exactamente
        // el daño que la simulación existe para evitar.
        if (configuracion.EsSimulacion)
        {
            var pendientes = _contexto.ChangeTracker.Entries().Count(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached);

            if (pendientes > 0)
            {
                throw new InvalidOperationException(
                    $"El cargador de {Catalogo} dejó {pendientes} cambios sin guardar en una simulación. " +
                    "Un cargador en modo simulación no debe modificar entidades: debe reportar el cambio, no aplicarlo.");
            }
        }
        else
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }

        return new ResultadoCarga
        {
            Catalogo = Catalogo,
            TipoOrigen = configuracion.TipoOrigen,
            Insertados = insertados,
            Actualizados = actualizados,
            Omitidos = omitidos,
            Rechazados = rechazados.Count,
            Rechazos = rechazados,
            Cambios = simulados
        };
    }

    // ── Ayudas para el diff de la simulación ───────────────────────────────────

    /// <summary>
    /// Nombre de una categoría, para que el diff sea legible. "La categoría cambió de General a
    /// Materiales" se entiende; "la categoría cambió de 3f2a…-9b1c a 7d4e…-0a2f" no, y obliga a
    /// abrir la base para entender qué pasó.
    /// <para>Se cachea porque el diff se arma dentro de un bucle sobre el mismo catálogo, y el
    /// caso normal —la mayoría de las categorías repetidas en las 111 filas— convertiría esto en
    /// una consulta a la base por cada fila para devolver siempre el mismo texto. La caché
    /// dura lo que la carga: el cargador es scoped y no la comparte entre cargas.</para>
    /// </summary>
    private async Task<string?> NombreCategoriaAsync(Guid? id, CancellationToken ca)
    {
        if (id is null) return null;

        if (_nombresCategoria.TryGetValue(id.Value, out var ya))
        {
            return ya;
        }

        var nombre = await _contexto.CategoriaProducto
            .AsNoTracking()
            .Where(c => c.Id == id.Value)
            .Select(c => c.Nombre)
            .FirstOrDefaultAsync(ca);

        // Se cachea también el null: si el id no resuelve a ninguna categoría, preguntar de nuevo
        // por cada fila es la misma consulta devolviendo lo mismo.
        var resultado = nombre ?? "(no encontrada)";
        _nombresCategoria[id.Value] = resultado;

        return resultado;
    }

    private async Task<string?> NombreUnidadAsync(Guid? id, CancellationToken ca)
    {
        if (id is null) return null;

        if (_nombresUnidad.TryGetValue(id.Value, out var ya))
        {
            return ya;
        }

        var nombre = await _contexto.UnidadMedida
            .AsNoTracking()
            .Where(u => u.Id == id.Value)
            .Select(u => u.Nombre)
            .FirstOrDefaultAsync(ca);

        var resultado = nombre ?? "(no encontrada)";
        _nombresUnidad[id.Value] = resultado;

        return resultado;
    }

    /// <summary>
    /// Formatea un valor para el diff. Los decimales salen con los dos decimales que usa el
    /// resto de la aplicación, para que "43.16" y "43,16" no parezcan dos números distintos
    /// según de dónde se mire.
    /// </summary>
    private static string? Format(decimal? valor) => valor?.ToString("N2");

    private static string? Format(int? valor) => valor?.ToString();
}
