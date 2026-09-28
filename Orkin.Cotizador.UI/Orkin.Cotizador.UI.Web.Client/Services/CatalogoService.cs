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

    public async Task<List<CategoriaProductoModel>> ListarCategoriasProductoAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<CategoriaProductoModel>>>("api/catalogos/categorias-producto");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<CategoriaProductoModel>?> CrearCategoriaProductoAsync(CategoriaProductoModel modelo)
    {
        var request = new
        {
            modelo.Codigo,
            modelo.Nombre,
            modelo.Descripcion,
            modelo.Orden
        };

        var response = await _http.PostAsJsonAsync("api/catalogos/categorias-producto", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<CategoriaProductoModel>>();
    }

    public async Task<ApiResponse<CategoriaProductoModel>?> ActualizarCategoriaProductoAsync(CategoriaProductoModel modelo)
    {
        var request = new
        {
            modelo.Nombre,
            modelo.Descripcion,
            modelo.Orden,
            modelo.EsActivo
        };

        var response = await _http.PutAsJsonAsync($"api/catalogos/categorias-producto/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<CategoriaProductoModel>>();
    }

    public async Task<List<TipoServicioModel>> ListarTiposServicioAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<TipoServicioModel>>>("api/catalogos/tipos-servicio");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<TipoServicioModel>?> CrearTipoServicioAsync(TipoServicioModel modelo)
    {
        var request = new
        {
            modelo.Codigo,
            modelo.Nombre,
            modelo.Descripcion,
            modelo.UnidadMedidaId,
            modelo.EsResidencialPorDefecto,
            modelo.EsEmpresarialPorDefecto,
            modelo.Orden
        };

        var response = await _http.PostAsJsonAsync("api/catalogos/tipos-servicio", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<TipoServicioModel>>();
    }

    public async Task<ApiResponse<TipoServicioModel>?> ActualizarTipoServicioAsync(TipoServicioModel modelo)
    {
        var request = new
        {
            modelo.Nombre,
            modelo.Descripcion,
            modelo.UnidadMedidaId,
            modelo.EsResidencialPorDefecto,
            modelo.EsEmpresarialPorDefecto,
            modelo.Orden,
            modelo.EsActivo
        };

        var response = await _http.PutAsJsonAsync($"api/catalogos/tipos-servicio/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<TipoServicioModel>>();
    }
}
