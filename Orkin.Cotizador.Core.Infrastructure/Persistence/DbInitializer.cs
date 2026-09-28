using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Domain.Sistema;
using Orkin.Cotizador.Core.Infrastructure.Options;
using Orkin.Cotizador.Core.Infrastructure.Persistence.Seed;

namespace Orkin.Cotizador.Core.Infrastructure.Persistence;

/// <summary>
/// Deja la base en estado utilizable antes de que el proceso acepte peticiones:
/// esperarla, aplicar migraciones, sembrar catálogos y parámetros, y crear el administrador
/// inicial.
/// <para><b>Por qué un <see cref="IHostedService"/> y no un bloque en <c>Program.cs</c>:</b>
/// <see cref="IHostedService.StartAsync"/> se espera antes de que Kestrel empiece a escuchar. Un
/// <c>await</c> suelto en <c>Program.cs</c> también bloquearía, pero dejaría la inicialización
/// invisible para el host: no se puede apagar con <c>--no-start</c>, no aparece en los logs con
/// marca de servicio, y un segundo <c>IHostedService</c> arrancaría sin esperar a que la base esté
/// lista. Aquí el orden queda impuesto por el registro.</para>
/// <para><b>⚠️ Nunca <c>EnsureCreated</c>.</b> El esquema se crea con migraciones
/// (<c>dotnet ef database update</c> o el pipeline en Staging y Producción). <c>EnsureCreated</c>
/// crea el esquema directamente del modelo y a partir de ahí las migraciones dejan de tener sentido:
/// la base queda con la forma de hoy y ningún registro de cómo llegó ahí.</para>
/// <para><b>Por qué la semilla corre en su propio contexto, sin el interceptor de auditoría:</b>
/// sembrar insertó cada fila de cada catálogo, y eso no es "una acción del sistema sobre un dato de
/// negocio": es el <i>origen</i> de los datos. Quedaría registrado en la bitácora de migraciones y
/// en el log, una vez, y no en <c>aud.RegistroAuditoria</c> fila por fila. Una tabla de auditoría con
/// veinte mil filas de los catálogos al inicio esconde las que de verdad interesa ver.</para>
/// </summary>
public sealed class DbInitializer : IHostedService
{
    /// <summary>
    /// Espera máxima entre reintentos. Con el tope en 10 s: en desarrollo el contenedor de SQL Server
    /// suele estar listo en pocos segundos, y esperar medio minuto para volver a intentarlo produce
    /// más demora de arranque que la que ahorra.
    /// </summary>
    private const int EsperaMaximaSegundos = 10;

    private const int IntentosMaximos = 12;

    private readonly IServiceProvider _servicios;
    private readonly ILoggerFactory _fabricaLog;
    private readonly IHostEnvironment _entorno;
    private readonly DatabaseOptions _baseDeDatos;
    private readonly SemillaOptions _semilla;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        IServiceProvider servicios,
        ILoggerFactory fabricaLog,
        IHostEnvironment entorno,
        IOptions<DatabaseOptions> baseDeDatos,
        IOptions<SemillaOptions> semilla,
        ILogger<DbInitializer> logger)
    {
        _servicios = servicios;
        _fabricaLog = fabricaLog;
        _entorno = entorno;
        _baseDeDatos = baseDeDatos.Value;
        _semilla = semilla.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Inicializando la base de datos…");

        await EsperarBaseDeDatosAsync(cancellationToken);

        using var contexto = CrearContextoLimpio();

        await AplicarMigracionesAsync(contexto, cancellationToken);

        if (_semilla.Ejecutar)
        {
            await SembrarAsync(cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Siembra omitida: RUN_SEED está en false. Los catálogos y los parámetros se toman " +
                "de lo que ya hay en la base.");
        }

        await CrearAdministradorInicialAsync(cancellationToken);

        _logger.LogInformation("Base de datos lista.");
    }

    /// <summary>No hay nada que apagar: la inicialización ya terminó cuando se llama.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ═════════════════════════════════════════════════════════════════════════════
    //  Espera
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reintenta abrir la conexión con espera exponencial acotada. Sin esto, levantar la API y el
    /// contenedor de SQL Server a la vez produce un arranque fallido la mitad de las veces, y el
    /// síntoma —"no se pudo abrir la conexión"— no dice que el problema es de orden de arranque.
    /// <para><b>Por qué contra <c>master</c> y no contra la base de la aplicación:</b> en un
    /// arranque limpio —volumen nuevo de Docker, base recién creada— la base de la aplicación
    /// todavía no existe; la crean las migraciones que corren <b>después</b> de este chequeo.
    /// Chequear contra una base que no existe produce el error 4060 ("Cannot open database")
    /// que no es un problema de conectividad sino de orden de arranque, y el reintento no lo
    /// resuelve porque la base no va a aparecer sola. <c>master</c> siempre existe: si
    /// responde, el servidor está arriba y las migraciones pueden trabajar.</para>
    /// </summary>
    private async Task EsperarBaseDeDatosAsync(CancellationToken cancellationToken)
    {
        Exception? ultimoError = null;

        var constructor = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            _baseDeDatos.ConnectionString)
        {
            InitialCatalog = "master"
        };

        for (var intento = 1; intento <= IntentosMaximos; intento++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var sondeo = new Microsoft.Data.SqlClient.SqlConnection(constructor.ConnectionString);
                await sondeo.OpenAsync(cancellationToken);

                _logger.LogInformation("SQL Server responde.");
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ultimoError = ex;
            }

            var espera = TimeSpan.FromSeconds(Math.Min(EsperaMaximaSegundos, Math.Pow(2, intento - 1)));

            _logger.LogWarning(
                "SQL Server todavía no responde (intento {Intento} de {Intentos}). Reintento en {Espera}s.",
                intento,
                IntentosMaximos,
                espera.TotalSeconds);

            await Task.Delay(espera, cancellationToken);
        }

        throw new InvalidOperationException(
            $"No se pudo conectar con SQL Server después de {IntentosMaximos} intentos. Revise que " +
            "el contenedor esté arriba y que la cadena de conexión apunte al host correcto.", ultimoError);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Migraciones
    // ═════════════════════════════════════════════════════════════════════════════

    private async Task AplicarMigracionesAsync(
        CotizadorDbContext contexto,
        CancellationToken cancellationToken)
    {
        var pendientes = (await contexto.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pendientes.Length == 0)
        {
            _logger.LogInformation("No hay migraciones pendientes.");
            return;
        }

        if (!_baseDeDatos.AplicarMigracionesAlArrancar)
        {
            // Falla en vez de avisar. Un despliegue que sale adelante sin aplicar la migración deja
            // la aplicación en pie contra un esquema viejo, y el error aparece después, cuando una
            // consulta toca una columna que no existe en la base.
            throw new InvalidOperationException(
                $"Hay {pendientes.Length} migración(es) pendiente(s) y " +
                $"{nameof(DatabaseOptions.AplicarMigracionesAlArrancar)} está en false, que es lo " +
                $"correcto fuera de desarrollo. Aplíquelas como paso explícito del despliegue: " +
                $"dotnet ef database update. Pendientes: {string.Join(", ", pendientes)}");
        }

        _logger.LogInformation("Aplicando {Cantidad} migración(es)…", pendientes.Length);

        await contexto.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("Migraciones aplicadas.");
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Semilla
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Busca la carpeta de semilla, empezando en el ContentRoot y subiendo hasta la raíz del disco.
    /// <para>Si la ruta de <see cref="SemillaOptions.Ruta"/> es absoluta se respeta tal cual: quien
    /// la escribió de esa forma quiso decir exactamente dónde está, y buscar en otro lado sería
    /// desobedecer una instrucción explícita.</para>
    /// <para><b>Por qué el error lista los directorios revisados:</b> "no existe la carpeta" sin
    /// decir dónde se buscó deja a quien lee el arranque sin saber si el problema es que la carpeta
    /// no está, que la configuración apunta a otro lado, o que el contenedor no la copió. Son tres
    /// arreglos distintos y solo uno de ellos es código.</para>
    /// </summary>
    private string ResolverRutaSemilla()
    {
        if (Path.IsPathRooted(_semilla.Ruta))
        {
            return _semilla.Ruta;
        }

        var revisados = new List<string>();
        var directorio = new DirectoryInfo(_entorno.ContentRootPath);

        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, _semilla.Ruta);
            revisados.Add(candidato);

            if (Directory.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        throw new SemillaInvalidaException(
            $"No se encontró la carpeta de semilla '{_semilla.Ruta}' en ninguno de estos " +
            $"directorios: {Environment.NewLine}" +
            string.Join(Environment.NewLine, revisados.Select(r => "  " + r)) +
            $"{Environment.NewLine}Con RUN_SEED=true el proceso necesita leer los catálogos de ahí: " +
            "sin ellos la aplicación arranca sin ningún estado, modalidad ni permiso, y fallaría en " +
            "la primera pantalla. En un contenedor, la carpeta tiene que estar copiada en la imagen.");
    }

    private async Task SembrarAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Siembra activada (RUN_SEED = true).");

        // La ruta de la semilla es relativa a la raíz de la solución, no al directorio desde el que
        // arrancó el proceso: scripts/seed está al lado de los .env y de los .csproj, y no dentro de
        // cada proyecto. Por eso se busca subiendo directorios en vez de combinar contra
        // ContentRootPath, que en `dotnet run` es la carpeta del proyecto de la API y en un
        // contenedor es /app. La regla es la misma de CargadorArchivoEnv —el más cercano gana— y
        // cubre los tres casos: la API en desarrollo, las pruebas, y el contenedor donde la
        // carpeta está justo en el ContentRoot y se encuentra en el primer intento.
        var rutaSemilla = ResolverRutaSemilla();

        // Contexto propio, sin el interceptor: la siembra es el origen de los datos, no una acción
        // del sistema sobre datos de negocio. Ver el comentario de la clase.
        using var contexto = CrearContextoLimpio();

        var modelo = contexto.Model;

        // Leer y validar los archivos ANTES de abrir la transacción. Un error de tipeo en un JSON
        // tiene que dejar la base intacta, y una validación a medias con 20 tablas ya escritas es lo
        // peor de los dos mundos.
        var archivos = LectorSemilla.Leer(rutaSemilla, modelo);

        var ordenados = AplicadorSemilla.OrdenarPorDependencias(archivos, modelo);

        _logger.LogInformation(
            "Aplicando {Cantidad} archivo(s) de semilla desde {Ruta}.",
            ordenados.Count,
            rutaSemilla);

        var aplicador = new AplicadorSemilla(
            contexto,
            new EjecutorSql(contexto),
            _fabricaLog.CreateLogger<AplicadorSemilla>());

        IReadOnlyList<ResultadoSemilla> resultados;
        int permisos;

        await using (var transaccion = await contexto.Database.BeginTransactionAsync(cancellationToken))
        {
            resultados = aplicador.Aplicar(ordenados);
            permisos = await aplicador.AsignarPermisosDeRolAsync(ordenados, cancellationToken);

            await transaccion.CommitAsync(cancellationToken);
        }

        var insertados = resultados.Sum(r => r.Insertados);
        var omitidos = resultados.Sum(r => r.Omitidos);
        var conflictos = resultados.Sum(r => r.Conflictos.Count);

        if (_semilla.InformarDetalle)
        {
            foreach (var resultado in resultados)
            {
                _logger.LogInformation(
                    "  {Tabla,-40} insertados={Insertados,-4} omitidos={Omitidos,-4} conflictos={Conflictos}",
                    resultado.Tabla,
                    resultado.Insertados,
                    resultado.Omitidos,
                    resultado.Conflictos.Count);
            }
        }

        _logger.LogInformation(
            "Siembra terminada: {Insertados} insertados, {Omitidos} omitidos, {Conflictos} " +
            "conflictos, {Permisos} permisos asignados a roles.",
            insertados,
            omitidos,
            conflictos,
            permisos);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Administrador inicial
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Crea el primer usuario solo si la tabla de usuarios está vacía, y se lo lleva sin avisar si
    /// ya hay alguno.
    /// <para><b>Por qué el nombre del rol se lee de un parámetro y no de una constante:</b> el
    /// administrador decide cómo se llaman sus roles. Un <c>"Administrador"</c> compilado obligaría
    /// a que el rol se llamara exactamente así, o a editar el código para renombrarlo. Además, si el
    /// parámetro no está, el arranque falla diciendo qué falta, en vez de crear un usuario sin ningún
    /// permiso y sin que nadie lo note hasta que nadie puede entrar al sistema.</para>
    /// </summary>
    private async Task CrearAdministradorInicialAsync(CancellationToken cancellationToken)
    {
        if (!_semilla.CrearUsuarioAdministrador)
        {
            return;
        }

        using var alcance = _servicios.CreateScope();
        var provider = alcance.ServiceProvider;

        var usuarios = provider.GetRequiredService<UserManager<Usuario>>();
        var roles = provider.GetRequiredService<RoleManager<Rol>>();
        var parametros = provider.GetRequiredService<IParametros>();

        if (await usuarios.Users.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Ya hay usuarios en seg.Usuario; no se crea el administrador inicial.");
            return;
        }

        var correo = _semilla.AdministradorCorreo;

        if (string.IsNullOrWhiteSpace(correo))
        {
            throw new InvalidOperationException(
                "La tabla seg.Usuario está vacía y no se indicó el correo del administrador inicial. " +
                "Defina ADMIN_EMAIL (o Seed:AdministradorCorreo).");
        }

        var contrasena = _semilla.AdministradorContrasena;

        if (string.IsNullOrWhiteSpace(contrasena))
        {
            throw new InvalidOperationException(
                "La tabla seg.Usuario está vacía y no se indicó la contraseña del administrador " +
                "inicial. Defina ADMIN_PASSWORD (o Seed:AdministradorContrasena).");
        }

        var nombreRol = await parametros.TextoAsync(ParametrosSeguridad.RolAdministrador, cancellationToken);

        if (string.IsNullOrWhiteSpace(nombreRol))
        {
            throw new InvalidOperationException(
                $"Falta el parámetro '{ParametrosSeguridad.RolAdministrador}', que dice a qué rol " +
                "pertenece el administrador inicial. Sin él se crearía un usuario sin ningún " +
                "permiso, y eso solo se descubre cuando alguien intenta entrar.");
        }

        if (!await roles.RoleExistsAsync(nombreRol))
        {
            throw new InvalidOperationException(
                $"El parámetro '{ParametrosSeguridad.RolAdministrador}' dice '{nombreRol}', pero no " +
                "existe ese rol en seg.Rol. Corrija el parámetro o siembre los roles antes.");
        }

        var usuario = new Usuario
        {
            UserName = correo,
            Email = correo,
            NombreCompleto = "Administrador",
            EsActivo = true,

            // El administrador inicial es, por definición, el usuario del sistema: el único que no
            // se puede desactivar ni eliminar, y el que hay que dejar de lado para no quedarse sin
            // quien administre. Sin esta marca, el primer cambio de permisos que se haga sobre
            // "Administrador" lo vuelve un usuario más y deja la instancia sin salida.
            EsUsuarioSistema = true,

            // Obliga a cambiar la contraseña en el primer ingreso: la del .env del despliegue es
            // de todos los que la llegan a leer, y dejar viva esa puerta es peor que el inconveniente
            // de un paso extra.
            DebeCambiarContrasena = true,
            EmailConfirmed = true
        };

        var creado = await usuarios.CreateAsync(usuario, contrasena);

        if (!creado.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo crear el administrador inicial: " +
                string.Join("; ", creado.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        var asignado = await usuarios.AddToRoleAsync(usuario, nombreRol);

        // El resultado se revisa, y no por descuido. AddToRoleAsync no lanza cuando no logra
        // asignar: devuelve IdentityResult con Succeeded = false, y un administrador sin rol
        // entra, no ve un solo menú y no tiene por qué poder quejarse de nada, porque desde su
        // lado todo funcionó. Un error de arranque es la única forma de que alguien se entere.
        if (!asignado.Succeeded)
        {
            throw new InvalidOperationException(
                $"Se creó el usuario '{correo}' pero no se pudo asignarle el rol '{nombreRol}': " +
                string.Join("; ", asignado.Errors.Select(e => $"{e.Code}: {e.Description}")) +
                ". Corrija el rol en la semilla y vuelva a levantar la API; el usuario ya está " +
                "creado, así que esta vez el arranque lo va a saltarte y el problema va a seguir " +
                "hasta que se lo asigne a mano.");
        }

        _logger.LogInformation(
            "Administrador inicial creado con el rol '{Rol}'. Debe cambiar la contraseña en el " +
            "primer ingreso.",
            nombreRol);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Contexto
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Un <see cref="CotizadorDbContext"/> nuevo, sin el interceptor de auditoría y con la misma
    /// cadena de conexión que la aplicación. Se construye a mano y no por inyección porque es
    /// justamente lo que hace falta: la semilla no debe pasar por el interceptor.
    /// </summary>
    private CotizadorDbContext CrearContextoLimpio()
    {
        var opciones = new DbContextOptionsBuilder<CotizadorDbContext>()
            .UseSqlServer(
                _baseDeDatos.ConnectionString,
                sql => sql.CommandTimeout(_baseDeDatos.CommandTimeoutSegundos))
            .Options;

        return new CotizadorDbContext(opciones);
    }
}

/// <summary>
/// Los parámetros de <c>dbo.ParametroGeneral</c> que el arranque necesita.
/// <para>Están declarados junto al punto que los usa, y no en una clase central de constantes: si
/// alguien renombra la fila en Administración, el compilador señala el único lugar que hay que
/// cambiar. Lo que <b>no</b> hay aquí es el valor: el nombre del rol administrador sale del propio
/// parámetro (§5.20, ADR-018).</para>
/// </summary>
internal static class ParametrosSeguridad
{
    /// <summary>Rol al que pertenece el usuario administrador que se crea en el primer arranque.</summary>
    public const string RolAdministrador = "SEG.ROL_ADMINISTRADOR";
}
