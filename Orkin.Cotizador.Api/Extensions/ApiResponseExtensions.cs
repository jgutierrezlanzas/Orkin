using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Extensions;

/// <summary>
/// Traduce el sobre de la API a una respuesta de HTTP.
/// <para><b>Por qué el número HTTP se decide en un solo lugar:</b> si cada controlador eligiera el
/// suyo, tarde o temprano uno devolvería <c>200</c> con un error adentro, o <c>500</c> por una
/// validación. Eso no se ve en la máquina que lo probó —todo le da igual— y lo descubre un usuario
/// mirando un mensaje que no cuadra. Con un solo <c>switch</c>, el código HTTP y
/// <c>ApiResponse.Exito</c> no pueden contradecirse porque salen del mismo dato.</para>
/// <para><b>Por qué un código de error desconocido es <c>500</c>:</b> sería
/// <c>400 Bad Request</c> y nadie iría a mirar el log, pensando que el usuario mandó algo malo. Un
/// código que nadie classificó es, por definición, un fallo del servidor.</para>
/// </summary>
public static class ApiResponseExtensions
{
    /// <summary>
    /// Convierte cualquier sobre —de éxito o de error— en un <see cref="IResult"/>. El
    /// <c>200</c> y el cuerpo salen de los mismos campos, así que no pueden discrepar.
    /// </summary>
    public static IResult AHttp<T>(this ApiResponse<T> respuesta) =>
        Results.Json(respuesta, statusCode: CodigoHttp(respuesta), options: Opciones);

    /// <summary>
    /// Igual que <see cref="AHttp{T}"/>, pero devuelve <c>204 No Content</c> cuando no hay nada que
    /// enviar. Para <c>DELETE</c> y para <c>PUT</c> que no devuelven cuerpo: un <c>200</c> con
    /// <c>null</c> obliga al cliente a deserializar algo que no existe.
    /// </summary>
    public static IResult AHttpSinCuerpo<T>(this ApiResponse<T> respuesta)
    {
        if (respuesta.Exito && respuesta.Datos is null)
        {
            return Results.NoContent();
        }

        return respuesta.AHttp();
    }

    /// <summary>
    /// El código HTTP que corresponde a este sobre, derivado de su <c>Codigo</c>.
    /// <para>Es un método de extensión y no un método suelto para que el middleware pueda escribir
    /// <c>respuesta.CodigoHttp()</c> y no tener que acordarse de la clase. Y <c>this</c> es
    /// obligatorio: un método estático dentro de una clase estática no se encuentra solo por estar
    /// ahí, y el error que da el compilador —"no contiene una definición"— no dice nada de que
    /// falte una palabra.</para>
    /// </summary>
    public static int CodigoHttp<T>(this ApiResponse<T> respuesta) => respuesta.Exito
        ? StatusCodes.Status200OK
        : respuesta.Codigo switch
        {
            // ── 400 · Lo que se mandó está mal, y quien lo mandó lo puede arreglar ───────────────

            "VALIDACION" => StatusCodes.Status400BadRequest,
            "DATO_INVALIDO" => StatusCodes.Status400BadRequest,

            // ── 401 · No hay sesión, o la que hay no sirve ───────────────────────────────────────
            // "NO_AUTENTICADO" y "NO_AUTORIZADO" no son sinónimos, aunque en español se confundan
            // todo el tiempo: no autenticado es "no sé quién eres" y no autorizado es "sé quién eres
            // y no te toca". El primero deja que el cliente vuelva a pedir un token; el segundo no
            // sirve de nada pedirlo otra vez. Mezclarlos hace que la interfaz se quede en un bucle
            // de reintentos que nunca va a pasar, o queryendo un token nuevo por un permiso que no
            // tiene.

            "NO_AUTENTICADO" => StatusCodes.Status401Unauthorized,
            "CREDENCIALES_INVALIDAS" => StatusCodes.Status401Unauthorized,
            "CREDENCIAL_VENCIDA" => StatusCodes.Status401Unauthorized,

            // ── 403 · Autenticado y sin permiso ──────────────────────────────────────────────────

            "NO_AUTORIZADO" => StatusCodes.Status403Forbidden,

            // ── 404 · No existe, y no es culpa de quien lo buscó ────────────────────────────────

            "NO_ENCONTRADO" => StatusCodes.Status404NotFound,

            // ── 409 · El estado del registro no permite lo que se pidió ───────────────────────────

            "CONCURRENCIA" => StatusCodes.Status409Conflict,

            // ── 422 · Se entiende y está bien formado, pero no se puede calcular ─────────────────

            "CALCULO_INVALIDO" => StatusCodes.Status422UnprocessableEntity,

            // ── 423 · Bloqueado, y se resuelve esperando ─────────────────────────────────────────
            // 423 y no 403 porque la diferencia le sirve al cliente: 403 dice "no va a pasar nunca",
            // y una cuenta bloqueada por intentos fallidos se destraba sola. Con 423 la interfaz
            // puede desactivar el botón de entrar y mostrar una cuenta regresiva, que es lo que la
            // persona necesita ver. Con 403 probablemente le pediría la contraseña otra vez, y cada
            // intento cuenta como fallido y alarga el bloqueo.

            "CUENTA_BLOQUEADA" => StatusCodes.Status423Locked,

            // ── 501 · La propia aplicación no lo tiene implementado todavía ──────────────────────

            "SEGUNDO_FACTOR_NO_IMPLEMENTADO" => StatusCodes.Status501NotImplemented,

            // ── 500 · Cualquier otro código es un fallo del servidor ─────────────────────────────
            // "CATALOGO_NO_RESUELTO" cae aquí a propósito, y no es una omisión: un código de
            // catálogo que el código nombra y la base no tiene es un despliegue incompleto, no algo
            // que la persona que está frente a la pantalla pueda arreglar. Va como 500 para que se
            // mire el log, que es donde está la explicación.

            _ => StatusCodes.Status500InternalServerError
        };

    /// <summary>
    /// Serialización en <c>camelCase</c> sin cambiar los nombres a <c>PascalCase</c> detrás: la base
    /// y la API hablan <c>PascalCase</c> y el navegador habla <c>camelCase</c>. Traducir en los dos
    /// sentidos es lo que evita poner <c>JsonPropertyName</c> en cada DTO.
    /// <para>Los <c>null</c> <b>se</b> escriben. Un <c>null</c> en la respuesta es información —"este
    /// campo no tiene valor"—, y omitirlo haría que el cliente no pudiera distinguir "vacío" de "el
    /// servidor no mandó este campo".</para>
    /// </summary>
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);
}
