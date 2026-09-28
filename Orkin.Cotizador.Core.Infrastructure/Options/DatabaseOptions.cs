namespace Orkin.Cotizador.Core.Infrastructure.Options;

/// <summary>
/// Configuración de la conexión a SQL Server (ADR-006). Se enlaza desde la sección
/// <c>Database</c> de <c>appsettings</c> y de las variables de entorno, que DotNetEnv inyecta antes.
/// <para>La cadena de conexión <b>no</b> está en un valor por defecto: si faltara, la conexión se
/// registra igual pero falla al primer uso, y ese error dice "no hay cadena de conexión" en lugar de
/// apuntar a una base que no es la del ambiente.</para>
/// </summary>
public sealed class DatabaseOptions
{
    public const string Seccion = "Database";

    /// <summary>Cadena de conexión. Viene de <c>ConnectionStrings__CotizadorDb</c> o de <c>.env</c>.</summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Espera máxima de un comando. Alto a propósito: una importación del ERP o un reporte de
    /// cotizaciones legue de minutos, y cortarlos produce errores confusos.
    /// </summary>
    public int CommandTimeoutSegundos { get; set; } = 180;

    /// <summary>
    /// Reintentos ante fallos transitorios. Cubre el arranque de la API mientras SQL Server
    /// todavía está levantándose en el contenedor de desarrollo.
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>
    /// Aplicar las migraciones pendientes al arrancar.
    /// <para><c>true</c> en desarrollo, para no tener que acordarse de correr
    /// <c>dotnet ef database update</c>. <c>false</c> en Staging y Producción, donde la migración
    /// la aplica el pipeline como paso explícito y separado: si el arranque las aplicara, un
    /// despliegue con un error de migración dejaría la aplicación caída sin poder ni diagnosticar
    /// ni revertir.</para>
    /// </summary>
    public bool AplicarMigracionesAlArrancar { get; set; } = true;

    /// <summary>
    /// Exponer los datos sensibles en los mensajes de error. <c>false</c> siempre fuera de
    /// desarrollo: un <c>SqlException</c> puede traer el texto de la consulta con valores.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; }
}
