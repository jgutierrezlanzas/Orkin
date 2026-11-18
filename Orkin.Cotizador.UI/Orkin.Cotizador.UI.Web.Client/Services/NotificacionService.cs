using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Orkin.Cotizador.UI.Shared.Notificaciones;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Cliente de la API única de notificaciones (Fase 8). La web no conoce los SPs legacy ni el
/// servidor SMTP: pide la bandeja y, si tiene CONFIGURAR, dispara recogida/envío/reenvío.
/// <para>Un <c>ApiResponse.Exito=false</c> se convierte en excepción con el mensaje de la API,
/// exactamente como en los demás servicios: el <c>RAISERROR</c> o el mensaje de validación se
/// muestra tal cual, no se sustituye por un "error inesperado".</para>
/// </summary>
public sealed class NotificacionService
{
    private const string Base = "api/notificaciones";
    private readonly HttpClient _http;

    public NotificacionService(HttpClient http) => _http = http;

    public async Task<BandejaNotificacionesVista> ObtenerBandejaAsync()
    {
        var respuesta = await LeerAsync<BandejaNotificacionesVista>(
            await _http.GetAsync($"{Base}/bandeja"));
        return respuesta.Datos ?? new BandejaNotificacionesVista();
    }

    public async Task<NotificacionConfiguracionVista?> ObtenerConfiguracionAsync()
    {
        var respuesta = await LeerAsync<NotificacionConfiguracionVista>(
            await _http.GetAsync($"{Base}/configuracion"));
        return respuesta.Datos;
    }

    public async Task<ResultadoColaVista> RecogerAsync()
    {
        var respuesta = await LeerAsync<ResultadoColaVista>(
            await _http.PostAsync($"{Base}/recoger", null));
        return respuesta.Datos ?? new ResultadoColaVista();
    }

    public async Task<ResultadoColaVista> ProcesarAsync()
    {
        var respuesta = await LeerAsync<ResultadoColaVista>(
            await _http.PostAsync($"{Base}/procesar", null));
        return respuesta.Datos ?? new ResultadoColaVista();
    }

    public async Task MarcarLeidaAsync(Guid id) =>
        await LeerAsync<object>(await _http.PostAsync($"{Base}/{id}/leida", null));

    public async Task<int> MarcarTodasLeidasAsync()
    {
        var respuesta = await LeerAsync<Marcadas>(await _http.PostAsync($"{Base}/leidas-todas", null));
        return respuesta.Datos?.Cantidad ?? 0;
    }

    public async Task ReenviarAsync(Guid id) =>
        await LeerAsync<object>(await _http.PostAsync($"{Base}/{id}/reenviar", null));

    private static async Task<ApiResponse<T>> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<ApiResponse<T>>();

        if (cuerpo is null)
        {
            throw new InvalidOperationException("La API de notificaciones no devolvió una respuesta válida.");
        }

        if (!cuerpo.Exito)
        {
            // FalloTransporteHandler convierte errores de red y 401 en ApiResponse con Exito=false;
            // acá se respeta el mensaje que traiga, sin inventar otro.
            throw new InvalidOperationException(
                cuerpo.Mensaje ?? cuerpo.Codigo ?? "No se pudo completar la operación.");
        }

        return cuerpo;
    }

    private sealed class Marcadas
    {
        [JsonPropertyName("marcadas")]
        public int Cantidad { get; set; }
    }
}
