using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// El núcleo común de los catálogos maestros: entidades que heredan <see cref="CatalogoBase"/>
/// y se identifican por <c>Codigo</c> (unidades de medida, categorías, tipos de servicio).
/// <para>La mecánica es la de todos los cargadores —upsert idempotente por clave de negocio,
/// comparación por hash, adopción de lo capturado a mano, simulación con el último paso
/// cortado— con la clave siendo <c>CodigoExterno = Codigo</c>. Lo que cambia entre hermanos
/// son los campos propios, y eso va en los hooks abstractos: un cargador nuevo de esta familia
/// es declarar el catálogo y mapear sus columnas, no recopiar cuatrocientas líneas que al
/// copiarse se desincronizan del original.</para>
/// <para><b>El orden de presentación</b> (<c>Orden</c>) no lo trae el ERP: si la vista no lo
/// dice, se usa la posición en el archivo. La vista del cliente puede ordenar sus filas como
/// quiera que el catálogo se lista en ese mismo orden.</para>
/// </summary>
public abstract class CargadorCatalogoMaestro<T> : ICargadorCatalogo
    where T : CatalogoBase
{
    protected readonly CotizadorDbContext Contexto;

    protected CargadorCatalogoMaestro(CotizadorDbContext contexto)
    {
        Contexto = contexto;
    }

    public abstract string Catalogo { get; }

    public async Task<ResultadoCarga> CargarAsync(
        IReadOnlyList<Dictionary<string, object?>> filas,
        ConfiguracionCarga configuracion,
        CancellationToken cancellationToken = default)
    {
        var insertados = 0;
        var actualizados = 0;
        var omitidos = 0;
        var rechazados = new List<RechazoCarga>();

        var origenDatoId = await Contexto.OrigenDatoCatalogo
            .AsNoTracking()
            .Where(o => o.Codigo == "ERP")
            .Select(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (origenDatoId == 0)
        {
            throw new InvalidOperationException(
                "No está sembrado el catálogo OrigenDato (falta ERP). Corra la semilla.");
        }

        var codigos = new List<string>();
        foreach (var fila in filas)
        {
            var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
            var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"));
            if (codigo is not null) codigos.Add(codigo);
        }

        // Las filas de esta conexión, por CodigoExterno (= Codigo). Acotado por conexión: dos
        // ERPs distintos con el mismo "UND" son dos filas y no se pisan.
        var existentes = await Set()
            .Where(e => e.ErpConexionId == configuracion.ErpConexionId
                     && e.CodigoExterno != null
                     && codigos.Contains(e.CodigoExterno))
            .ToDictionaryAsync(e => e.CodigoExterno!, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Huérfanas: lo capturado a mano o sembrado del Excel con el mismo código. Sin
        // adoptarlas, el Codigo único rechazaría el insert completo.
        var huerfanas = await Set()
            .Where(e => e.ErpConexionId == null && codigos.Contains(e.Codigo))
            .ToDictionaryAsync(e => e.Codigo, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var numeroFila = 1;
        var simulados = new List<CambioSimulado>();
        var hashesSimulados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in filas)
        {
            try
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);

                var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"))
                    ?? throw new FormatException("El campo 'Codigo' es obligatorio.");

                var nombre = Normalizacion.AString(mapeada.GetValueOrDefault("Nombre"))
                    ?? throw new FormatException("El campo 'Nombre' es obligatorio.");

                var orden = Normalizacion.AEnteroOpcional(mapeada.GetValueOrDefault("Orden")) ?? numeroFila;

                var hash = Normalizacion.HashContenido(mapeada);

                if (existentes.TryGetValue(codigo, out var existente))
                {
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
                        var cambios = new List<CampoSimulado>();

                        if (existente.Nombre != nombre)
                        {
                            cambios.Add(new CampoSimulado("Nombre", existente.Nombre, nombre));
                        }
                        if (existente.Orden != orden)
                        {
                            cambios.Add(new CampoSimulado("Orden", existente.Orden.ToString(), orden.ToString()));
                        }
                        cambios.AddRange(await DiferenciaExtrasAsync(existente, mapeada, cancellationToken));

                        if (configuracion.EsSimulacion)
                        {
                            simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Actualizar, cambios));
                            hashesSimulados[codigo] = hash;
                        }
                        else
                        {
                            existente.Nombre = nombre;
                            existente.Orden = orden;
                            await AplicarExtrasAsync(existente, mapeada, cancellationToken);
                            existente.HashContenido = Convert.FromHexString(hash);
                            existente.UltimaSyncUtc = DateTime.UtcNow;
                            existente.UpdatedAt = DateTime.UtcNow;
                        }

                        actualizados++;
                    }
                }
                else if (huerfanas.Remove(codigo, out var huerfana))
                {
                    // La fila ya estaba (manual o Excel) y el ERP trae el mismo código: el ERP pasa
                    // a ser la fuente de verdad. Se escribe siempre sin mirar el hash —adoptar ya
                    // es un cambio— y se cuenta como actualizado porque la fila no es nueva.
                    var cambiosAdopcion = new List<CampoSimulado>
                    {
                        new("Procedencia", "Ninguna (capturada a mano)",
                            $"La conexión «{configuracion.ErpConexionId}»")
                    };
                    if (huerfana.Nombre != nombre)
                    {
                        cambiosAdopcion.Add(new CampoSimulado("Nombre", huerfana.Nombre, nombre));
                    }
                    cambiosAdopcion.AddRange(await DiferenciaExtrasAsync(huerfana, mapeada, cancellationToken));

                    if (configuracion.EsSimulacion)
                    {
                        simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Adoptar, cambiosAdopcion));
                        hashesSimulados[codigo] = hash;
                    }
                    else
                    {
                        huerfana.CodigoExterno = codigo;
                        huerfana.ErpConexionId = configuracion.ErpConexionId;
                        huerfana.OrigenDatoId = origenDatoId;
                        huerfana.Nombre = nombre;
                        huerfana.Orden = orden;
                        huerfana.EsSembrado = false;
                        await AplicarExtrasAsync(huerfana, mapeada, cancellationToken);
                        huerfana.HashContenido = Convert.FromHexString(hash);
                        huerfana.UltimaSyncUtc = DateTime.UtcNow;
                        huerfana.UpdatedAt = DateTime.UtcNow;
                    }

                    existentes[codigo] = huerfana;
                    actualizados++;
                }
                else
                {
                    var nuevo = CrearEntidad();
                    nuevo.Codigo = codigo;
                    nuevo.Nombre = nombre;
                    nuevo.Orden = orden;
                    nuevo.OrigenDatoId = origenDatoId;
                    nuevo.ErpConexionId = configuracion.ErpConexionId;
                    nuevo.CodigoExterno = codigo;
                    nuevo.HashContenido = Convert.FromHexString(hash);
                    nuevo.UltimaSyncUtc = DateTime.UtcNow;
                    nuevo.IsActive = true;
                    // No es del seed: es del ERP, y la siembra no debe tocarla en su siguiente
                    // corrida (EsSembrado es la marca de idempotencia de §5.20).
                    nuevo.EsSembrado = false;
                    nuevo.CreatedAt = DateTime.UtcNow;
                    nuevo.UpdatedAt = DateTime.UtcNow;
                    await AplicarExtrasAsync(nuevo, mapeada, cancellationToken);

                    if (configuracion.EsSimulacion)
                    {
                        simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Insertar, []));
                    }
                    else
                    {
                        Set().Add(nuevo);
                    }

                    existentes[codigo] = nuevo;
                    insertados++;
                }
            }
            catch (Exception ex)
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
                rechazados.Add(new RechazoCarga(
                    numeroFila,
                    Normalizacion.AString(mapeada.GetValueOrDefault("Codigo")),
                    ex.Message,
                    null));
            }

            numeroFila++;
        }

        if (configuracion.EsSimulacion)
        {
            var pendientes = Contexto.ChangeTracker.Entries().Count(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached);
            if (pendientes > 0)
            {
                throw new InvalidOperationException(
                    $"El cargador de {Catalogo} dejó {pendientes} cambios sin guardar en una simulación. " +
                    "Un cargador en modo simulación no debe modificar entidades: debe reportar el cambio, no aplicarlo.");
            }
        }
        else
        {
            await Contexto.SaveChangesAsync(cancellationToken);
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

    /// <summary>El DbSet del catálogo concreto.</summary>
    protected abstract DbSet<T> Set();

    /// <summary>Una entidad nueva, sin campos comunes ni de origen: el núcleo los llena.</summary>
    protected abstract T CrearEntidad();

    /// <summary>Los campos propios del catálogo. Se llama antes de guardar una entidad nueva.</summary>
    protected abstract Task AplicarExtrasAsync(T entidad, Dictionary<string, object?> mapeada, CancellationToken ct);

    /// <summary>
    /// El diff de los campos propios, para la simulación. Se calcula antes de escribir, no
    /// después, porque comparar después de asignar siempre da "no cambió".
    /// </summary>
    protected abstract Task<List<CampoSimulado>> DiferenciaExtrasAsync(T existente, Dictionary<string, object?> mapeada, CancellationToken ct);
}
