using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Acceso a los endpoints de orígenes de datos (<c>api/origenes</c>): las conexiones al ERP de
/// los clientes y los flujos que llenan los catálogos desde ellas.
/// <para>El servicio devuelve la <see cref="ApiResponse{T}"/> completa en los métodos que
/// escriben, y solo los datos en los que solo leen. La diferencia es a propósito: al listar no
/// hay nada que avisar (una lista vacía es una lista vacía), pero al guardar sí, y el código del
/// error ("VALIDACION", "DATO_INVALIDO", "NO_ENCONTRADO") dice más que un "no se pudo".
/// </para>
/// </summary>
public sealed class IntegracionService
{
    private readonly HttpClient _http;

    public IntegracionService(HttpClient http)
    {
        _http = http;
    }

    // ── Conexiones ───────────────────────────────────────────────────────────

    public async Task<List<ErpConexionModel>> ListarConexionesAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<ErpConexionModel>>>("api/origenes/conexiones");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<ErpConexionModel>?> CrearConexionAsync(ErpConexionModel modelo, string contrasena)
    {
        var request = new
        {
            modelo.Nombre,
            modelo.Descripcion,
            modelo.Servidor,
            modelo.Puerto,
            modelo.NombreBaseDatos,
            modelo.Esquema,
            modelo.Usuario,
            Contrasena = contrasena,
            modelo.TiempoEsperaSegundos
        };

        var response = await _http.PostAsJsonAsync("api/origenes/conexiones", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ErpConexionModel>>();
    }

    /// <summary>
    /// Actualiza la conexión. <paramref name="contrasena"/> en <c>null</c> significa "no la
    /// cambiaron, deja la que está": mandarla vacía la borraría, que es un error distinto al que
    /// la persona quiso pedir. Por eso la contraseña no se manda nunca implícitamente, sino solo
    /// cuando el formulario la tiene a la vista porque alguien la escribió.
    /// </summary>
    public async Task<ApiResponse<ErpConexionModel>?> ActualizarConexionAsync(ErpConexionModel modelo, string? contrasena)
    {
        var request = new
        {
            modelo.Descripcion,
            modelo.Servidor,
            modelo.Puerto,
            modelo.NombreBaseDatos,
            modelo.Esquema,
            modelo.Usuario,
            Contrasena = contrasena,
            modelo.TiempoEsperaSegundos
        };

        var response = await _http.PutAsJsonAsync($"api/origenes/conexiones/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ErpConexionModel>>();
    }

    /// <summary>
    /// Abre la conexión. La prueba fallida llega como HTTP 400 con el motivo en el mensaje, así
    /// que además del <c>Exito</c> se lee <c>Mensaje</c> y <c>Codigo</c>.
    /// </summary>
    public async Task<ApiResponse<ProbarConexionModel>?> ProbarConexionAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/origenes/conexiones/{id}/probar", null);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ProbarConexionModel>>();
    }

    // ── Flujos ───────────────────────────────────────────────────────────────

    public async Task<List<ErpFlujoModel>> ListarFlujosAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<ErpFlujoModel>>>("api/origenes/flujos");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<ErpFlujoModel>?> CrearFlujoAsync(ErpFlujoModel modelo)
    {
        var request = new
        {
            modelo.ErpConexionId,
            modelo.Nombre,
            modelo.Descripcion,
            modelo.Catalogo,
            modelo.Origen,
            modelo.MapeoJson
        };

        var response = await _http.PostAsJsonAsync("api/origenes/flujos", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ErpFlujoModel>>();
    }

    public async Task<ApiResponse<ErpFlujoModel>?> ActualizarFlujoAsync(ErpFlujoModel modelo)
    {
        var request = new
        {
            modelo.Descripcion,
            modelo.Catalogo,
            modelo.Origen,
            modelo.MapeoJson
        };

        var response = await _http.PutAsJsonAsync($"api/origenes/flujos/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ErpFlujoModel>>();
    }

    public async Task<ApiResponse<object>?> DarDeBajaFlujoAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/origenes/flujos/{id}");
        return await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
    }

    public async Task<List<SincronizacionEjecucionModel>> ListarEjecucionesAsync(Guid flujoId, int top = 20)
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<SincronizacionEjecucionModel>>>(
            $"api/origenes/flujos/{flujoId}/ejecuciones?top={top}");

        return respuesta?.Datos ?? [];
    }

    /// <summary>
    /// Corre el flujo ahora. No se encola ni se devuelve un id de trabajo: una sincronización de
    /// un catálogo de un ERP de un cliente termina en segundos o en un par de minutos, y esperar
    /// con la pantalla quieta a que termine es más honesto que mostrar un "en cola" que nadie
    /// va a ver avanzar.
    /// </summary>
    public async Task<ApiResponse<ResultadoCargaModel>?> EjecutarFlujoAsync(Guid flujoId)
    {
        var response = await _http.PostAsync($"api/origenes/flujos/{flujoId}/ejecutar", null);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ResultadoCargaModel>>();
    }
}
