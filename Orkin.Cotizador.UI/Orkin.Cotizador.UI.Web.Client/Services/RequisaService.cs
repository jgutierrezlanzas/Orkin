using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Acceso a la API única de requisas (Fase 5). La clave de cada fila es la PK legacy
/// <c>REQUISA_NUMERO</c>, expuesta por <see cref="RequisaModel.Ruta"/>.
/// </summary>
public sealed class RequisaService
{
    private readonly HttpClient _http;

    public RequisaService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<RequisaModel>> ListarAsync(
        string? texto = null,
        string? cliente = null,
        string? estado = null,
        DateOnly? desde = null,
        DateOnly? hasta = null)
    {
        var parametros = new List<string>();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            parametros.Add($"texto={Uri.EscapeDataString(texto.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(cliente))
        {
            parametros.Add($"cliente={Uri.EscapeDataString(cliente.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            parametros.Add($"estado={Uri.EscapeDataString(estado.Trim())}");
        }

        if (desde is { } d)
        {
            parametros.Add($"desde={d:yyyy-MM-dd}");
        }

        if (hasta is { } h)
        {
            parametros.Add($"hasta={h:yyyy-MM-dd}");
        }

        var url = parametros.Count == 0 ? "api/requisas" : "api/requisas?" + string.Join('&', parametros);
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<RequisaModel>>>(url);
        return respuesta?.Datos ?? [];
    }

    public async Task<RequisaModel?> ObtenerAsync(string numero)
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<RequisaModel>>(
            $"api/requisas/{Uri.EscapeDataString(numero)}");
        return respuesta?.Datos;
    }

    /// <summary>Reutiliza el endpoint que delega en el flujo de orden/SINCO_GENERA_REQUISA.</summary>
    public async Task<RequisaModel> CrearDesdeOrdenAsync(OrdenModel orden)
    {
        var respuesta = await LeerAsync<RequisaModel>(
            _http.PostAsync($"api/requisas/desde-orden/{orden.Ruta}", content: null));

        if (respuesta is null || !respuesta.Exito)
        {
            throw new InvalidOperationException(respuesta?.Mensaje ?? "No se pudo generar la requisa.");
        }

        return respuesta.Datos
            ?? throw new InvalidOperationException("La API no devolvió la requisa generada.");
    }

    public async Task<RequisaModel> GuardarAsync(RequisaModel modelo)
    {
        var contenido = JsonContent.Create(new
        {
            modelo.Cliente,
            modelo.FechaRequisa,
            modelo.FechaOrden,
            modelo.Locacion,
            modelo.Instrucciones,
            modelo.Bodega,
            Lineas = modelo.LineasDetalle.Select(d => new
            {
                d.Consecutivo,
                d.CodigoArticulo,
                DescripcionArticulo = d.DescripcionArticulo,
                d.CantidadEntregada,
                d.CantidadUtilizada,
                d.CantidadDevuelta,
                d.CostoUnitario,
                d.CentroCosto,
                d.CuentaContable
            }).ToList(),
            Tecnicos = modelo.TecnicosDetalle.Select(t => new
            {
                t.Consecutivo,
                t.CodigoTecnico,
                t.Iniciales
            }).ToList()
        });

        var respuesta = await LeerAsync<RequisaModel>(
            _http.PutAsync($"api/requisas/{modelo.Ruta}", contenido));

        if (respuesta is null || !respuesta.Exito)
        {
            throw new InvalidOperationException(respuesta?.Mensaje ?? "No se pudo guardar la requisa.");
        }

        return respuesta.Datos
            ?? throw new InvalidOperationException("La API no devolvió la requisa guardada.");
    }

    /// <summary>Anular devuelve 204: no hay cuerpo que deserializar.</summary>
    public Task AnularAsync(RequisaModel requisa)
        => EjecutarSinCuerpoAsync(
            _http.PostAsync($"api/requisas/{requisa.Ruta}/anular", content: null),
            "No se pudo anular la requisa.");

    private static async Task<ApiResponse<T>?> LeerAsync<T>(Task<HttpResponseMessage> tarea)
    {
        var respuesta = await tarea;
        return await respuesta.Content.ReadFromJsonAsync<ApiResponse<T>>();
    }

    private static async Task EjecutarSinCuerpoAsync(Task<HttpResponseMessage> tarea, string mensajePorDefecto)
    {
        var respuesta = await tarea;
        if (respuesta.IsSuccessStatusCode)
        {
            return;
        }

        var error = await respuesta.Content.ReadFromJsonAsync<ApiResponse<object>>();
        throw new InvalidOperationException(error?.Mensaje ?? mensajePorDefecto);
    }
}
