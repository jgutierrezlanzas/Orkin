using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Red de seguridad del transporte: convierte un fallo de red (CORS, API caída, DNS, tiempo de
/// espera) en una respuesta de error <b>de la plataforma</b>, para que la pantalla muestre el
/// motivo real en vez de morirse.
/// <para><b>Por qué hace falta:</b> cuando el navegador bloquea una llamada por CORS —porque la
/// página se sirvió desde un origen que la API no tiene en su lista— o cuando la API no responde,
/// <c>HttpClient</c> lanza <see cref="HttpRequestException"/> ("TypeError: Failed to fetch").
/// Esa excepción no la capturaba nadie: subía hasta Blazor y la app entera se quedaba en la
/// pantalla genérica de "Ha ocurrido un error inesperado. Recargar", sin decir qué pasó ni
/// dónde mirar. Corrió una vez desde un puerto distinto y el síntoma fue exactamente ese.</para>
/// <para><b>Por qué responde 200 y no 503:</b> el contrato de esta UI es <c>ApiResponse</c>, no
/// el código de estado: ningún servicio del cliente revisa el status, todos leen
/// <c>Exito</c>/<c>Mensaje</c>. Y <c>GetFromJsonAsync</c> —que usan todos los Listar— llama
/// <c>EnsureSuccessStatusCode</c> antes de deserializar: con un 503 sintético, la excepción
/// volvería a romper la pantalla, solo que más adentro. Con un 200 y un cuerpo
/// <c>ApiResponse</c> de error, cada llamada se comporta como si la API hubiera contestado
/// "no pude": los formularios muestran el mensaje en el snackbar y las listas quedan vacías
/// en vez de matar la página.</para>
/// </summary>
public sealed class FalloTransporteHandler : DelegatingHandler
{
    private readonly string _api;

    /// <param name="api">La dirección de la API, para que el mensaje diga a dónde no se llegó.</param>
    public FalloTransporteHandler(string api)
    {
        _api = api;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
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
