namespace Orkin.Cotizador.Core.Infrastructure.Options;

/// <summary>
/// Control de la siembra de datos iniciales. Se enlaza de la sección <c>Seed</c> y, sobre todo, de
/// la variable de ambiente <c>RUN_SEED</c> (D-19, §5.20).
/// <para>La regla es simple y deliberada: en desarrollo <c>true</c>, en Staging y Producción
/// <c>false</c>. En esos ambientes la base se carga con un respaldo restaurado, nunca con una
/// siembra; correrla sobre datos reales es exactamente el riesgo que el <c>EsSembrado</c> de cada
/// catálogo intenta mitigar, pero la primera línea de defensa es no correrla.</para>
/// <para>Cuando está en <c>true</c>, la siembra es <b>idempotente</b> y se puede correr tantas veces
/// como haga falta: inserta lo que falta y no toca lo que ya está, ni siquiera si el JSON cambió.
/// Ver <c>DbInitializer</c>.</para>
/// </summary>
public sealed class SemillaOptions
{
    public const string Seccion = "Seed";

    /// <summary>
    /// <b>RUN_SEED</b>. Si es <c>true</c>, al arrancar se intenta sembrar lo que falta.
    /// Si es <c>false</c>, no se escribe absolutamente nada de la semilla.
    /// </summary>
    public bool Ejecutar { get; set; }

    /// <summary>
    /// Carpeta con los archivos <c>*.json</c> de la semilla. Un archivo por tabla, nombrado
    /// exactamente como la entidad, para que agregar un catálogo sea agregar un archivo y no tocar
    /// una lista en el código.
    /// </summary>
    public string Ruta { get; set; } = "scripts/seed";

    /// <summary>
    /// Crear el usuario administrador inicial si la tabla de usuarios está vacía. Se apaga en
    /// ambientes con datos reales.
    /// </summary>
    public bool CrearUsuarioAdministrador { get; set; } = true;

    /// <summary>Correo del administrador inicial. Sin valor, se usa el de la variable de ambiente.</summary>
    public string? AdministradorCorreo { get; set; }

    /// <summary>
    /// Contraseña inicial. <b>Nunca</b> se escribe en un archivo de despliegue: viene de
    /// <c>ADMIN_PASSWORD</c> y solo se usa si la base está vacía, obligando a cambiarla en el
    /// primer ingreso.
    /// </summary>
    public string? AdministradorContrasena { get; set; }

    /// <summary>Escribir en el log el detalle de qué se insertó, qué se omitió y qué hubo conflicto.</summary>
    public bool InformarDetalle { get; set; } = true;
}
