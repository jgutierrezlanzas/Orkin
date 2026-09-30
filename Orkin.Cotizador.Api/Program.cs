using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Orkin.Cotizador.Api.Middlewares;
using Orkin.Cotizador.Api.Seguridad;
using Orkin.Cotizador.Core.Application.DependencyInjection;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Configuration;
using Orkin.Cotizador.Core.Infrastructure.DependencyInjection;
using Orkin.Cotizador.Core.Infrastructure.Persistence;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Orkin.Cotizador.Api;

/// <summary>
/// El composition root de la API: todo lo que se registra se registra una vez y por nombre.
/// <para>El orden importa en un punto y en ninguno más. Primero infraestructura, porque ahí se
/// decide cómo se abre la conexión; después aplicación; después seguridad, que necesita el
/// <c>CotizadorDbContext</c> ya registrado para las tablas de Identity; después CORS, que lee la
/// configuración; y por último los controladores. Lo que no depende del orden no lleva números ni
/// comentarios de orden, porque un comentario de orden se vuelve falso en cuanto algo se mueve.</para>
/// <para>La configuración se reemplaza entera en <see cref="Main"/> antes de registrar nada, para
/// que <c>IOptions</c>, CORS y el <c>JwtBearer</c> lean exactamente la misma que va a leer el
/// design-time factory de las migraciones. Ver <see cref="ConfiguracionBootstrap"/>.</para>
/// </summary>
public static class Program
{
    /// <summary>
    /// Nombre de la política de CORS. Vive en una constante para que el lugar donde se registra y
    /// el lugar donde se usa no puedan separarse por un error de tipeo.
    /// </summary>
    private const string PoliticaCors = "OrigenesDeclarados";

    /// <summary>
    /// Los eventos de <c>JwtBearer</c>: acá, y solo acá, se consulta la lista de revocados.
    /// <para><b>Por qué hace falta en cada petición:</b> cerrar sesión no cambia la firma del token.
    /// El token sigue siendo criptográficamente válido hasta que vence, y la validación por firma no
    /// tiene forma de saber que alguien lo cerró. Si nadie mira la lista de revocados, "cerrar
    /// sesión" es borrar el token del navegador y nada más, y el token sigue entrando hasta el
    /// vencimiento. Ver <see cref="RevocacionTokens"/>.</para>
    /// <para><b>Por qué se resuelve el servicio por <c>RequestServices</c>:</b>
    /// <c>OnTokenValidated</c> no recibe el contenedor de inyección, y hace falta
    /// <see cref="RevocacionTokens"/>, que es un singleton. <c>RequestServices</c> da el mismo
    /// contenedor que usa el resto de la petición, así que no hay una segunda instancia donde el
    /// registro quede de este lado y la consulta del otro.</para>
    /// <para><b>Por qué <c>Fail</c> y no una excepción:</b> <c>Fail</c> deja que el handler de
    /// <c>JwtBearer</c> responda 401 y no registre nada como error del servidor. Una excepción
    /// acá sería un 500 con el detalle del token en el log, que es justo la respuesta que no
    /// corresponde a "este token no sirve".</para>
    /// </summary>
    private static JwtBearerEvents EventosJwt() => new()
    {
        OnTokenValidated = contexto =>
        {
            var principal = contexto.Principal;

            if (principal is null)
            {
                return Task.CompletedTask;
            }

            var identificador = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            // El usuario y la hora de emisión solo hacen falta para el corte por usuario, que es lo
            // que mata las sesiones abiertas de alguien a quien le cambiaron la contraseña. Si el
            // token no los trae, se revisa igual la lista de identificadores: menos es mejor que
            // dejar de revisar. Ver RevocacionTokens.EstaRevocado.
            var usuario = principal.FindFirst(Claims.Id)?.Value;
            var emitido = MomentoDeEmision(principal);

            var revocacion = contexto.HttpContext.RequestServices.GetRequiredService<RevocacionTokens>();

            if (revocacion.EstaRevocado(identificador ?? string.Empty, usuario, emitido))
            {
                contexto.Fail("El token fue revocado en un cierre de sesión.");
            }

            return Task.CompletedTask;
        }
    };

    /// <summary>
    /// Cuándo se emitió el token, en UTC.
    /// <para><b>Por qué se buscan dos claims:</b> el token trae <c>iat</c> y <c>nbf</c>, y los dos
    /// valen, pero el <c>JwtSecurityTokenHandler</c> traduce al crear el principal los nombres
    /// cortos que conoce, y no todos los claim sobreviven con el mismo nombre. Se intenta el uno y
    /// luego el otro, y si ninguno está se devuelve <c>null</c>: es preferible no poder aplicar el
    /// corte a no adivinar una fecha, porque una fecha inventada revoca tokens que sí deberían
    /// servir.</para>
    /// </summary>
    private static DateTime? MomentoDeEmision(ClaimsPrincipal principal)
    {
        foreach (var nombre in new[] { JwtRegisteredClaimNames.Iat, JwtRegisteredClaimNames.Nbf })
        {
            var texto = principal.FindFirst(nombre)?.Value;

            if (long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var segundos))
            {
                return DateTimeOffset.FromUnixTimeSeconds(segundos).UtcDateTime;
            }
        }

        return null;
    }

    public static void Main(string[] args)
    {
        // El nombre del entorno tiene que estar disponible antes de construir la configuración,
        // porque de él dependen tanto el appsettings.{Entorno}.json como los .env. El host de
        // ASP.NET lo deduce de ASPNETCORE_ENVIRONMENT o del perfil de launchSettings; se lee acá de
        // forma explícita para que el orden no dependa de cuándo se instancia el host.
        var entorno = EntornoDelProceso(args);

        var construccion = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = entorno
        });

        // Se reemplaza la configuración del host por la del bootstrap. A partir de acá todo lo que
        // lea IConfiguration -los IOptions, el JwtBearer, CORS- ve exactamente la misma que ve el
        // design-time factory de las migraciones.
        var config = ConfiguracionBootstrap.Construir(
            entorno,
            construccion.Environment.ContentRootPath,
            args);

        construccion.Configuration.Sources.Clear();
        construccion.Configuration.AddConfiguration(config);

        // Licencia de QuestPDF (el generador de PDF de las cotizaciones). Community: gratis
        // para empresas con menos de 1M USD de ingresos anuales, que es el caso de ORKIN.
        // Sin esta línea, el primer PDF explota con 500 y el mensaje del bienvenida en inglés.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        Registrar(construccion);

        var app = construccion.Build();

        ConfigurarCanalizacion(app);

        app.Run();
    }

    private static void Registrar(WebApplicationBuilder construccion)
    {
        var configuration = construccion.Configuration;

        construccion.Services.AddInfrastructure(configuration);
        construccion.Services.AddApplicationServices();

        RegistrarSeguridad(construccion);
        RegistrarCors(construccion, configuration);
        RegistrarDocumentacion(construccion);

        construccion.Services
            .AddControllers()
            .AddJsonOptions(opciones =>
            {
                // Los null se mandan en la respuesta. La tentación de usar WhenWritingNull para
                // (payloads más chicos) se paga en la interfaz: un campo que aparece y desaparece
                // según el estado obliga a la UI a preguntar por la forma en vez de leer el dato.
                opciones.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            });

        construccion.Services.Configure<ApiBehaviorOptions>(opciones =>
        {
            // El filtro automático de ModelState devuelve un ValidationProblemDetails con su
            // propio formato, que no es el ApiResponse<T> que espera el cliente. Se apaga para que
            // sea el GlobalExceptionMiddleware el que decida la forma de la respuesta, y para que
            // haya una sola forma de error en toda la API.
            opciones.SuppressModelStateInvalidFilter = true;
        });
    }

    private static void RegistrarSeguridad(WebApplicationBuilder construccion)
    {
        var configuration = construccion.Configuration;

        // ── Identity ────────────────────────────────────────────────────────────────
        // AddIdentity y no AddIdentityCore porque AddIdentityCore no registra el
        // SignInManager, que es justo lo que valida la contraseña y cuenta los intentos. El
        // precio de esa elección son cuatro esquemas de cookie que esta API no usa, y el precio
        // se paga en dos lugares concretos, que son los dos que se corrigen más abajo:
        //
        //  1. AddIdentity deja el DefaultChallengeScheme en Identity.Application, así que cada
        //     401 terminaba en un CookieAuthenticationHandler del log. Se corrige fijando los
        //     tres esquemas por defecto en el JWT, en el bloque de más abajo.
        //  2. PasswordSignInAsync establece la sesión al validar la contraseña, y establecer la
        //     sesión para Identity es escribir una cookie. Se comprobó: cada ingreso devolvía un
        //     Set-Cookie de .AspNetCore.Identity.Application con la identidad de la persona
        //     dentro, y ese documento no lo revocaba el cierre de sesión, que solo revoca el
        //     identificador del token. Se corrige usando CheckPasswordSignInAsync, que verifica
        //     lo mismo y no establece nada. Ver AuthController.LoginAsync.
        //
        // Lo que se conserva de Identity es el hasheado de contraseñas, el contador de intentos y
        // el bloqueo, que es lo que no conviene reimplementar.
        //
        // La política de contraseña se deja en el mínimo razonable y no se endurece: la cuenta
        // Administrador del arranque tiene que poder crearse con la contraseña que viene en el .env
        // del despliegue, que es una frase, y endurecer aquí solo mueve el problema al instalador.
        // Endurecer es una decisión de D-16, no de este archivo.
        construccion.Services
            .AddIdentity<Usuario, Rol>(opciones =>
            {
                opciones.Password.RequiredLength = 10;
                opciones.Password.RequireNonAlphanumeric = false;
                opciones.Password.RequireUppercase = true;
                opciones.Password.RequireDigit = true;
                opciones.Lockout.MaxFailedAccessAttempts = 5;
                opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                opciones.Lockout.AllowedForNewUsers = true;
                opciones.SignIn.RequireConfirmedEmail = false;
                opciones.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<CotizadorDbContext>()
            .AddDefaultTokenProviders();

        // El IdentityErrorDescriber por defecto escribe los errores de política de contraseña en
        // inglés ("Passwords must have at least one digit"); la API responde en español. Se
        // registra después del AddIdentity porque en DI gana el último: este pisa al de Identity
        // sin necesidad de quitarlo antes. Ver IdentityErrorDescriberEspanol.
        construccion.Services.AddScoped<IdentityErrorDescriber, IdentityErrorDescriberEspanol>();

        // ── Opciones de seguridad ───────────────────────────────────────────────────
        // La clave de firma se valida al arrancar, y con longitud mínima. Una clave de cuatro
        // caracteres es un 500 a las tres de la mañana con un mensaje que dice "clave demasiado
        // corta" en el log, y "no puedo entrar" al usuario.
        //
        // HorasValidez acotado a 24 horas porque es el techo de lo que tarda en surtir efecto una
        // revocación: un permiso que se le quita a alguien sigue sirviendo hasta que su token
        // vence, y con una semana de validez eso no es un detalle.
        construccion.Services.AddOptions<SeguridadOptions>()
            .Bind(configuration.GetSection(SeguridadOptions.Seccion))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClaveFirma),
                $"Falta {SeguridadOptions.Seccion}:ClaveFirma. Va en el .env, no en appsettings.json.")
            .Validate(o => Encoding.UTF8.GetByteCount(o.ClaveFirma) >= 32,
                $"{SeguridadOptions.Seccion}:ClaveFirma necesita al menos 32 bytes para HMAC-SHA256. " +
                "Generar una con:  openssl rand -base64 48")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Emisor),
                $"Falta {SeguridadOptions.Seccion}:Emisor.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audiencia),
                $"Falta {SeguridadOptions.Seccion}:Audiencia.")
            .Validate(o => o.HorasValidez is > 0 and <= 24,
                $"{SeguridadOptions.Seccion}:HorasValidez debe estar entre 1 y 24. Pasarse de 24 hace " +
                "que revocar un permiso tarde más de un día en surtir efecto.")
            .ValidateOnStart();

        // ── JWT ─────────────────────────────────────────────────────────────────────
        // Los tres esquemas por defecto se fijan al JWT, y no solo el de autenticación. AddIdentity
        // los deja en sus propios esquemas de cookie, así que sin esto el de desafío terminaba en
        // un CookieAuthenticationHandler: no rompía nada, porque la cookie nunca se emitía, pero
        // metía una línea de log que parece un problema de autenticación en cada 401 de la API.
        //
        // El de ingreso también se fija, aunque hoy nada firme con cookie. Un DefaultSignInScheme
        // que apunta a Identity.External es una puerta abierta para el día que alguien escriba
        // un SignInAsync por costumbre: el token y la cookie pasarían a valer los dos, y solo uno
        // de los dos se revoca al cerrar sesión.
        //
        // El bloque se lee otra vez porque AddJwtBearer recibe un delegado, no un IOptions: la
        // configuración tiene que estar resuelta en el momento de armar el handler, que es al
        // arrancar, y no cuando alguien autentica. Que se lea con Get y no con IOptions es a
        // propósito: por IOptions habría que resolver una instancia de SeguridadOptions al
        // construir el handler, y hoy solo hace falta una copia de cuatro cadenas que nadie va a
        // cambiar mientras el proceso corra.
        var seguridad = configuration.GetSection(SeguridadOptions.Seccion).Get<SeguridadOptions>()
            ?? new SeguridadOptions();

        construccion.Services
            .AddAuthentication(esquema =>
            {
                esquema.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                esquema.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                esquema.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
                esquema.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(esquema =>
            {
                esquema.TokenValidationParameters = ParametroValidacion(seguridad);
                esquema.Events = EventosJwt();
            });

        // ── Autorización ────────────────────────────────────────────────────────────
        // El proveedor de políticas es singleton y construye la política bajo demanda a partir del
        // nombre: los permisos son filas de seg.Permiso y no hay forma de registrarlos uno por uno
        // en el composition root sin volver a quemarlos en el código. Ver ADR-007 y
        // ProveedorPoliticaPermisos.
        construccion.Services.AddSingleton<IAuthorizationPolicyProvider, ProveedorPoliticaPermisos>();
        construccion.Services.AddSingleton<IAuthorizationHandler, ManejadorPermisoRequerido>();
        construccion.Services.AddAuthorization();

        // ── Sesión ──────────────────────────────────────────────────────────────────
        construccion.Services.AddMemoryCache();
        construccion.Services.AddSingleton<RevocacionTokens>();
        construccion.Services.AddScoped<EmisorJwt>();
        construccion.Services.AddScoped<Pdf.GeneradorPdfCotizacion>();
        construccion.Services.AddHttpContextAccessor();

        // La infraestructura registra su propio IContextoUsuario para los procesos que no tienen
        // HttpContext (la semilla, el ETL). Acá se quita y se pone el de la API, para que la
        // bitácora registre el usuario real y no uno de relleno.
        construccion.Services.RemoveAll<IContextoUsuario>();
        construccion.Services.AddScoped<IContextoUsuario, ContextoUsuarioHttp>();

        // ── Data Protection ────────────────────────────────────────────────────────
        // Cifra la contraseña de la conexión al ERP del cliente (int.ErpConexion).
        // <para><b>Las claves tienen que vivir fuera del contenedor.</b> Sin un volumen que las
        // persista, una reconstrucción regenera el juego de claves y las contraseñas ya guardadas
        // dejan de descifrarse: la conexión sigue en la lista, con su servidor y su usuario, y al
        // probarla responde que no tiene credenciales. En docker-compose el volumen está montado en
        // <c>/root/.aspnet/DataProtection-Keys</c>; en producción hay que además cifrarlas y
        // respaldarlas, porque perder ese volumen es perder el acceso de todos los clientes.</para>
        construccion.Services.AddDataProtection();
    }

    /// <summary>
    /// Los parámetros con los que se valida el token.
    /// <para>Cada <c>Validate*</c> en <c>false</c> es un token que entra sin comprobar nada de lo
    /// que dice. Van todos en <c>true</c>: el algoritmo se acota a la lista de abajo, el emisor y
    /// la audiencia se comparan, y la vigencia se comprueba. La lista de algoritmos es lo que
    /// impide el ataque clásico de cambiar <c>HS256</c> por <c>none</c> y esperar que la
    /// validación lo tome por una firma válida.</para>
    /// <para><b>La tolerancia de reloj no es cero porque los relojes no están sincronizados:</b>
    /// entre la máquina que emite y la que valida puede haber un minuto de diferencia, y sin
    /// tolerancia un token emitido "en el segundo 60" falla en el segundo 59 del otro lado. El
    /// valor sale de configuración porque el costo de equivocarse es distinto en cada despliegue:
    /// con dos servidores en la misma sala es un problema menor; con uno al otro lado del país es una
    /// mockería, y los tokens caen solos.</para>
    /// <para><b>El nombre y el rol apuntan a los claims de Identity, no a <c>sub</c> y
    /// <c>role</c>:</b> es lo que <c>ClaimsPrincipal</c> usa para <c>User.Identity.Name</c> y para
    /// <c>IsInRole</c>, y dejarlos puestos acá evita tener que corregir los nombres uno por uno en
    /// el código que los consulta.</para>
    /// </summary>
    public static TokenValidationParameters ParametroValidacion(SeguridadOptions seguridad) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = seguridad.Emisor,
        ValidateAudience = true,
        ValidAudience = seguridad.Audiencia,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(seguridad.MinutosToleranciaReloj),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(seguridad.ClaveFirma)),
        ValidAlgorithms = ["HS256"],
        NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };

    private static void RegistrarCors(WebApplicationBuilder construccion, IConfiguration configuracion)
    {
        var origenes = configuracion.GetSection("Cors:Origenes").Get<string[]>() ?? [];

        construccion.Services.AddCors(opciones =>
        {
            opciones.AddPolicy(PoliticaCors, politica =>
            {
                // Una lista vacío deja la política sin orígenes, que es denegar todo. No se cae a
                // AllowAnyOrigin ni a un comodín: el navegador manda el Origin en cada llamada, y
                // un origen cualquiera convierte el token en un token que cualquiera puede gastar.
                // La lista vacía avisa en ConfigurarCanalizacion.
                if (origenes.Length != 0)
                {
                    politica
                        .WithOrigins(origenes)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });
    }

    private static void RegistrarDocumentacion(WebApplicationBuilder construccion)
    {
        construccion.Services.AddEndpointsApiExplorer();
        construccion.Services.AddSwaggerGen(opciones =>
        {
            opciones.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "API Cotizador ORKIN",
                Version = "v1",
                Description =
                    "Cotizador de control de plagas. La base nunca se escribe hacia el ERP del " +
                    "cliente: la integración es de una sola dirección y de solo lectura (ADR-017)."
            });

            opciones.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Token emitido por POST /api/auth/login."
            });

            opciones.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", documento)] = []
            });

            // Los comentarios XML se incluyen si el proyecto los genera. Si no están, Swagger
            // funciona igual: es una mejora, no una dependencia.
            var comentarios = Path.Combine(AppContext.BaseDirectory, "Comments.xml");

            if (File.Exists(comentarios))
            {
                opciones.IncludeXmlComments(comentarios);
            }
        });
    }

    private static void ConfigurarCanalizacion(WebApplication app)
    {
        var origenes = app.Configuration.GetSection("Cors:Origenes").Get<string[]>();

        if (origenes is null or { Length: 0 })
        {
            app.Logger.LogWarning(
                "Cors:Origenes está vacío: la política queda sin orígenes y el navegador bloqueará " +
                "las llamadas desde la interfaz. En desarrollo, agregar el origen de UI.Web.");
        }

        // El orden de la canalización es el orden en que corre cada cosa, y en dos pares depende
        // de ese orden:
        //
        // GlobalExceptionMiddleware va primero porque envuelve todo lo que viene después, y si
        // fuera después, una excepción de UseCors o de UseAuthentication no llegaría a él.
        //
        // UseCors va antes de UseAuthentication porque el relleno de CORS tiene que ver la
        // petición completa, y un 401 que se responde con cabeceras de CORS ausentes se ve en el
        // navegador como un error de red en vez de como "tu sesión venció".
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseHttpsRedirection();
        app.UseCors(PoliticaCors);
        app.UseAuthentication();
        app.UseAuthorization();

        ConfigurarSwaggerUI(app);

        app.MapControllers();
    }

    private static void ConfigurarSwaggerUI(WebApplication app)
    {
        // En producción Swagger queda apagado salvo que se pida explícitamente. No es que sea
        // peligroso exponerse: es que la interfaz de pruebas pasa a ser parte de la superficie
        // pública sin que nadie lo haya decidido.
        var esProduccion = app.Environment.EnvironmentName
            .Equals("Production", StringComparison.OrdinalIgnoreCase);

        if (!esProduccion || LeerBooleano(app.Configuration, "Swagger:Enable"))
        {
            app.UseSwagger();
            app.UseSwaggerUI(opciones =>
            {
                opciones.SwaggerEndpoint("/swagger/v1/swagger.json", "API Cotizador ORKIN v1");
                opciones.DocumentTitle = "API Cotizador ORKIN";
            });
        }
    }

    /// <summary>
    /// Lee un booleano de configuración aceptando más que <c>true</c>/<c>false</c>.
    /// <para>Una variable de ambiente no es un booleano: llega como el texto que le puso el
    /// despliegue, y lo que hay que soportar en la práctica es lo que alguien escribe en un
    /// archivo de despliegue: <c>1</c>, <c>si</c>, <c>sí</c>. Sin esto, un
    /// <c>Swagger:Enable=1</c> se lee como falso y el apagado queda sin explicación, que es peor que
    /// un aviso.</para>
    /// <para>Lo que no se acepta es un texto que no significa nada: se lee como falso, sin fallar,
    /// porque un valor mal escrito en la configuración de despliegue no debe impedir que la API
    /// levante.</para>
    /// </summary>
    private static bool LeerBooleano(IConfiguration configuracion, string clave)
    {
        var texto = configuracion[clave];

        if (bool.TryParse(texto, out var valor))
        {
            return valor;
        }

        return texto switch
        {
            "1" or "si" or "sí" => true,
            _ => false
        };
    }

    /// <summary>
    /// El nombre del entorno, leído antes de armar el host.
    /// <para>Los argumentos de línea de comandos van primero porque son lo más explícito que hay:
    /// quien escribe <c>--environment Staging</c> en la consola quiere Staging aunque la variable
    /// de entorno diga otra cosa, y al revés pasa siempre.</para>
    /// <para>Se lee de forma explícita y no se deja que el host lo deduzca, porque el bootstrap de
    /// la configuración necesita el nombre <i>antes</i> de que exista el host que lo deduce: la
    /// precedencia de los <c>.env</c> depende del entorno, y el entorno depende de esto.</para>
    /// <para>El valor por defecto es Production y no Development a propósito. Un despliegue que
    /// se olvidó de declarar el entorno tiene que arrancar con las validaciones duras, no con las
    /// blandas de desarrollo.</para>
    /// </summary>
    private static string EntornoDelProceso(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--environment" or "-e")
            {
                return args[i + 1];
            }
        }

        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";
    }
}
