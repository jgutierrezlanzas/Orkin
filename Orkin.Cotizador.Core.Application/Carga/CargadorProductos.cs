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
        var origenPrecioId = await _contexto.OrigenPrecioCatalogo
            .AsNoTracking()
            .Where(o => o.Codigo == (configuracion.ErpConexionId != null ? "ERP" : "EXCEL"))
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
                if (existentes.TryGetValue(codigo, out var existente))
                {
                    var hashExistente = existente.HashContenido is { Length: > 0 }
                        ? Convert.ToHexString(existente.HashContenido)
                        : null;

                    if (hashExistente == hash)
                    {
                        omitidos++;
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
                    // Se escribe siempre, sin mirar el hash: adopted la fila ya cambia (le
                    // cambian la conexión y el origen del precio), así que omitirla por tener la
                    // misma huella dejaría la conexión sin reclamar y la próxima corrida
                    // intentaría adoptarla otra vez, para siempre.
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

                    existentes[codigo] = huerfana;
                    actualizados++;
                }
                else
                {
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

                    _contexto.Producto.Add(nuevo);
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

        await _contexto.SaveChangesAsync(cancellationToken);

        return new ResultadoCarga
        {
            Catalogo = Catalogo,
            TipoOrigen = configuracion.TipoOrigen,
            Insertados = insertados,
            Actualizados = actualizados,
            Omitidos = omitidos,
            Rechazados = rechazados.Count,
            Rechazos = rechazados
        };
    }
}
