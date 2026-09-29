using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.MotorCalculo;
using Orkin.Cotizador.Core.Domain.Sistema;
using Orkin.Cotizador.Core.Infrastructure.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.MotorCalculo;
using Orkin.Cotizador.Core.Infrastructure.Options;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Infrastructure.DependencyInjection;

/// <summary>
/// Registra todo lo de infraestructura: la conexión, el contexto, la auditoría, el acceso a catálogos
/// y a parámetros, y la inicialización de la base.
/// <para>La API llama a un solo <c>AddInfrastructure(config)</c> y no conoce ninguna de estas
/// piezas. Agregar un servicio de infraestructura no debería obligar a tocar <c>Program.cs</c>.</para>
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuracion)
    {
        // ── Opciones ───────────────────────────────────────────────────────────────
        // ValidateOnStart hace que un valor faltante o mal formado falle al arrancar, y no la
        // primera vez que alguien abre una pantalla a las tres de la mañana.
        services.AddOptions<DatabaseOptions>()
            .Bind(configuracion.GetSection(DatabaseOptions.Seccion))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                $"Falta la cadena de conexión. Va en el .env como " +
                $"'{DatabaseOptions.Seccion}__ConnectionString', o en appsettings.json bajo " +
                $"'{DatabaseOptions.Seccion}:ConnectionString'. No es 'ConnectionStrings__...': " +
                "el binder solo conoce la sección 'Database'.")
            .Validate(o => o.MaxRetryCount > 0, "Database:MaxRetryCount debe ser mayor que cero.")
            .Validate(o => o.CommandTimeoutSegundos > 0, "Database:CommandTimeoutSegundos debe ser mayor que cero.")
            .ValidateOnStart();

        // RUN_SEED y las credenciales del administrador llegan del .env sin prefijo de sección
        // (ADMIN_EMAIL, no Seed__AdministradorCorreo), porque las escriben personas que no han
        // abierto el proyecto. Por eso no alcanza con Bind: se enlaza la sección y después se leen
        // las variables sueltas, que ganan porque son lo último que se define.
        services.AddOptions<SemillaOptions>()
            .Configure(opciones =>
            {
                configuracion.GetSection(SemillaOptions.Seccion).Bind(opciones);

                opciones.Ejecutar = LeerBool(configuracion, "RUN_SEED", opciones.Ejecutar);
                opciones.CrearUsuarioAdministrador =
                    LeerBool(configuracion, "CREATE_ADMIN_USER", opciones.CrearUsuarioAdministrador);

                opciones.AdministradorCorreo = PrimeraNoVacia(
                    opciones.AdministradorCorreo,
                    configuracion["ADMIN_EMAIL"]);

                opciones.AdministradorContrasena = PrimeraNoVacia(
                    opciones.AdministradorContrasena,
                    configuracion["ADMIN_PASSWORD"]);
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.Ruta),
                $"{SemillaOptions.Seccion}:{nameof(SemillaOptions.Ruta)} debe indicar la carpeta " +
                "con los archivos de semilla.")
            .ValidateOnStart();

        // ── Base de datos ──────────────────────────────────────────────────────────
        // El interceptor se registra antes que el contexto a propósito: la resolución es perezosa,
        // así que el orden no importa, pero leerlo en este orden evita la pregunta de si hará falta
        // moverlo.
        services.AddScoped<IContextoUsuario, ContextoUsuarioSistema>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<CotizadorDbContext>((proveedor, opciones) =>
        {
            var config = proveedor.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            opciones.UseSqlServer(
                config.ConnectionString,
                sql =>
                {
                    sql.CommandTimeout(config.CommandTimeoutSegundos);

                    // La cadena de conexión y la reintentabilidad son solo de lectura: cambiarlas en
                    // caliente está bien, pero EF no lo soporta y el error es críptico.
                    sql.MigrationsAssembly(typeof(CotizadorDbContext).Assembly.FullName);

                    if (config.MaxRetryCount > 0)
                    {
                        sql.EnableRetryOnFailure(
                            config.MaxRetryCount,
                            TimeSpan.FromSeconds(5),
                            null);
                    }
                });

            // Los datos sensibles en los mensajes de error son útiles en desarrollo y un accidente
            // fuera de él: un SqlException puede traer la consulta completa con valores.
            if (config.EnableSensitiveDataLogging)
            {
                opciones.EnableSensitiveDataLogging();
                opciones.EnableDetailedErrors();
            }

            // El interceptor va aquí, y no en un AddScoped<CotizadorDbContext> aparte: registrar el
            // contexto a mano compila igual, pero deja la base sin bitácora y sin que nadie lo note
            // hasta que se pregunta quién cambió ese precio.
            opciones.AddInterceptors(proveedor.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        // ── Catálogos y parámetros ─────────────────────────────────────────────────
        // Singleton con scope propio: la caché tiene que sobrevivir a las peticiones y a la
        // invalidación explícita desde Administración. Por eso no son scoped, aunque toquen la
        // base: abren su propio scope para leer y guardan solo texto.
        services.AddSingleton<ICatalogoResolver, CatalogoResolver>();
        services.AddSingleton<IParametros, ParametrosService>();
        services.AddSingleton<IConstructorConfiguracionMotor, ConstructorConfiguracionMotor>();

        // ── Arranque ───────────────────────────────────────────────────────────────
        // Un solo IHostedService que hace migraciones y semilla. Registrado con
        // AddHostedService, que lo resuelve como scoped.
        services.AddHostedService<DbInitializer>();

        // ── Proceso de carga desde origen externo (Fase 2.5) ─────────────────────
        // El orquestador es scoped porque depende de los cargadores, que a su vez
        // dependen del DbContext. Los lectores se construyen por llamada, no por DI,
        // porque cada carga puede tener su propio archivo o su propia conexión.
        services.AddScoped<Carga.ProcesoCargaService>();

        // ── Integración con el ERP (Fase 12) ─────────────────────────────────────
        // El cifrador de secretos (la contraseña del ERP del cliente nunca se guarda en claro)
        // se registra acá y no en la capa de aplicación porque es una pieza de infraestructura
        // que la aplicación consume.
        services.AddSingleton<Carga.CifradorSecreto>();

        return services;
    }

    /// <summary>
    /// Lee un booleano de una variable de ambiente que admite las tres escrituras habituales:
    /// <c>true</c>/<c>false</c>, <c>1</c>/<c>0</c> y <c>si</c>/<c>no</c>.
    /// <para>Un valor que no se reconoce no se adivina: se avisa y se usa el que ya venía. Un
    /// <c>RUN_SEED=yes</c> mal escrito que en realidad significaba "no" sembraría catálogos en una
    /// base de producción, y eso no puede pasar por un <c>TryParse</c> que devuelve falso en
    /// silencio.</para>
    /// </summary>
    private static bool LeerBool(IConfiguration configuracion, string clave, bool porDefecto)
    {
        var texto = configuracion[clave];

        if (string.IsNullOrWhiteSpace(texto))
        {
            return porDefecto;
        }

        var valor = texto.Trim().ToLowerInvariant();

        return valor switch
        {
            "1" or "true" or "si" or "sí" or "yes" => true,
            "0" or "false" or "no" => false,
            _ => throw new InvalidOperationException(
                $"La variable de entorno {clave} tiene el valor '{texto}', que no se puede " +
                "interpretar como verdadero o falso. Use true/false, 1/0 o si/no.")
        };
    }

    private static string? PrimeraNoVacia(params string?[] candidatos)
        => candidatos.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
}
