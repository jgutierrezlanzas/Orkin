using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

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
            modelo.Servidor,
            modelo.Puerto,
            modelo.NombreBaseDatos,
            modelo.Esquema,
            modelo.Usuario,
            Contrasena = contrasena,
            modelo.Descripcion,
            modelo.TiempoEsperaSegundos
        };
        var response = await _http.PostAsJsonAsync("api/origenes/conexiones", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ErpConexionModel>>();
    }

    public async Task<ApiResponse<ErpConexionModel>?> ActualizarConexionAsync(ErpConexionModel modelo, string? contrasena)
    {
        var request = new
        {
            modelo.Nombre,
            modelo.Servidor,
            modelo.Puerto,
            modelo.NombreBaseDatos,
            modelo.Esquema,
            modelo.Usuario,
            Contrasena = contrasena,
            modelo.Descripcion,
            modelo.TiempoEsperaSegundos
        };
        var response = await _http.PutAsJsonAsync($"api/origenes/conexiones/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ErpConexionModel>>();
    }

    public async Task<ApiResponse<ProbarConexionModel>?> ProbarConexionAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/origenes/conexiones/{id}/probar", null);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ProbarConexionModel>>();
    }

    // ── Flujos ────────────────────────────────────────────────────────────────

    public async Task<string[]> ObtenerCatalogosDisponiblesAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<string[]>>("api/origenes/catalogos");
        return respuesta?.Datos ?? ["PRODUCTOS"];
    }

    /// <summary>
    /// Los campos que un catálogo espera del origen — para la tabla de homologación.
    /// Se piden a la API en vez de venir en una constante: agregar un cargador
    /// hace aparecer sus campos automáticamente.
    /// </summary>
    public async Task<List<CampoEsperadoModel>> ObtenerCamposEsperadosAsync(string catalogo)
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<CampoEsperadoModel>>>(
            $"api/origenes/catalogos/{catalogo}/campos");
        return respuesta?.Datos ?? [];
    }

    /// <summary>
    /// Las columnas que trae la vista del ERP, para llenar la homologación con los nombres de
    /// verdad y no con lo que se suppose que se llaman. Abre conexión al cliente: la pide quien
    /// está configurando, nunca sola por haber escrito el origen.
    /// </summary>
    public async Task<ApiResponse<List<ColumnaOrigenModel>>?> ObtenerColumnasOrigenAsync(
        Guid conexionId, string origen)
    {
        var response = await _http.PostAsJsonAsync("api/origenes/flujos/columnas", new
        {
            ErpConexionId = conexionId,
            Origen = origen
        });
        return await response.Content.ReadFromJsonAsync<ApiResponse<List<ColumnaOrigenModel>>>();
    }

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
            modelo.Nombre,
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

    public async Task<ApiResponse<ResultadoCargaModel>?> EjecutarFlujoAsync(Guid flujoId)
    {
        var response = await _http.PostAsync($"api/origenes/flujos/{flujoId}/ejecutar", null);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ResultadoCargaModel>>();
    }

    public async Task<ApiResponse<SimulacionFlujoModel>?> SimularFlujoAsync(Guid flujoId)
    {
        var response = await _http.PostAsync($"api/origenes/flujos/{flujoId}/simular", null);
        return await response.Content.ReadFromJsonAsync<ApiResponse<SimulacionFlujoModel>>();
    }

    public async Task<ApiResponse<ErpFlujoModel>?> ConfigurarProgramacionAsync(
        Guid flujoId, string tipoDisparador, int? intervaloSegundos,
        int? maximoRegistrosPorCorrida, short? maximoFallosConsecutivos)
    {
        var request = new
        {
            TipoDisparador = tipoDisparador,
            IntervaloSegundos = intervaloSegundos,
            MaximoRegistrosPorCorrida = maximoRegistrosPorCorrida,
            MaximoFallosConsecutivos = maximoFallosConsecutivos
        };
        var response = await _http.PutAsync($"api/origenes/flujos/{flujoId}/programacion",
            JsonContent.Create(request));
        return await response.Content.ReadFromJsonAsync<ApiResponse<ErpFlujoModel>>();
    }

    public async Task<ApiResponse<ErpFlujoModel>?> PausarFlujoAsync(Guid flujoId, string? motivo = null)
    {
        var request = new { Motivo = motivo };
        var response = await _http.PostAsJsonAsync($"api/origenes/flujos/{flujoId}/pausar", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ErpFlujoModel>>();
    }

    public async Task<ApiResponse<ErpFlujoModel>?> ReanudarFlujoAsync(Guid flujoId)
    {
        var response = await _http.PostAsync($"api/origenes/flujos/{flujoId}/reanudar", null);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ErpFlujoModel>>();
    }
}
