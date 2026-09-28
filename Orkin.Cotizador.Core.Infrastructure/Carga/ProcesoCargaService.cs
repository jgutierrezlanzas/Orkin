using Microsoft.Extensions.Logging;

using Orkin.Cotizador.Core.Domain.Carga;

namespace Orkin.Cotizador.Core.Infrastructure.Carga;

/// <summary>
/// Orquesta el proceso de carga: recibe una configuración y un lector, resuelve el cargador
/// apropiado para el catálogo, lee las filas del origen y las carga en la base.
/// </summary>
public sealed class ProcesoCargaService
{
    private readonly ILogger<ProcesoCargaService> _logger;
    private readonly IReadOnlyDictionary<string, ICargadorCatalogo> _cargadores;

    public ProcesoCargaService(
        ILogger<ProcesoCargaService> logger,
        IEnumerable<ICargadorCatalogo> cargadores)
    {
        _logger = logger;
        _cargadores = cargadores.ToDictionary(c => c.Catalogo, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ejecuta la carga: lee del origen y carga en el catálogo configurado.
    /// </summary>
    public async Task<ResultadoCarga> EjecutarAsync(
        ConfiguracionCarga configuracion,
        ILectorOrigen lector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        ArgumentNullException.ThrowIfNull(lector);

        if (!_cargadores.TryGetValue(configuracion.Catalogo, out var cargador))
        {
            throw new InvalidOperationException(
                $"No hay cargador registrado para el catálogo '{configuracion.Catalogo}'. " +
                $"Los catálogos disponibles son: {string.Join(", ", _cargadores.Keys.OrderBy(k => k))}.");
        }

        if (!string.Equals(lector.TipoOrigen, configuracion.TipoOrigen, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"El lector es de tipo '{lector.TipoOrigen}' pero la configuración dice '{configuracion.TipoOrigen}'.");
        }

        _logger.LogInformation(
            "Iniciando carga de {Catalogo} desde {TipoOrgen}. Lector: {TipoLector}.",
            configuracion.Catalogo, configuracion.TipoOrigen, lector.GetType().Name);

        var filas = await lector.LeerAsync(cancellationToken);

        _logger.LogInformation(
            "Se leyeron {Cantidad} filas del origen {TipoOrigen} para {Catalogo}.",
            filas.Count, configuracion.TipoOrigen, configuracion.Catalogo);

        var resultado = await cargador.CargarAsync(filas, configuracion, cancellationToken);

        _logger.LogInformation(
            "Carga de {Catalogo} completada: {Insertados} insertados, {Actualizados} actualizados, " +
            "{Omitidos} omitidos, {Rechazados} rechazados.",
            configuracion.Catalogo, resultado.Insertados, resultado.Actualizados,
            resultado.Omitidos, resultado.Rechazados);

        return resultado;
    }
}
