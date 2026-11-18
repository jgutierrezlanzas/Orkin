using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Acceso a la API única de órdenes de trabajo (Fase 4). La clave de cada fila es la PK legacy
/// <c>ORDEN_NUMERO + ID_VISITA + PLANTILLA</c>, expuesta por <see cref="OrdenModel.Ruta"/>.
/// </summary>
public sealed class OrdenService
{
    private readonly HttpClient _http;

    public OrdenService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<OrdenModel>> ListarAsync(
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

        var url = parametros.Count == 0 ? "api/ordenes" : "api/ordenes?" + string.Join('&', parametros);
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<OrdenModel>>>(url);
        return respuesta?.Datos ?? [];
    }

    public async Task<OrdenModel?> ObtenerAsync(string ordenNumero, long idVisita, long plantilla)
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<OrdenModel>>(
            $"api/ordenes/{Uri.EscapeDataString(ordenNumero)}/{idVisita}/{plantilla}");
        return respuesta?.Datos;
    }

    public async Task<OrdenModel> CrearDesdeVisitaAsync(long idVisita)
    {
        var respuesta = await LeerAsync<OrdenModel>(
            _http.PostAsync($"api/ordenes/desde-visita/{idVisita}", content: null));

        if (respuesta is null || !respuesta.Exito)
        {
            throw new InvalidOperationException(respuesta?.Mensaje ?? "No se pudo generar la orden desde la visita.");
        }

        return respuesta.Datos
            ?? throw new InvalidOperationException("La API no devolvió la orden generada.");
    }

    public async Task<OrdenModel> GuardarAsync(OrdenModel modelo)
    {
        var contenido = JsonContent.Create(new
        {
            modelo.Cliente,
            modelo.FechaOrden,
            modelo.Hora,
            modelo.Direccion,
            modelo.Contacto,
            modelo.Telefono,
            modelo.Vehiculo,
            modelo.ServicioRealizado,
            modelo.DescripTrabajo,
            modelo.Notas,
            modelo.Instrucciones,
            Detalles = modelo.Productos.Select(d => new
            {
                d.Consecutivo,
                d.CodigoArticulo,
                DescripcionArticulo = d.DescripcionArticulo,
                d.TipoPlaga,
                d.LugarTratar,
                d.Area,
                d.Concentracion,
                d.CodigoDosificacion,
                d.MetodoAplicacion,
                d.Cantidad,
                d.Umedida,
                d.CostoUnitario
            }).ToList(),
            Servicios = modelo.ServiciosDetalle.Select(s => new
            {
                s.Consecutivo,
                s.CodigoServicio,
                s.CodigoFrecuencia
            }).ToList(),
            Equipos = modelo.EquiposDetalle.Select(e => new
            {
                e.Consecutivo,
                e.CodigoEquipo,
                e.Utilizado
            }).ToList(),
            Tecnicos = modelo.TecnicosDetalle.Select(t => new
            {
                t.Consecutivo,
                t.CodigoTecnico,
                t.Iniciales
            }).ToList()
        });

        var respuesta = await LeerAsync<OrdenModel>(
            _http.PutAsync($"api/ordenes/{modelo.Ruta}", contenido));

        if (respuesta is null || !respuesta.Exito)
        {
            throw new InvalidOperationException(respuesta?.Mensaje ?? "No se pudo guardar la orden.");
        }

        return respuesta.Datos
            ?? throw new InvalidOperationException("La API no devolvió la orden guardada.");
    }

    /// <summary>Anular y generar requisa devuelven 204: no hay cuerpo que deserializar.</summary>
    public Task AnularAsync(OrdenModel orden)
        => EjecutarSinCuerpoAsync(
            _http.PostAsync($"api/ordenes/{orden.Ruta}/anular", content: null),
            "No se pudo anular la orden.");

    public Task GenerarRequisaAsync(OrdenModel orden)
        => EjecutarSinCuerpoAsync(
            _http.PostAsync($"api/ordenes/{orden.Ruta}/requisa", content: null),
            "No se pudo generar la requisa.");

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
