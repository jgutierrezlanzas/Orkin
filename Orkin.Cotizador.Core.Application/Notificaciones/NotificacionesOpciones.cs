namespace Orkin.Cotizador.Core.Application.Notificaciones;

/// <summary>
/// Configuración del servidor SMTP. Se enlaza desde <c>Correo__*</c> del <c>.env</c> y se valida al
/// arrancar: una clave mal copiada tiene que impedir el inicio, no descubrirse cuando el primer
/// correo queda en la cola.
/// <para><b>Habilitado=false por defecto.</b> Los correos se encolan igual, pero el proveedor los
/// rechaza con un mensaje explícito y quedan a la espera de reintento. Así un despliegue nuevo no
/// envía nada por accidente y, sobre todo, nadie confunde "no configured" con "enviado".</para>
/// </summary>
public sealed class CorreoOpciones
{
    public const string Seccion = "Correo";

    /// <summary>Encender el envío real. Requiere Servidor y De válidos.</summary>
    public bool Habilitado { get; set; }

    /// <summary>Servidor SMTP. Sin puerto embebido: va en <see cref="Puerto"/>.</summary>
    public string Servidor { get; set; } = string.Empty;

    public int Puerto { get; set; } = 587;

    /// <summary>
    /// TLS sobre el mismo puerto (STARTTLS en 587, SSL directo en 465). El servidor decide qué
    /// acepta; la opción solo fija lo que le pedimos.
    /// </summary>
    public bool UsarSsl { get; set; } = true;

    /// <summary>Usuario SMTP. Vacío significa autenticación anónima.</summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Clave SMTP. Nunca se escribe en logs ni se devuelve por la API.</summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>Remitente usado en el sobre y en el encabezado From.</summary>
    public string De { get; set; } = string.Empty;

    public string NombreDe { get; set; } = "ORKIN";

    public int TimeoutSegundos { get; set; } = 30;
}

/// <summary>
/// Configuración del job y de la bandeja de notificaciones. Se enlaza desde <c>Notificaciones__*</c>.
/// <para><b>HabilitadoJob=false por defecto:</b> encender el job es una decisión explícita de
/// despliegue. La API y los endpoints de consulta funcionan igual sin él, y el desarrollo no se
/// encuentra con correos enviados a destinatarios reales sin querer.</para>
/// </summary>
public sealed class NotificacionesOpciones
{
    public const string Seccion = "Notificaciones";

    public bool HabilitadoJob { get; set; }

    /// <summary>Cada cuánto despierta el job para recoger avisos y drenar la cola.</summary>
    public int IntervaloSegundos { get; set; } = 60;

    /// <summary>Máximo de correos por vuelta. Acota el daño de una cola con miles de filas.</summary>
    public int TamanoLote { get; set; } = 20;

    /// <summary>Intentos totales, incluido el primero.</summary>
    public int MaximoIntentos { get; set; } = 3;

    /// <summary>Base del backoff exponencial: intento 1 espera esto, el 2 el doble, etc.</summary>
    public int BackoffBaseSegundos { get; set; } = 60;

    /// <summary>
    /// Exigir <c>OBJECT_DEFINITION</c> y permiso de EXECUTE antes de invocar cada SP legacy.
    /// <b>No desactivar en producción</b>: es la barrera que impide ejecutar un SP que alguien
    /// cambió para escribir en el staging de visitas.
    /// </summary>
    public bool ValidarDefinicionesSp { get; set; } = true;

    /// <summary>
    /// Destinatario interno de los avisos de requisas erradas (SP sin columna de correo). Vacío
    /// desactiva ese aviso; no se inventa una dirección de relleno.
    /// </summary>
    public string CorreoInterno { get; set; } = string.Empty;

    /// <summary>Cantidad de avisos que devuelve la bandeja de la campana.</summary>
    public int TamanoBandeja { get; set; } = 50;
}
