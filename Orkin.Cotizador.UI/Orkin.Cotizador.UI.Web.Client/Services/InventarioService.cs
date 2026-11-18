using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Acceso a la API única de documentos de inventario (Fase 6). La clave de cada fila es la PK
/// legacy compuesta, expuesta por <see cref="DocumentoModel.Ruta"/>.
/// </summary>
public sealed class InventarioService
{
    private readonly HttpClient _http;

    public InventarioService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<DocumentoModel>> ListarAsync(
        string? texto = null,
        string? transferido = null,
        DateOnly? desde = null,
        DateOnly? hasta = null)
    {
        var parametros = new List<string>();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            parametros.Add($"texto={Uri.EscapeDataString(texto.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(transferido) &&
            !transferido.Equals("Todos", StringComparison.OrdinalIgnoreCase))
        {
            parametros.Add($"transferido={Uri.EscapeDataString(transferido.Trim())}");
        }

        if (desde is { } d)
        {
            parametros.Add($"desde={d:yyyy-MM-dd}");
        }

        if (hasta is { } h)
        {
            parametros.Add($"hasta={h:yyyy-MM-dd}");
        }

        var url = "api/inventario/documentos" +
                  (parametros.Count == 0 ? string.Empty : "?" + string.Join('&', parametros));

        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<DocumentoModel>>>(url);
        return respuesta?.Datos ?? [];
    }

    public async Task<DocumentoModel?> ObtenerAsync(string paquete, string documento)
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<DocumentoModel>>(
            $"api/inventario/documentos/{Uri.EscapeDataString(paquete)}/{Uri.EscapeDataString(documento)}");
        return respuesta?.Datos;
    }

    public Task<ExactusResultadoModel> GenerarDesdeRequisaAsync(string requisaNumero)
        => LeerAsync(
            _http.PostAsync(
                $"api/inventario/documentos/generar-desde-requisa/{Uri.EscapeDataString(requisaNumero)}",
                content: null),
            "No se pudo generar el documento de inventario.");

    public Task<ExactusResultadoModel> TransferirAsync(DocumentoModel documento)
        => LeerAsync(
            _http.PostAsync($"api/inventario/documentos/{documento.Ruta}/transferir", content: null),
            "No se pudo trasladar el documento a Exactus.");

    private static async Task<ExactusResultadoModel> LeerAsync(
        Task<HttpResponseMessage> tarea,
        string mensajePorDefecto)
    {
        var respuesta = await tarea;
        var contenido = await respuesta.Content.ReadFromJsonAsync<ApiResponse<ExactusResultadoModel>>();

        if (!respuesta.IsSuccessStatusCode || contenido is null || !contenido.Exito)
        {
            throw new InvalidOperationException(contenido?.Mensaje ?? mensajePorDefecto);
        }

        return contenido.Datos
            ?? throw new InvalidOperationException(mensajePorDefecto);
    }
}
