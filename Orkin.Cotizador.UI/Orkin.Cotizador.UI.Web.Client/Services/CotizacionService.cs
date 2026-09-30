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

    /// <summary>
    /// Calcula en memoria. El cliente puede venir del catálogo (<paramref name="clienteId"/>) o
    /// ser nuevo: en ese caso hace falta el tipo que elige quien cotiza.
    /// </summary>
    public async Task<ApiResponse<CotizacionCalculadaResponse>?> CalcularAsync(
        Guid? clienteId,
        byte? tipoClienteId,
        DateOnly fecha,
        decimal margen,
        List<LineaInsumoRequest> insumos)
    {
        var request = new
        {
            ClienteId = clienteId,
            TipoClienteId = tipoClienteId,
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

    /// <summary>
    /// Crea la cotización. Con <paramref name="clienteId"/> va para un cliente del catálogo; sin
    /// él, para un cliente nuevo cuyo nombre va en <paramref name="clienteNombre"/> y cuyo tipo
    /// elige quien cotiza.
    /// </summary>
    public async Task<ApiResponse<CotizacionListItem>?> CrearAsync(
        Guid? clienteId,
        string? clienteNombre,
        byte? tipoClienteId,
        Guid tipoServicioId,
        DateOnly fecha,
        string? modalidadCodigo,
        decimal margen,
        List<LineaInsumoRequest> insumos)
    {
        var request = new
        {
            ClienteId = clienteId,
            ClienteNombre = clienteNombre,
            TipoClienteId = tipoClienteId,
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