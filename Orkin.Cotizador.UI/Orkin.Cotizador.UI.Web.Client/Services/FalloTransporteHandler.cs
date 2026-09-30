using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Red de seguridad del transporte y de la sesión: convierte un fallo de red (CORS, API caída,
/// tiempo de espera) o un 401 de sesión expirada en una respuesta de error <b>de la plataforma</b>,
/// para que la pantalla muestre el motivo real en vez de morirse.
/// <para><b>Por qué hace falta:</b> cuando el navegador bloquea una llamada por CORS —porque la
/// página se sirvió desde un origen que la API no tiene en su lista— o cuando la API no responde,
/// <c>HttpClient</c> lanza <see cref="HttpRequestException"/>. Esa excepción no la capturaba nadie:
/// subía hasta Blazor y la app entera se quedaba en la pantalla genérica de "Ha ocurrido un error
/// inesperado. Recargar", sin decir qué pasó ni dónde mirar.</para>
/// <para><b>El 401 de sesión expirada:</b> cuando el token vence a las 8 horas, la API responde
/// 401 y <c>GetFromJsonAsync</c> lanza por el código de estado — la pantalla moría en el
/// ErrorBoundary en vez de hacer lo que el usuario espera: <b>volver al login</b>. Ahora el
/// handler detecta el 401, borra el token de los dos almacenes, y redirige al login con un
/// full-page reload — la sesión murió, y no hay nada que seguir haciendo con la pantalla que
/// quedó abierta.</para>
/// <para><b>Por qué responde 200 y no 503:</b> el contrato de esta UI es <c>ApiResponse</c>, no
/// el código de estado: ningún servicio del cliente revisa el status, todos leen
/// <c>Exito</c>/<c>Mensaje</c>. Y <c>GetFromJsonAsync</c> —que usan todos los Listar— llama
/// <c>EnsureSuccessStatusCode</c> antes de deserializar: con un no-2xx, la excepción volvería a
/// romper la pantalla, solo que más adentro. Con un 200 y un cuerpo <c>ApiResponse</c> de error,
/// cada llamada se comporta como si la API hubiera contestado "no pude": los formularios
/// muestran el mensaje en el snackbar y las listas quedan vacías en vez de matar la página.</para>
/// </summary>
public sealed class FalloTransporteHandler : DelegatingHandler
{
    private readonly string _api;
    private readonly IJSRuntime _js;

    /// <param name="api">La dirección de la API, para que el mensaje diga a dónde no se llegó.</param>
    /// <param name="js">Para limpiar el token y redirigir al login cuando la sesión expira.</param>
    public FalloTransporteHandler(string api, IJSRuntime js)
    {
        _api = api;
        _js = js;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return Error(request, MensajeConexion(ex.Message));
        }
        catch (TaskCanceledException)
        {
            // TaskCanceled sin cancelación pedida por el usuario = timeout del HttpClient.
            return Error(request,
                $"La API en {_api} no respondió a tiempo. " +
                "Puede estar muy cargada o deteniéndose a medias; revise su estado y vuelva a intentar.");
        }

        // ── Sesión expirada: limpiar y redirigir al ingreso ─────────────────────
        //
        // El token dura 8 horas (SeguridadOptions.HorasValidez). Cuando vence, la API
        // responde 401 en todo. La única respuesta correcta de la interfaz es mandar a
        // quien estaba trabajando de vuelta al login — no matar la pantalla con un
        // ErrorBoundary, y no dejarlo mirando una página con datos vacíos y sin saber
        // qué pasó. El full-page reload es a propósito: la sesión murió, y todo lo que
        // la pantalla tenía en memoria ya no sirve.
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "orkin_cotizador_token");
            await _js.InvokeVoidAsync("sessionStorage.removeItem", "orkin_cotizador_token");
            await _js.InvokeVoidAsync("eval", "location.href = '/login?expirada=1'");

            // La respuesta sintética solo existe para que el RequestAsJsonAsync que está
            // corriendo no lance antes de que el navegador procese la redirección.
            return Error(request, "Su sesión ha expirado. Redirigiendo al ingreso...");
        }

        // ── Otros errores HTTP de la API (500, 403, etc.) ─────────────────────────
        //
        // La API ya devuelve ApiResponse JSON para los errores que maneja (validación,
        // permisos), pero puede devolver otras cosas (500 del framework, 403 del
        // middleware). Convertir a 200 + ApiResponse evita que GetFromJsonAsync lance
        // por el código de estado y deja que la pantalla muestre el mensaje.
        if (!response.IsSuccessStatusCode)
        {
            var contenido = await response.Content.ReadAsStringAsync(cancellationToken);

            // Si la API ya envió un ApiResponse (los errores manejados), pasarlo tal cual.
            if (contenido.Contains("\"exito\""))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent(contenido, Encoding.UTF8, "application/json")
                };
            }

            // Si es otro tipo de respuesta (500 del framework, 403), sintetizar el mensaje.
            var codigo = ((int)response.StatusCode).ToString();
            return Error(request,
                $"La API respondió {codigo} ({response.StatusCode}). " +
                (string.IsNullOrWhiteSpace(contenido) ? "" : $"Detalle: {contenido.TrimStart('{').TrimEnd('}')}"));
        }

        return response;
    }

    private string MensajeConexion(string razonNavegador) =>
        $"No se pudo conectar con la API en {_api}. El navegador dijo: «{razonNavegador}». " +
        "Las dos causas típicas: que la API no esté corriendo, o que esta página se esté sirviendo " +
        "desde un origen que la API no tiene en su lista CORS (Cors__Origenes, en docker-compose " +
        "o appsettings). Cuando es CORS, la API sí responde pero el navegador descarta la respuesta: " +
        "por eso se ve igual que si no hubiera API.";

    /// <summary>
    /// La respuesta sintética: el mismo JSON que produce la API cuando algo falla, con un código
    /// propio ("TRANSPORTE") para que el código pueda distinguir "falló la API" de "nunca llegué
    /// a la API". Serializado como objeto y no a mano: escapar comillas y acentos a pico es el
    /// tipo de detalle que se rompe el día que el mensaje lleva un nombre con tilde.
    /// </summary>
    private static HttpResponseMessage Error(HttpRequestMessage request, string mensaje)
    {
        var cuerpo = System.Text.Json.JsonSerializer.Serialize(new
        {
            exito = false,
            mensaje,
            codigo = "TRANSPORTE",
            datos = (object?)null,
            errores = Array.Empty<object>()
        });

        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };
    }
}
