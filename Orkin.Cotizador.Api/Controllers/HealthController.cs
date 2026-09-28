using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// El estado del servicio. Es lo único que un orquestador de despliegues, un balanceador de carga
/// o un humano por consola puede preguntar sin necesitar credenciales.
/// <para><b>Por qué hay dos rutas y no una:</b> un proceso puede estar vivo y no poder trabajar, y
/// para un balanceador eso son dos decisiones opuestas. Para <em>reiniciarlo</em> hay que
/// conservarlo, porque matar un proceso no arregla una base caída y solo convierte un problema en
/// un bucle de reinicios. Para <em>mandarle tráfico</em> hay que sacarlo de rotación, porque
/// seguir enviándole peticiones a algo que no puede responder solo genera errores de más. Por eso
/// <c>/health</c> responde siempre que el proceso atienda peticiones, y <c>/health/ready</c>
/// devuelve 503 cuando la base no contesta.</para>
/// <para><b>Por qué el detalle va en el cuerpo y no en la cadena de la respuesta:</b> el código de
/// salud de un orquestador es un entero, no un texto. Poner "no conecta a la base" en la razón
/// textual haría que un <c>grep</c> en el script de despliegue decidiera si hay que reiniciar, y
/// eso se rompe en silencio la primera vez que cambia una palabra.</para>
/// <para><b>Por qué no pide autenticación:</b> una sonda que necesita token deja de diagnosticar
/// justo cuando el problema es que el token no se puede emitir. Lo que sí se exige es que la ruta
/// no devuelva nada más que estado: por eso no hay una sonda que liste versiones de paquetes,
/// conexiones abiertas ni configuración.</para>
/// </summary>
[ApiController]
[Route("health")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    private readonly CotizadorDbContext _contexto;
    private readonly ILogger<HealthController> _log;

    public HealthController(CotizadorDbContext contexto, ILogger<HealthController> log)
    {
        _contexto = contexto;
        _log = log;
    }

    /// <summary>
    /// El proceso está vivo. No consulta nada, y por eso responde aunque la base esté caída.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IResult Vivo() => Results.Json(
        ApiResponse<EstadoSalud>.Ok(new EstadoSalud(
            Estado: "VIVO",
            Version: AssemblyInformacion.Version(),
            Entorno: EntornoActual(),
            Uptime: Environment.TickCount64 / 1000)));

    /// <summary>
    /// El proceso puede trabajar. Abre la conexión a la base, la cierra y devuelve 503 si no
    /// puede, para que el proceso salga de rotación sin que nadie lo mate.
    /// <para>La consulta es <c>SELECT 1</c> y no un conteo de filas ni una lectura de la última
    /// cotización: lo que se quiere saber es si hay alguien del otro lado del cable, y una
    /// consulta pesada convertiría la sonda en carga para la base justo cuando la base está
    /// dolida.</para>
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IResult> ListoAsync(CancellationToken cancellationToken)
    {
        try
        {
            // CanConnectAsync abre la conexión y la cierra. Medir cuánto tardó sirve para ver la
            // latencia desde adentro, que es el número que importa cuando alguien dice "está
            // lento" y el tiempo que ve en el navegador no cuadra con lo que mide la red.
            var inicio = Stopwatch.GetTimestamp();
            var conecta = await _contexto.Database.CanConnectAsync(cancellationToken);
            var milisegundos = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;

            if (!conecta)
            {
                _log.LogError("La sonda de disponibilidad no pudo conectar a la base de datos.");

                return SinBase();
            }

            return Results.Json(
                ApiResponse<EstadoSalud>.Ok(new EstadoSalud(
                    Estado: "LISTO",
                    Version: AssemblyInformacion.Version(),
                    Entorno: EntornoActual(),
                    Uptime: Environment.TickCount64 / 1000,
                    BaseLatenciaMs: Math.Round(milisegundos, 1))));
        }
        catch (Exception ex)
        {
            // La sonda no deja subir la excepción al middleware global a propósito: un 503 sin
            // sobre es exactamente lo que espera un orquestador. Si dejara subir, el middleware la
            // convertiría en un 500, y un 500 en una sonda se lee como "el proceso está roto" en vez
            // de "la base no contesta", que llevan a arregles distintos.
            _log.LogError(ex, "Falló la sonda de disponibilidad.");

            return SinBase();
        }
    }

    private static IResult SinBase() => Results.Json(
        ApiResponse<EstadoSalud>.Error(
            "SIN_BASE",
            "El servicio no puede alcanzar la base de datos."),
        statusCode: StatusCodes.Status503ServiceUnavailable);

    /// <summary>
    /// El nombre del entorno. Se lee de la variable de ambiente y no de <c>IHostEnvironment</c> para
    /// que la sonda no dependa de que la canalización se haya armado, que es justamente lo que
    /// se quiere comprobar.
    /// </summary>
    private static string EntornoActual() =>
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? "Sin definir";
}

/// <summary>
/// Lo que dice la sonda. Deliberadamente corto: estado, versión, entorno y, en la de
/// disponibilidad, cuánto tardó la base.
/// <para><b>Por qué no más:</b> una sonda de salud es pública y su respuesta termina en un tablero
/// de monitoreo que a veces es de pago y a veces no. Cada dato que se agrega es un dato que sale
/// de la red de la empresa sin control, y ninguno de los que faltarían ayuda a nadie.
/// Sin identificadores de conexión, sin nombres de servidor, sin configuración.</para>
/// </summary>
/// <param name="Estado">VIVO o LISTO. Distinguir los dos permite ver, en el tablero, si lo que falló
/// es el proceso o es la base: el arreglo es distinto en cada caso.</param>
/// <param name="Version">Versión de la API, para confirmar que se está hablando con la correcta.</param>
/// <param name="Entorno">Development, Staging o Production.</param>
/// <param name="Uptime">Segundos desde que arrancó el proceso.</param>
/// <param name="BaseLatenciaMs">Cuánto tardó la base en responder, si esta sonda la consultó.</param>
public sealed record EstadoSalud(
    string Estado,
    string Version,
    string Entorno,
    long Uptime,
    double? BaseLatenciaMs = null);

/// <summary>
/// Saca la versión del ensamblado.
/// <para>Usa <c>InformationalVersion</c> y no <c>AssemblyVersion</c> porque la segunda la reescribe
/// el compilador a <c>1.0.0.0</c> en cada compilación: sirve para comparar archivos, no para saber
/// qué se desplegó. Y le quita el sufijo que agrega el SDK al final —algo como
/// <c>1.0.0+abc123def456</c>— que en el tablero parece un error de formato y nadie sabe si es
/// información o ruido.</para>
/// </summary>
internal static class AssemblyInformacion
{
    public static string Version()
    {
        var informativa = typeof(AssemblyInformacion).Assembly
            .GetCustomAttributes(
                typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informativa))
        {
            return "0.0.0-sin-version-informativa";
        }

        var separador = informativa.IndexOf('+', StringComparison.Ordinal);

        return separador > 0 ? informativa[..separador] : informativa;
    }
}
