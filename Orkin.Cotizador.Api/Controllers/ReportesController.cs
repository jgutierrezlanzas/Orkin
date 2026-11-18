using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Reportes;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Reportes e impresión (Fase 7). El visor pide datos y PDF/CSV a la API; los SPs legacy se
/// invocan con parámetros desde <see cref="IReporteService"/> y los mensajes <c>RAISERROR</c>
/// llegan al cliente sin traducir.
/// </summary>
[ApiController]
[Route("api/reportes")]
public sealed class ReportesController : ControllerBase
{
    private readonly IReporteService _reportes;
    private readonly Reportes.GeneradorPdfReporte _generadorPdf;

    public ReportesController(IReporteService reportes, Reportes.GeneradorPdfReporte generadorPdf)
    {
        _reportes = reportes;
        _generadorPdf = generadorPdf;
    }

    /// <summary>Catálogo único: un reporte por SP, con sus parámetros y columnas.</summary>
    [HttpGet("catalogo")]
    [Authorize("REPORTES.VER")]
    public IResult Catalogo() =>
        Results.Ok(ApiResponse<IReadOnlyList<ReporteDefinicion>>.Ok(_reportes.Catalogo));

    /// <summary>Bloque de firmas del documento 0003 en <c>vvTipodeDocumentosFirma</c>.</summary>
    [HttpGet("firmas")]
    [Authorize("REPORTES.VER")]
    public async Task<IResult> Firmas(CancellationToken cancellationToken)
    {
        var firmas = await _reportes.ObtenerFirmasAsync(cancellationToken);
        return Results.Ok(ApiResponse<List<FirmaReporte>>.Ok(firmas));
    }

    /// <summary>Vista previa acotada para el visor web.</summary>
    [HttpGet("{clave}/datos")]
    [Authorize("REPORTES.VER")]
    public async Task<IResult> Datos(
        string clave,
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        [FromQuery] string? numero,
        [FromQuery] int? anio,
        [FromQuery] int? mes,
        [FromQuery] string? fechaCorte,
        [FromQuery] bool? vencida,
        [FromQuery] int limite = 100,
        CancellationToken cancellationToken = default) =>
        await EjecutarAsync(clave, Parametros(desde, hasta, numero, anio, mes, fechaCorte, vencida),
            async (resultado, ct) =>
            {
                var datos = new
                {
                    clave = resultado.Definicion.Clave,
                    nombre = resultado.Definicion.Nombre,
                    columnas = resultado.Definicion.Columnas,
                    filas = resultado.Filas,
                    total = resultado.Filas.Count
                };
                return Results.Ok(ApiResponse<object>.Ok(datos));
            }, Math.Clamp(limite, 1, 500), cancellationToken);

    /// <summary>Descarga el PDF generado en la API con QuestPDF desde el dataset del SP.</summary>
    [HttpGet("{clave}/pdf")]
    [Authorize("REPORTES.IMPRIMIR")]
    public async Task<IResult> Pdf(
        string clave,
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        [FromQuery] string? numero,
        [FromQuery] int? anio,
        [FromQuery] int? mes,
        [FromQuery] string? fechaCorte,
        [FromQuery] bool? vencida,
        CancellationToken cancellationToken = default) =>
        await EjecutarAsync(clave,
            Parametros(desde, hasta, numero, anio, mes, fechaCorte, vencida),
            (resultado, _) => Task.FromResult<IResult>(
                Results.File(_generadorPdf.Generar(resultado), "application/pdf", $"{resultado.Definicion.Clave}.pdf")),
            null, cancellationToken);

    /// <summary>Exporta el mismo dataset a CSV con BOM para que Excel respete los acentos.</summary>
    [HttpGet("{clave}/csv")]
    [Authorize("REPORTES.EXPORTAR")]
    public async Task<IResult> Csv(
        string clave,
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        [FromQuery] string? numero,
        [FromQuery] int? anio,
        [FromQuery] int? mes,
        [FromQuery] string? fechaCorte,
        [FromQuery] bool? vencida,
        CancellationToken cancellationToken = default) =>
        await EjecutarAsync(clave,
            Parametros(desde, hasta, numero, anio, mes, fechaCorte, vencida),
            (resultado, _) => Task.FromResult<IResult>(
                Results.File(_reportes.GenerarCsv(resultado), "text/csv; charset=utf-8",
                    $"{resultado.Definicion.Clave}.csv")),
            null, cancellationToken);

    /// <summary>
    /// Impresión de una OT desde su listado. Está separado del PDF genérico porque el permiso es
    /// propio del módulo (<c>OT.IMPRIMIR</c>): quien puede imprimir una orden no tiene por qué
    /// poder ejecutar cualquier reporte de costos.
    /// </summary>
    [HttpGet("impresion/orden/{numero}")]
    [Authorize("OT.IMPRIMIR")]
    public Task<IResult> ImprimirOrden(string numero, CancellationToken cancellationToken) =>
        EjecutarAsync(
            "orden",
            new Dictionary<string, object?> { ["numero"] = numero },
            (resultado, _) => Task.FromResult<IResult>(Results.File(
                _generadorPdf.Generar(resultado),
                "application/pdf",
                $"OT-{numero}.pdf")),
            null,
            cancellationToken);

    /// <summary>
    /// Impresión de una requisa desde su listado (<c>REQUISAS.IMPRIMIR</c>). El SP legacy solo
    /// acepta un rango de fechas, así que se acota el dataset a la requisa pedida antes de armar
    /// el PDF; no se reescribe el SP ni se filtra en SQL con datos del usuario.
    /// </summary>
    [HttpGet("impresion/requisa/{numero}")]
    [Authorize("REQUISAS.IMPRIMIR")]
    public Task<IResult> ImprimirRequisa(
        string numero,
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        CancellationToken cancellationToken) =>
        EjecutarAsync(
            "requisas",
            Parametros(desde, hasta, null, null, null, null, null),
            (resultado, _) =>
            {
                var filas = resultado.Filas
                    .Where(f => string.Equals(
                        f.TryGetValue("REQUISA_NUMERO", out var valor) ? valor?.ToString()?.Trim() : null,
                        numero,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var acotado = resultado with
                {
                    Filas = filas,
                    Subtitulo = $"{resultado.Subtitulo} · Requisa {numero}"
                };
                return Task.FromResult<IResult>(Results.File(
                    _generadorPdf.Generar(acotado),
                    "application/pdf",
                    $"RQ-{numero}.pdf"));
            },
            null,
            cancellationToken);

    private static Dictionary<string, object?> Parametros(
        string? desde, string? hasta, string? numero, int? anio, int? mes,
        string? fechaCorte, bool? vencida)
    {
        var parametros = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (desde is not null) parametros["desde"] = desde;
        if (hasta is not null) parametros["hasta"] = hasta;
        if (numero is not null) parametros["numero"] = numero;
        if (anio.HasValue) parametros["anio"] = anio.Value;
        if (mes.HasValue) parametros["mes"] = mes.Value;
        if (fechaCorte is not null) parametros["fechaCorte"] = fechaCorte;
        if (vencida.HasValue) parametros["vencida"] = vencida.Value;
        return parametros;
    }

    private async Task<IResult> EjecutarAsync(
        string clave,
        IReadOnlyDictionary<string, object?> parametros,
        Func<ReporteResultado, CancellationToken, Task<IResult>> respuesta,
        int? limite,
        CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _reportes.ObtenerAsync(clave, parametros, limite, cancellationToken);
            return await respuesta(resultado, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<object>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<object>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            // Mensaje del RAISERROR del SP, sin envolver ni sustituir.
            return ApiResponse<object>.Error("SP_ERROR", ex.Message).AHttp();
        }
    }
}
