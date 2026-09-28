using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

public sealed class CotizacionService
{
    private readonly HttpClient _http;

    public CotizacionService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<CotizacionCalculadaResponse>?> CalcularAsync(
        Guid clienteId,
        DateOnly fecha,
        decimal margen,
        List<LineaInsumoRequest> insumos)
    {
        var request = new
        {
            ClienteId = clienteId,
            Fecha = fecha,
            Margen = margen,
            Insumos = insumos.Select(i => new
            {
                i.ProductoId,
                i.Cantidad,
                i.SemestreId
            }).ToList(),
            Amortizables = new List<object>()
        };

        var response = await _http.PostAsJsonAsync("api/cotizaciones/calcular", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<CotizacionCalculadaResponse>>();
    }

    public async Task<PaginatedCotizaciones> ListarAsync(
        int pagina = 1,
        int tamanio = 20,
        Guid? clienteId = null,
        string? estado = null)
    {
        var url = $"api/cotizaciones?pagina={pagina}&tamanio={tamanio}";

        if (clienteId.HasValue)
        {
            url += $"&clienteId={clienteId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            url += $"&estado={estado}";
        }

        var respuesta = await _http.GetFromJsonAsync<ApiResponse<PaginatedCotizaciones>>(url);
        return respuesta?.Datos ?? new PaginatedCotizaciones();
    }

    public async Task<CotizacionOpciones> ObtenerOpcionesAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<CotizacionOpciones>>("api/cotizaciones/opciones");
        return respuesta?.Datos ?? new CotizacionOpciones();
    }

    public async Task<ApiResponse<CotizacionListItem>?> CrearAsync(
        Guid clienteId,
        Guid tipoServicioId,
        DateOnly fecha,
        string? modalidadCodigo,
        decimal margen,
        List<LineaInsumoRequest> insumos)
    {
        var request = new
        {
            ClienteId = clienteId,
            TipoServicioId = tipoServicioId,
            Fecha = fecha,
            ModalidadCodigo = modalidadCodigo,
            Margen = margen,
            Insumos = insumos.Select(i => new
            {
                i.ProductoId,
                i.Cantidad,
                i.SemestreId
            }).ToList(),
            Amortizables = new List<object>()
        };

        var response = await _http.PostAsJsonAsync("api/cotizaciones", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<CotizacionListItem>>();
    }
}