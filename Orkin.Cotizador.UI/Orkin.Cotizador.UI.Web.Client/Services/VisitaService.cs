using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>Acceso a la agenda propia y a su importador a través de la API única (D-22).</summary>
public sealed class VisitaService
{
    private readonly HttpClient _http;

    public VisitaService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<VisitaModel>> ListarAsync(
        DateOnly? desde = null,
        DateOnly? hasta = null,
        string? texto = null,
        string? cliente = null,
        long? plantilla = null)
    {
        var parametros = new List<string>();
        if (desde is { } d)
        {
            parametros.Add($"desde={d:yyyy-MM-dd}");
        }

        if (hasta is { } h)
        {
            parametros.Add($"hasta={h:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            parametros.Add($"texto={Uri.EscapeDataString(texto.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(cliente))
        {
            parametros.Add($"cliente={Uri.EscapeDataString(cliente.Trim())}");
        }

        if (plantilla is { } p)
        {
            parametros.Add($"plantilla={p}");
        }

        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<VisitaModel>>>(
            "api/visitas?" + string.Join('&', parametros));
        return respuesta?.Datos ?? [];
    }

    public async Task<VisitaModel?> ObtenerAsync(long idVisita)
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<VisitaModel>>($"api/visitas/{idVisita}");
        return respuesta?.Datos;
    }

    public Task<ApiResponse<VisitaModel>?> GuardarAsync(VisitaGuardarModel modelo, bool esEdicion)
    {
        var contenido = JsonContent.Create(new
        {
            modelo.IdCalendario,
            modelo.Cliente,
            modelo.Locacion,
            modelo.TipoServicio,
            modelo.FechaInicio,
            modelo.HoraInicio,
            modelo.FechaFinal,
            modelo.HoraFinal,
            modelo.CodigoVehiculo,
            modelo.Nota,
            modelo.Plantilla,
            Tecnicos = modelo.Tecnicos.Select(t => new { t.CodigoTecnico, t.Iniciales }).ToList()
        });

        return esEdicion
            ? EnviarAsync<VisitaModel>(_http.PutAsync($"api/visitas/{modelo.IdVisita}", contenido))
            : EnviarAsync<VisitaModel>(_http.PostAsync("api/visitas", contenido));
    }

    public async Task<bool> ReversarAsync(long idVisita)
    {
        var respuesta = await _http.DeleteAsync($"api/visitas/{idVisita}");
        return respuesta.IsSuccessStatusCode;
    }

    public async Task<string?> GenerarOrdenAsync(long idVisita)
    {
        var respuesta = await _http.PostAsync($"api/visitas/{idVisita}/generar-orden", content: null);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<ApiResponse<GenerarOrdenModel>>();
        return cuerpo?.Datos?.OrdenNumero;
    }

    // ── Importador ──────────────────────────────────────────────────────────────

    public async Task<ImportacionGoogleResumenModel?> ImportarAsync(
        string nombre,
        IReadOnlyList<EventoGoogleModel> eventos)
    {
        var contenido = JsonContent.Create(new
        {
            Nombre = nombre,
            Origen = "Google Calendar",
            Eventos = eventos.Select(e => new
            {
                e.EventoId, e.CalendarId, e.Summary, e.Ubicacion, e.Descripcion,
                e.FechaInicio, e.FechaFin
            }).ToList()
        });
        var respuesta = await EnviarAsync<ImportacionGoogleResumenModel>(_http.PostAsync("api/visitas/importacion", contenido));
        return respuesta?.Datos;
    }

    public async Task<List<ImportacionGoogleEventoModel>> ListarEventosImportadosAsync(Guid? loteId = null)
    {
        var ruta = loteId is { } id
            ? $"api/visitas/importacion/eventos?loteId={id}"
            : "api/visitas/importacion/eventos";
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<ImportacionGoogleEventoModel>>>(ruta);
        return respuesta?.Datos ?? [];
    }

    public async Task<ImportacionGoogleEventoModel?> CorregirEventoAsync(
        Guid id,
        CorregirEventoGoogleModel modelo)
    {
        var contenido = JsonContent.Create(new
        {
            modelo.ClienteCodigo, modelo.LocacionCodigo, modelo.TipoServicioCodigo,
            modelo.VehiculoCodigo, modelo.TecnicosCodigos, modelo.Nota, modelo.Plantilla
        });
        var respuesta = await EnviarAsync<ImportacionGoogleEventoModel>(_http.PutAsync($"api/visitas/importacion/eventos/{id}", contenido));
        return respuesta?.Datos;
    }

    public async Task<bool> ConfirmarEventoAsync(Guid id)
    {
        var respuesta = await _http.PostAsync($"api/visitas/importacion/eventos/{id}/confirmar", content: null);
        return respuesta.IsSuccessStatusCode;
    }

    public async Task<bool> DescartarEventoAsync(Guid id)
    {
        var respuesta = await _http.DeleteAsync($"api/visitas/importacion/eventos/{id}");
        return respuesta.IsSuccessStatusCode;
    }

    public async Task<List<MapeoEventoGoogleModel>> ListarMapeosAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<MapeoEventoGoogleModel>>>(
            "api/visitas/importacion/homologacion");
        return respuesta?.Datos ?? [];
    }

    public async Task<MapeoEventoGoogleModel?> GuardarMapeoAsync(MapeoEventoGoogleModel modelo)
    {
        var contenido = JsonContent.Create(new
        {
            modelo.Campo, modelo.ValorOrigen, modelo.ValorDestino, modelo.Descripcion, modelo.Activo
        });
        var tarea = modelo.Id == Guid.Empty
            ? _http.PostAsync("api/visitas/importacion/homologacion", contenido)
            : _http.PutAsync($"api/visitas/importacion/homologacion/{modelo.Id}", contenido);
        var respuesta = await EnviarAsync<MapeoEventoGoogleModel>(tarea);
        return respuesta?.Datos;
    }

    public async Task<bool> EliminarMapeoAsync(Guid id)
    {
        var respuesta = await _http.DeleteAsync($"api/visitas/importacion/homologacion/{id}");
        return respuesta.IsSuccessStatusCode;
    }

    private static async Task<ApiResponse<T>?> EnviarAsync<T>(Task<HttpResponseMessage> tarea)
    {
        var respuesta = await tarea;
        if (!respuesta.IsSuccessStatusCode)
        {
            return null;
        }

        return await respuesta.Content.ReadFromJsonAsync<ApiResponse<T>>();
    }

    private sealed class GenerarOrdenModel
    {
        public long IdVisita { get; set; }
        public string? OrdenNumero { get; set; }
    }
}
