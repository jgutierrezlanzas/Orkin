using System.Globalization;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Cliente de la API única de reportes (Fase 7). La web solo pide datos, PDF o CSV: el dataset
/// sale de los SPs legacy y el PDF se arma en la API. Un error del SP (su <c>RAISERROR</c>) llega
/// en el cuerpo <c>ApiResponse.Mensaje</c> y se lanza tal cual para que la página lo muestre.
/// </summary>
public sealed class ReportesService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    public ReportesService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task<List<ReporteDefinicionModel>> ObtenerCatalogoAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<ReporteDefinicionModel>>>(
            "api/reportes/catalogo");
        return respuesta?.Datos ?? [];
    }

    public async Task<List<FirmaReporteModel>> ObtenerFirmasAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<FirmaReporteModel>>>(
            "api/reportes/firmas");
        return respuesta?.Datos ?? [];
    }

    /// <summary>Vista previa acotada para el visor.</summary>
    public async Task<ReporteDatosModel?> ObtenerDatosAsync(
        string clave,
        IReadOnlyDictionary<string, object?> parametros,
        int limite = 100)
    {
        var query = Query(parametros);
        query.Add($"limite={limite}");
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<ReporteDatosModel>>(
            $"api/reportes/{Uri.EscapeDataString(clave)}/datos?{string.Join('&', query)}");
        return respuesta?.Datos;
    }

    /// <summary>Pide el PDF o el CSV a la API y dispara la descarga en el navegador.</summary>
    public async Task DescargarAsync(
        string clave,
        string formato,
        IReadOnlyDictionary<string, object?> parametros,
        string? nombre = null)
    {
        var Extension = formato == "csv" ? "csv" : "pdf";
        var mime = formato == "csv" ? "text/csv;charset=utf-8" : "application/pdf";
        var url = $"api/reportes/{Uri.EscapeDataString(clave)}/{Extension}";
        var query = Query(parametros);
        if (query.Count > 0)
        {
            url += "?" + string.Join('&', query);
        }

        var bytes = await ObtenerBytesAsync(url);
        await DescargarArchivoAsync(bytes, nombre ?? $"{clave}.{Extension}", mime);
    }

    /// <summary>Imprime una OT desde su listado (permiso <c>OT.IMPRIMIR</c>).</summary>
    public async Task ImprimirOrdenAsync(string numero)
    {
        var bytes = await ObtenerBytesAsync(
            $"api/reportes/impresion/orden/{Uri.EscapeDataString(numero)}");
        await DescargarArchivoAsync(bytes, $"OT-{numero}.pdf", "application/pdf");
    }

    /// <summary>Imprime una requisa desde su listado (permiso <c>REQUISAS.IMPRIMIR</c>).</summary>
    public async Task ImprimirRequisaAsync(string numero, DateOnly fecha)
    {
        var bytes = await ObtenerBytesAsync(
            $"api/reportes/impresion/requisa/{Uri.EscapeDataString(numero)}" +
            $"?desde={fecha:yyyy-MM-dd}&hasta={fecha:yyyy-MM-dd}");
        await DescargarArchivoAsync(bytes, $"RQ-{numero}.pdf", "application/pdf");
    }

    private async Task<byte[]> ObtenerBytesAsync(string url)
    {
        var respuesta = await _http.GetAsync(url);
        if (respuesta.IsSuccessStatusCode)
        {
            var bytes = await respuesta.Content.ReadAsByteArrayAsync();
            if (bytes.Length > 0)
            {
                return bytes;
            }

            throw new InvalidOperationException("La API devolvió un archivo vacío.");
        }

        var error = await respuesta.Content.ReadFromJsonAsync<ApiResponse<object>>();
        throw new InvalidOperationException(error?.Mensaje ?? "No se pudo generar el archivo.");
    }

    private async Task DescargarArchivoAsync(byte[] bytes, string nombre, string mime)
        => await _js.InvokeVoidAsync(
            "descargarArchivo",
            Convert.ToBase64String(bytes),
            nombre,
            mime);

    private static List<string> Query(IReadOnlyDictionary<string, object?> parametros)
    {
        var query = new List<string>();
        foreach (var (clave, valor) in parametros)
        {
            if (valor is null)
            {
                continue;
            }

            var texto = valor switch
            {
                DateOnly fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                bool booleano => booleano ? "true" : "false",
                int entero => entero.ToString(CultureInfo.InvariantCulture),
                _ => valor.ToString()?.Trim() ?? string.Empty
            };

            if (texto.Length > 0)
            {
                query.Add($"{Uri.EscapeDataString(clave)}={Uri.EscapeDataString(texto)}");
            }
        }

        return query;
    }
}
