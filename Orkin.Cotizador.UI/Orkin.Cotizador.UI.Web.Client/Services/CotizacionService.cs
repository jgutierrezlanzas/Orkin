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
    /// Calcula en memoria. El cliente puede venir del ERP (<paramref name="clienteCodigo"/>) o ser
    /// nuevo: en ese caso hace falta el tipo que elige quien cotiza.
    /// </summary>
    public async Task<ApiResponse<CotizacionCalculadaResponse>?> CalcularAsync(
        string? clienteCodigo,
        byte? tipoClienteId,
        DateOnly fecha,
        decimal margen,
        List<LineaInsumoRequest> insumos,
        List<LineaAmortizableRequest>? amortizables = null)
    {
        var request = new
        {
            ClienteCodigo = clienteCodigo,
            TipoClienteId = tipoClienteId,
            Fecha = fecha,
            Margen = margen,
            Insumos = insumos.Select(i => new
            {
                i.ProductoCodigo,
                i.Cantidad,
                i.SemestreId
            }).ToList(),
            Amortizables = (amortizables ?? []).Select(a => new
            {
                a.ActivoCodigo,
                a.Cantidad,
                a.MesesVidaUtil
            }).ToList()
        };

        var response = await _http.PostAsJsonAsync("api/cotizaciones/calcular", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<CotizacionCalculadaResponse>>();
    }

    public async Task<PaginatedCotizaciones> ListarAsync(
        int pagina = 1,
        int tamanio = 20,
        string? clienteCodigo = null,
        string? estado = null)
    {
        var url = $"api/cotizaciones?pagina={pagina}&tamanio={tamanio}";

        if (!string.IsNullOrWhiteSpace(clienteCodigo))
        {
            url += $"&clienteCodigo={Uri.EscapeDataString(clienteCodigo.Trim())}";
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            url += $"&estado={Uri.EscapeDataString(estado.Trim())}";
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
    /// Crea la cotización. Con <paramref name="clienteCodigo"/> va para un cliente del ERP; sin
    /// él, para uno nuevo cuyo nombre va en <paramref name="clienteNombre"/> y cuyo tipo elige
    /// quien cotiza.
    /// </summary>
    public async Task<ApiResponse<CotizacionListItem>?> CrearAsync(
        string? clienteCodigo,
        string? clienteNombre,
        byte? tipoClienteId,
        string tipoServicioCodigo,
        DateOnly fecha,
        string? modalidadCodigo,
        decimal margen,
        List<LineaInsumoRequest> insumos,
        List<LineaAmortizableRequest>? amortizables = null,
        string? tecnicoCodigo = null,
        DateOnly? fechaEstimada = null,
        string? direccionServicio = null)
    {
        var request = new
        {
            ClienteCodigo = clienteCodigo,
            ClienteNombre = clienteNombre,
            TipoClienteId = tipoClienteId,
            TipoServicioCodigo = tipoServicioCodigo,
            Fecha = fecha,
            ModalidadCodigo = modalidadCodigo,
            TecnicoCodigo = tecnicoCodigo,
            FechaEstimada = fechaEstimada,
            DireccionServicio = direccionServicio,
            Margen = margen,
            Insumos = insumos.Select(i => new
            {
                i.ProductoCodigo,
                i.Cantidad,
                i.SemestreId
            }).ToList(),
            Amortizables = (amortizables ?? []).Select(a => new
            {
                a.ActivoCodigo,
                a.Cantidad,
                a.MesesVidaUtil
            }).ToList()
        };

        var response = await _http.PostAsJsonAsync("api/cotizaciones", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<CotizacionListItem>>();
    }

    /// <summary>Trae una cotización con sus líneas, para recargar edición y detalle.</summary>
    public async Task<CotizacionDetalleModel?> ObtenerDetalleAsync(Guid id)
    {
        var response = await _http.GetFromJsonAsync<ApiResponse<CotizacionDetalleModel>>($"api/cotizaciones/{id}");
        return response?.Datos;
    }

    /// <summary>
    /// Actualiza una cotización existente (solo en BORRADOR o RECHAZADA). La API recalcula todo:
    /// las líneas se reemplazan completas.
    /// <para><b>Es de reemplazo, no de parche.</b> Lo que no se manda se borra: por eso
    /// <paramref name="modalidadCodigo"/> y <paramref name="amortizables"/> son obligatorios.
    /// Mandarlos en null «por convenience» dejaría el método capaz de borrar en silencio la
    /// modalidad y los activos, que es exactamente lo que pasó antes.</para>
    /// </summary>
    public async Task<ApiResponse<CotizacionListItem>?> ActualizarAsync(
        Guid id,
        string tipoServicioCodigo,
        DateOnly fecha,
        decimal margen,
        List<LineaInsumoRequest> insumos,
        List<LineaAmortizableRequest>? amortizables,
        string? modalidadCodigo,
        string? tecnicoCodigo = null,
        DateOnly? fechaEstimada = null,
        string? direccionServicio = null)
    {
        var request = new
        {
            TipoServicioCodigo = tipoServicioCodigo,
            Fecha = fecha,
            ModalidadCodigo = modalidadCodigo,
            TecnicoCodigo = tecnicoCodigo,
            FechaEstimada = fechaEstimada,
            DireccionServicio = direccionServicio,
            Margen = margen,
            Insumos = insumos.Select(i => new
            {
                i.ProductoCodigo,
                i.Cantidad,
                i.SemestreId
            }).ToList(),
            Amortizables = (amortizables ?? []).Select(a => new
            {
                a.ActivoCodigo,
                a.Cantidad,
                a.MesesVidaUtil
            }).ToList()
        };

        var response = await _http.PutAsJsonAsync($"api/cotizaciones/{id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<CotizacionListItem>>();
    }

    /// <summary>Descarga el PDF de una cotización como bytes, para guardarlo o enviarlo.</summary>
    public async Task<byte[]?> DescargarPdfAsync(Guid id)
    {
        var response = await _http.GetAsync($"api/cotizaciones/{id}/pdf");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadAsByteArrayAsync();
    }
}
