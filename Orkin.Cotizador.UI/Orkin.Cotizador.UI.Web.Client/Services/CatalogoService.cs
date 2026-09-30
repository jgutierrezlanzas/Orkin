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

    public async Task<List<ProductoModel>> ListarProductosAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<ProductoModel>>>("api/catalogos/productos");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<ProductoModel>?> CrearProductoAsync(ProductoModel modelo)
    {
        var request = new
        {
            modelo.Codigo,
            modelo.Nombre,
            modelo.Descripcion,
            modelo.CategoriaId,
            modelo.UnidadMedidaId,
            modelo.CostoActual,
            modelo.PrecioVentaActual,
            modelo.VidaUtilMeses
        };

        var response = await _http.PostAsJsonAsync("api/catalogos/productos", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ProductoModel>>();
    }

    public async Task<ApiResponse<ProductoModel>?> ActualizarProductoAsync(ProductoModel modelo)
    {
        var request = new
        {
            modelo.Nombre,
            modelo.Descripcion,
            modelo.CategoriaId,
            modelo.UnidadMedidaId,
            modelo.CostoActual,
            modelo.PrecioVentaActual,
            modelo.VidaUtilMeses,
            modelo.EsActivo
        };

        var response = await _http.PutAsJsonAsync($"api/catalogos/productos/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ProductoModel>>();
    }

    public async Task<List<ClienteModel>> ListarClientesAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<ClienteModel>>>("api/catalogos/clientes");
        return respuesta?.Datos ?? [];
    }

    public async Task<List<TipoClienteOption>> ObtenerTiposClienteAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<TipoClienteOption>>>("api/catalogos/clientes/tipos");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<ClienteModel>?> CrearClienteAsync(ClienteModel modelo)
    {
        var request = new
        {
            modelo.Codigo,
            modelo.Nombre,
            modelo.RazonSocial,
            modelo.TipoClienteId,
            modelo.CedulaORuc,
            modelo.IdentificacionFiscal,
            modelo.Direccion,
            modelo.Ciudad,
            modelo.Departamento,
            modelo.Telefono,
            modelo.Correo,
            modelo.ContactoNombre,
            modelo.ContactoCargo,
            modelo.SitioWeb,
            modelo.Notas
        };

        var response = await _http.PostAsJsonAsync("api/catalogos/clientes", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ClienteModel>>();
    }

    public async Task<ApiResponse<ClienteModel>?> ActualizarClienteAsync(ClienteModel modelo)
    {
        var request = new
        {
            modelo.Nombre,
            modelo.RazonSocial,
            modelo.TipoClienteId,
            modelo.CedulaORuc,
            modelo.IdentificacionFiscal,
            modelo.Direccion,
            modelo.Ciudad,
            modelo.Departamento,
            modelo.Telefono,
            modelo.Correo,
            modelo.ContactoNombre,
            modelo.ContactoCargo,
            modelo.SitioWeb,
            modelo.Notas,
            modelo.EsActivo
        };

        var response = await _http.PutAsJsonAsync($"api/catalogos/clientes/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ClienteModel>>();
    }

    /// <summary>
    /// El tipo de cambio vigente para una fecha — el último valor cuya fecha sea ≤ la fecha
    /// consultada. Lo muestra la pantalla de cotizar, para que quien cotiza sepa a cuánto
    /// está el dólar sin esperar a que el cálculo lo traiga de vuelta.
    /// </summary>
    public async Task<TipoCambioModel?> ObtenerTipoCambioVigenteAsync(DateOnly fecha)
    {
        var response = await _http.GetFromJsonAsync<ApiResponse<TipoCambioModel>>(
            $"api/catalogos/tipos-cambio/vigente?fecha={fecha:yyyy-MM-dd}");
        return response?.Datos;
    }

    public async Task<List<TipoCambioModel>> ListarTiposCambioAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<TipoCambioModel>>>("api/catalogos/tipos-cambio");
        return respuesta?.Datos ?? [];
    }

    public async Task<List<FuenteTipoCambioOption>> ObtenerFuentesTipoCambioAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<FuenteTipoCambioOption>>>("api/catalogos/tipos-cambio/fuentes");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<TipoCambioModel>?> CrearTipoCambioAsync(TipoCambioModel modelo)
    {
        var request = new
        {
            modelo.Fecha,
            modelo.Valor,
            modelo.FuenteId
        };

        var response = await _http.PostAsJsonAsync("api/catalogos/tipos-cambio", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<TipoCambioModel>>();
    }

    public async Task<ApiResponse<TipoCambioModel>?> ActualizarTipoCambioAsync(TipoCambioModel modelo)
    {
        var request = new
        {
            modelo.Fecha,
            modelo.Valor,
            modelo.FuenteId,
            modelo.EsActivo
        };

        var response = await _http.PutAsJsonAsync($"api/catalogos/tipos-cambio/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<TipoCambioModel>>();
    }
}
