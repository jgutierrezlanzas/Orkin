using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.MotorCalculo;

namespace Orkin.Cotizador.Api.Middlewares;

/// <summary>
/// La última línea de defensa: convierte cualquier excepción que sube por la canalización en un
/// sobre de error, y se asegura de que al cliente no le llegue nada que no deba.
/// <para><b>Por qué un middleware y no try/catch en cada controlador:</b> veinte controladores con
/// veinte try/catch son veinte lugares donde se olvidó uno, y el que se olvidó devuelve una página en
/// blanco con un <c>500</c> sin código de error. Además hay excepciones que nacen fuera de los
/// controladores —en la resolución de dependencias, en la serialización de la respuesta— y a esas no
/// las alcanza ningún try/catch de controlador.</para>
/// <para><b>Por qué el detalle técnico no sale:</b> la pila, la consulta SQL y la forma de las tablas
/// se escriben en el log <b>con el identificador de petición</b>, y al cliente se le da ese
/// identificador. Un mensaje como <c>Violation of PRIMARY KEY constraint 'PK_x'. Cannot insert
/// duplicate key</c> le dice a cualquiera que sepa hacer una petición cómo se llama la tabla y cuál
/// es su clave; y un mensaje de excepción puede incluir el valor que alguien tecleó en un campo que
/// después se cae. Con el identificador, el que atiende el reporte encuentra la línea exacta del log
/// sin depender de la hora.</para>
/// <para><b>Por qué distingue los dos casos:</b> una excepción de dominio, de permisos o de
/// validación produce un mensaje accionable, porque el usuario puede hacer algo con él. Una
/// excepción inesperada produce un mensaje genérico. Confundir las dos —enseñar el mensaje técnico
/// de la segunda— hace que el usuario reporte "me salió un error de base" y nadie sepa qué hacer con
/// eso.</para>
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _siguiente;
    private readonly ILogger<GlobalExceptionMiddleware> _log;

    public GlobalExceptionMiddleware(RequestDelegate siguiente, ILogger<GlobalExceptionMiddleware> log)
    {
        _siguiente = siguiente;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _siguiente(contexto);
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            // El cliente se fue a mitad de la respuesta: cerró la pestaña, canceló una descarga, o
            // el proxy cortó la conexión. No es un error, y swallowed aquí es lo correcto — no hay a
            // quién avisarle ni nada que registrar. Dejarlo subir convertiría una cierre normal en
            // un 500 que nadie va a leer, y ensuciaría el log con miles de falsos errores cuando la
            // red de una oficina se cae.
            _log.LogDebug(
                "Petición cancelada por el cliente: {Metodo} {Ruta}",
                contexto.Request.Method,
                contexto.Request.Path);
        }
        catch (Exception ex)
        {
            await ResponderAsync(contexto, ex);
        }
    }

    private async Task ResponderAsync(HttpContext contexto, Exception ex)
    {
        var (respuesta, nivel) = Clasificar(ex, contexto.TraceIdentifier);

        if (nivel == LogLevel.Warning)
        {
            // Un error de dominio es parte del día a día: alguien escribió mal una fecha, o no tiene
            // permiso. Va como advertencia para que no se entierre entre los errores de servidor, y
            // con la pila completa, que en este caso sí sirve para corregir.
            _log.LogWarning(
                ex,
                "{Metodo} {Ruta} → {Codigo}. Peticion {Referencia}",
                contexto.Request.Method,
                contexto.Request.Path,
                respuesta.Codigo,
                contexto.TraceIdentifier);
        }
        else
        {
            _log.LogError(
                ex,
                "{Metodo} {Ruta} → error no controlado. Peticion {Referencia}",
                contexto.Request.Method,
                contexto.Request.Path,
                contexto.TraceIdentifier);
        }

        // Si la respuesta ya empezó a salir, no se puede cambiar ni el código ni el cuerpo. Aceptar
        // que quede incompleta es lo único sensato: escribir encima lanza InvalidOperationException
        // _dentro_ de este catch, y el error real queda escondido detrás de otro.
        if (contexto.Response.HasStarted)
        {
            _log.LogWarning(
                "La respuesta a {Ruta} ya había empezado; no se pudo enviar el sobre de error. " +
                "Peticion {Referencia}",
                contexto.Request.Path,
                contexto.TraceIdentifier);

            return;
        }

        contexto.Response.Clear();
        contexto.Response.StatusCode = respuesta.CodigoHttp();
        contexto.Response.ContentType = "application/json; charset=utf-8";

        await contexto.Response.WriteAsJsonAsync(
            respuesta, cancellationToken: contexto.RequestAborted);
    }

    /// <summary>
    /// Decide el sobre, y si el hecho se registra como advertencia —alguien tiene que
    /// corregirlo— o como error —hay que enterarse ya—.
    /// <para>El nivel se decide por el <b>código</b> y no por el tipo de excepción, porque lo que
    /// importa para el que atiende el log no es qué la lanzó, sino si es algo que va a volver a
    /// pasar sin que nadie lo toque. Una fecha mal escrita se repite mañana; una conexión caída
    /// también, y por eso el umbral entre los dos niveles es si esto lo va a volver a ver alguien
    /// sin que antes se haya corregido la causa.</para>
    /// </summary>
    private static (ApiResponse<Unit> Respuesta, LogLevel Nivel) Clasificar(Exception ex, string referencia)
    {
        var (codigo, mensaje) = ex switch
        {
            // ── El usuario puede hacer algo con esto ──────────────────────────────

            // Un cálculo sin datos suficientes, o con números que no cierran. El mensaje lo arma el
            // motor y dice qué campo hay que corregir.
            CalculoInvalidoException calculo => ("CALCULO_INVALIDO", calculo.Message),

            // Un código de catálogo que no existe o está inactivo. Casi siempre es un catálogo mal
            // cargado, y el mensaje trae la tabla y el código, que es todo lo que hace falta.
            CatalogoNoResueltoException catalogo => ("CATALOGO_NO_RESUELTO", catalogo.Message),

            // Autenticado pero sin permiso. No se dice qué permiso falta: le enseñaría la
            // estructura de permisos a alguien que no debería verla.
            UnauthorizedAccessException =>
                ("NO_AUTORIZADO", "No tiene permiso para hacer esta operación."),

            // Se pidió algo que no existe. Es 404 y no 400 porque la culpa no es de quien pidió.
            KeyNotFoundException =>
                ("NO_ENCONTRADO", "El registro solicitado no existe."),

            // Regla de negocio, o tipo que no coincide con el que se esperaba.
            ArgumentException argumento => ("DATO_INVALIDO", argumento.Message),

            FormatException => ("DATO_INVALIDO",
                "Alguno de los datos enviados no tiene el formato esperado."),

            // Dos personas editaron lo mismo a la vez. Este mensaje sí es accionable.
            DbUpdateConcurrencyException =>
                ("CONCURRENCIA",
                    "Alguien más modificó este registro mientras usted lo editaba. Vuelva a cargarlo " +
                    "y aplique los cambios de nuevo."),

            // ── El usuario no puede hacer nada, pero hay que avisarle ────────────

            // Un INSERT, un DELETE o una clave foránea. Lo que falló sí lo hizo el usuario, y la
            // causa más común es violar el CHECK de un catálogo, así que merece una respuesta con
            // forma de "hay datos que corregir" en vez de un 500 opaco. El detalle va al log.
            DbUpdateException actualizacion when EsViolacionDeRestriccion(actualizacion) =>
                ("DATO_INVALIDO", "Los datos no cumplen una regla del modelo de negocio."),

            DbUpdateException =>
                ("ERROR_DE_BASE",
                    "No se pudo guardar el registro. Si el problema persiste, avise al administrador."),

            TimeoutException =>
                ("TIEMPO_AGOTADO",
                    "La operación tardó demasiado y se canceló. Intente de nuevo en un momento."),

            _ => ("ERROR_INTERNO", "Ocurrió un error inesperado. Avise al administrador.")
        };

        var nivel = codigo switch
        {
            "VALIDACION" or "DATO_INVALIDO" or "NO_AUTORIZADO" or "NO_ENCONTRADO"
                or "CONCURRENCIA" => LogLevel.Warning,

            _ => LogLevel.Error
        };

        // La referencia va en todos los mensajes, incluso en los accionables. Es lo que permite que
        // quien reporte el problema encuentre la línea del log sin depender de la hora en que pasó,
        // que es la información que de verdad se pierde cuando alguien dice "falló en la mañana".
        return (ApiResponse<Unit>.Error(codigo, $"{mensaje} (referencia {referencia})"), nivel);
    }

    /// <summary>
    /// Distingue un error de datos —clave duplicada, clave foránea, CHECK— de un error de base de
    /// verdad. Los dos llegan como <see cref="DbUpdateException"/>, y al usuario le corresponden
    /// mensajes distintos: uno puede corregirlo, el otro tiene que esperar a que alguien investigue.
    /// <para>Se mira el número de error de SQL Server, que es el dato estable, y no el texto del
    /// mensaje: el texto cambia con el idioma del servidor y entre versiones.</para>
    /// </summary>
    private static bool EsViolacionDeRestriccion(DbUpdateException ex) => ex.InnerException switch
    {
        Microsoft.Data.SqlClient.SqlException sql => sql.Number is 2601 or 2627 or 547,
        _ => false
    };
}
