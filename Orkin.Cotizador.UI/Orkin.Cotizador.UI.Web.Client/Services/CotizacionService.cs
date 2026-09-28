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
            }).ToList()
        };

        var response = await _http.PostAsJsonAsync("api/cotizaciones/calcular", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<CotizacionCalculadaResponse>>();
    }
}
