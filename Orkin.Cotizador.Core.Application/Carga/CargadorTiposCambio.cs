using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Carga el catálogo de tipos de cambio desde un origen externo, con la misma mecánica de
/// <see cref="CargadorProductos"/>: upsert idempotente por <c>CodigoExterno</c> (la fecha en ISO),
/// actualización por hash, adopción de la fila capturada a mano con la misma (fecha, fuente).
/// <para>El tipo de cambio es el catálogo con más filas de todos (1 309 días en el ERP demo) y el
/// que más duele desactualizado: una cotización de mañana con el TC de la semana pasada es un
/// precio distinto del que va a cobrar el cliente. La unicidad es (Fecha, Fuente) —
/// <c>UX_TipoCambio_Fecha_Fuente</c> —, así que la adopción va por ese par: sin ella, la primera
/// corrida del ERP chocaría contra cualquier fila manual del mismo día y misma fuente.</para>
/// </summary>
public sealed class CargadorTiposCambio : ICargadorCatalogo
{
    private readonly CotizadorDbContext _contexto;

    public CargadorTiposCambio(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public string Catalogo => "TIPOS_CAMBIO";

    public IReadOnlyList<CampoEsperado> CamposEsperados =>
    [
        new("Fecha", "fecha", true, "La fecha del tipo de cambio en formato YYYY-MM-DD."),
        new("Valor", "número", true, "Córdobas por 1 dólar. Debe ser mayor que cero."),
    ];

    public async Task<ResultadoCarga> CargarAsync(
        IReadOnlyList<Dictionary<string, object?>> filas,
        ConfiguracionCarga configuracion,
        CancellationToken cancellationToken = default)
    {
        var insertados = 0;
        var actualizados = 0;
        var omitidos = 0;
        var rechazados = new List<RechazoCarga>();

        // La fuente se resuelve por código: el ERP del cliente guarda el histórico oficial del
        // Banco Central, y si su vista distingue otra fuente, se respeta. Si no trae nada, el
        // default es BANCO_CENTRAL y no "rechazar todo": una vista de dos columnas (fecha, valor)
        // es lo más natural del mundo y exigirle una tercera columna constante a cada cliente
        // sería ponerle trabajo al DBA para proteger una semántica que el mismo default ya fija.
        var codigoFuente = "BANCO_CENTRAL";
        var fuenteId = await _contexto.FuenteTipoCambioCatalogo
            .AsNoTracking()
            .Where(f => f.Codigo == codigoFuente && f.EsActivo)
            .Select(f => f.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (fuenteId == 0)
        {
            throw new InvalidOperationException(
                "No está sembrado el catálogo FuenteTipoCambio (falta BANCO_CENTRAL). Corra la semilla.");
        }

        var origenDatoId = await _contexto.OrigenDatoCatalogo
            .AsNoTracking()
            .Where(o => o.Codigo == "ERP")
            .Select(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (origenDatoId == 0)
        {
            throw new InvalidOperationException(
                "No está sembrado el catálogo OrigenDato (falta ERP). Corra la semilla.");
        }

        // Índice de fechas ya leídas en esta corrida: una fecha repetida en el origen no puede
        // insertar dos veces (la segunda actualiza la primera), en simulación y en real igual.
        var fechasEnElLote = new HashSet<string>(StringComparer.Ordinal);
        var codigosExternos = new List<string>();
        var fechas = new List<DateOnly>();

        foreach (var fila in filas)
        {
            var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
            var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Fecha"));
            if (codigo is not null)
            {
                codigosExternos.Add(codigo);
                if (DateOnly.TryParse(codigo, out var fecha))
                {
                    fechas.Add(fecha);
                }
            }
        }

        // Las filas de esta conexión, por fecha ISO.
        var existentes = await _contexto.TipoCambio
            .Where(t => t.ErpConexionId == configuracion.ErpConexionId
                     && t.CodigoExterno != null
                     && codigosExternos.Contains(t.CodigoExterno))
            .ToDictionaryAsync(t => t.CodigoExterno!, StringComparer.Ordinal, cancellationToken);

        // Huérfanas: filas sembradas a mano con la misma (fecha, fuente) — sin conexión. Sin
        // adoptarlas, UX_TipoCambio_Fecha_Fuente rechazaría el insert con un error que no dice
        // de dónde viene el conflicto. Se compara DateOnly directo y no Fecha.ToString():
        // convertir fechas dentro de la consulta no es traducible a SQL y el lote completo
        // rebotaría con un error de LINQ.
        var huerfanas = await _contexto.TipoCambio
            .Where(t => t.ErpConexionId == null
                     && t.FuenteId == fuenteId
                     && fechas.Contains(t.Fecha))
            .ToDictionaryAsync(t => t.Fecha.ToString("yyyy-MM-dd"), StringComparer.Ordinal, cancellationToken);

        var numeroFila = 1;
        var simulados = new List<CambioSimulado>();
        var hashesSimulados = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var fila in filas)
        {
            try
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);

                var fechaTexto = Normalizacion.AString(mapeada.GetValueOrDefault("Fecha"))
                    ?? throw new FormatException("El campo 'Fecha' es obligatorio (YYYY-MM-DD).");

                if (!DateOnly.TryParse(fechaTexto, out var fecha))
                {
                    throw new FormatException($"La fecha '{fechaTexto}' no es válida. Formato esperado: YYYY-MM-DD.");
                }

                var valor = Normalizacion.ADecimal(mapeada.GetValueOrDefault("Valor"), "Valor");

                if (valor <= 0)
                {
                    throw new FormatException($"El valor del tipo de cambio debe ser mayor que cero (llegó {valor}).");
                }

                var hash = Normalizacion.HashContenido(mapeada);
                var codigoExterno = fecha.ToString("yyyy-MM-dd");

                if (!fechasEnElLote.Add(codigoExterno))
                {
                    // Fecha duplicada dentro del lote: se rechaza en vez de sobrescribir a
                    // medias, porque dos valores para el mismo día en el mismo origen son un dato
                    // corrupto del ERP y hay que verlo, no desempatarlo en silencio.
                    throw new FormatException($"La fecha '{codigoExterno}' aparece dos veces en el origen.");
                }

                if (existentes.TryGetValue(codigoExterno, out var existente))
                {
                    var hashExistente = hashesSimulados.TryGetValue(codigoExterno, out var yaSimulado)
                        ? yaSimulado
                        : existente?.HashContenido is { Length: > 0 }
                            ? Convert.ToHexString(existente.HashContenido)
                            : null;

                    if (hashExistente == hash)
                    {
                        omitidos++;
                        if (configuracion.EsSimulacion)
                        {
                            simulados.Add(new CambioSimulado(numeroFila, codigoExterno, AccionSimulada.Omitir, []));
                        }
                    }
                    else
                    {
                        var cambios = new List<CampoSimulado>();
                        if (existente.Valor != valor)
                        {
                            cambios.Add(new CampoSimulado("Valor", existente.Valor.ToString("N4"), valor.ToString("N4")));
                        }

                        if (configuracion.EsSimulacion)
                        {
                            simulados.Add(new CambioSimulado(numeroFila, codigoExterno, AccionSimulada.Actualizar, cambios));
                            hashesSimulados[codigoExterno] = hash;
                        }
                        else
                        {
                            existente.Valor = valor;
                            existente.HashContenido = Convert.FromHexString(hash);
                            existente.UltimaSyncUtc = DateTime.UtcNow;
                            existente.UpdatedAt = DateTime.UtcNow;
                        }

                        actualizados++;
                    }
                }
                else if (huerfanas.Remove(codigoExterno, out var huerfana))
                {
                    if (configuracion.EsSimulacion)
                    {
                        simulados.Add(new CambioSimulado(numeroFila, codigoExterno, AccionSimulada.Adoptar,
                            [new CampoSimulado("Procedencia", "Ninguna (capturada a mano)",
                                $"La conexión «{configuracion.ErpConexionId}»")]));
                        hashesSimulados[codigoExterno] = hash;
                    }
                    else
                    {
                        huerfana.CodigoExterno = codigoExterno;
                        huerfana.ErpConexionId = configuracion.ErpConexionId;
                        huerfana.OrigenDatoId = origenDatoId;
                        huerfana.Valor = valor;
                        huerfana.HashContenido = Convert.FromHexString(hash);
                        huerfana.UltimaSyncUtc = DateTime.UtcNow;
                        huerfana.UpdatedAt = DateTime.UtcNow;
                    }

                    existentes[codigoExterno] = huerfana;
                    actualizados++;
                }
                else
                {
                    var nuevo = new TipoCambio
                    {
                        Id = Guid.NewGuid(),
                        Fecha = fecha,
                        Valor = valor,
                        FuenteId = fuenteId,
                        OrigenDatoId = origenDatoId,
                        ErpConexionId = configuracion.ErpConexionId,
                        CodigoExterno = codigoExterno,
                        HashContenido = Convert.FromHexString(hash),
                        UltimaSyncUtc = DateTime.UtcNow,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    if (configuracion.EsSimulacion)
                    {
                        simulados.Add(new CambioSimulado(numeroFila, codigoExterno, AccionSimulada.Insertar, []));
                    }
                    else
                    {
                        _contexto.TipoCambio.Add(nuevo);
                    }

                    existentes[codigoExterno] = nuevo;
                    insertados++;
                }
            }
            catch (Exception ex)
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
                rechazados.Add(new RechazoCarga(numeroFila, Normalizacion.AString(mapeada.GetValueOrDefault("Fecha")), ex.Message, null));
            }

            numeroFila++;
        }

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
}
