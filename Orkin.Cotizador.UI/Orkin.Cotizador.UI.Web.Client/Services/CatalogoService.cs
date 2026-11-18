using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Acceso a las opciones de catálogo leídas en vivo del ERP y a los tipos de cambio, que sí son
/// datos propios de la plataforma. Los catálogos espejo y su CRUD se retiraron (D-24): el ERP es
/// la única fuente de clientes, técnicos, artículos y el resto de las listas operativas.
/// </summary>
public sealed class CatalogoService
{
    private readonly HttpClient _http;

    public CatalogoService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Opciones legibles de las vistas ERP. Un solo endpoint para todos los códigos que antes se
    /// capturaban a ciegas; el filtro por cliente solo acota las locaciones (D-21).
    /// </summary>
    public async Task<OpcionesErpModel> ListarOpcionesErpAsync(string? cliente = null)
    {
        var url = string.IsNullOrWhiteSpace(cliente)
            ? "api/catalogos/opciones-erp"
            : $"api/catalogos/opciones-erp?cliente={Uri.EscapeDataString(cliente.Trim())}";
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<OpcionesErpModel>>(url);
        return respuesta?.Datos ?? new OpcionesErpModel();
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
