using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Acceso a los endpoints de catálogos de la API.
/// </summary>
public sealed class CatalogoService
{
    private readonly HttpClient _http;

    public CatalogoService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<UnidadMedidaModel>> ListarUnidadesMedidaAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<UnidadMedidaModel>>>("api/catalogos/unidades-medida");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<UnidadMedidaModel>?> CrearUnidadMedidaAsync(UnidadMedidaModel modelo)
    {
        var request = new
        {
            modelo.Codigo,
            modelo.Nombre,
            modelo.Abreviatura,
            modelo.PermiteDecimales,
            modelo.Orden
        };

        var response = await _http.PostAsJsonAsync("api/catalogos/unidades-medida", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<UnidadMedidaModel>>();
    }

    public async Task<ApiResponse<UnidadMedidaModel>?> ActualizarUnidadMedidaAsync(UnidadMedidaModel modelo)
    {
        var request = new
        {
            modelo.Nombre,
            modelo.Abreviatura,
            modelo.PermiteDecimales,
            modelo.Orden,
            modelo.EsActivo
        };

        var response = await _http.PutAsJsonAsync($"api/catalogos/unidades-medida/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<UnidadMedidaModel>>();
    }
}
