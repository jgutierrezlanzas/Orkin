using System.IdentityModel.Tokens.Jwt;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Api.Seguridad;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Ingreso, salida y quién soy.
/// <para><b>Por qué el ingreso no usa el de ASP.NET Identity:</b> el de Identity devuelve una
/// cookie, y una cookie de sesión es lo contrario de lo que esta API necesita. El navegador que
/// consume estos endpoints es una aplicación de WebAssembly que ya está en otra página, y el token
/// se guarda y se manda a mano. Por eso el mismo <c>SignInManager</c> —que valida la contraseña,
/// cuenta los intentos y aplica el bloqueo— se usa desde un controlador propio, y lo único que se
/// agrega al final es la emisión del token. Reimplementar la validación de contraseña sería tirar
/// abajo la protección contra la fuerza bruta y todo el trabajo de hasheado que Identity ya
/// resuelve.</para>
/// <para><b>Por qué los tres endpoints no devuelven lo mismo:</b> el ingreso devuelve un token,
/// porque es lo único que cambia. Salir devuelve un mensaje. Y "quién soy" devuelve los datos del
/// usuario, que es una lectura normal. Un endpoint único que hiciera las tres cosas terminaría
/// devolviendo un token <c>null</c> en dos de los tres casos, y quien lo consuma tendría que
/// distinguir tres formas de "nada" en el mismo campo.</para>
/// <para><b>Por qué no hay aquí ningún <c>[Authorize]</c> con código de permiso:</b> este
/// permiso sería "poder entrar", y el que decide si se entra es el <c>PasswordSignInAsync</c> con
/// la contraseña correcta. Poner un permiso exigido en el ingreso obligaría a que el primer usuario
/// de la base existiera con ese permiso para poder crear al segundo, que es unhueco de arranque
/// que no vale la pena abrir.</para>
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// El mismo texto para toda credencial que no sirve. Vive en una constante y no en el sitio
    /// donde se usa porque se usa en tres ramas del mismo método, y tres copias del mismo texto
    /// divergen: alguien edita una y la persona recibe tres mensajes distintos según qué falló.
    /// </summary>
    private const string CredencialesIncorrectas =
        "El correo o la contraseña no son correctos.";

    private readonly SignInManager<Usuario> _ingreso;
    private readonly CotizadorDbContext _contexto;
    private readonly EmisorJwt _emisor;
    private readonly RevocacionTokens _revocacion;
    private readonly IContextoUsuario _quien;
    private readonly ILogger<AuthController> _log;

    public AuthController(
        SignInManager<Usuario> ingreso,
        CotizadorDbContext contexto,
        EmisorJwt emisor,
        RevocacionTokens revocacion,
        IContextoUsuario quien,
        ILogger<AuthController> log)
    {
        _ingreso = ingreso;
        _contexto = contexto;
        _emisor = emisor;
        _revocacion = revocacion;
        _quien = quien;
        _log = log;
    }

    /// <summary>
    /// Entra y devuelve el token.
    /// <para><b>Por qué un solo mensaje para todos los fallos:</b> decir "ese correo no existe" o
    /// "la contraseña está mal" por separado convierte el ingreso en un formulario para averiguar
    /// qué correos tiene el sistema. El único caso que se distingue es el de la cuenta bloqueada,
    /// porque ahí la persona no puede hacer nada por su cuenta y hay que decirle cuánto falta.</para>
    /// <para><b>Por qué la cuenta inactiva se trata como credencial inválida:</b> por la misma
    /// razón. Decirle a alguien "tu cuenta está desactivada" confirma que el correo existe. Y en la
    /// práctica no cambia nada para quien está frente a la pantalla: no puede entrar de las dos
    /// formas.</para>
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IResult> LoginAsync(
        [FromBody] SolicitudIngreso solicitud,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Credencial)
            || string.IsNullOrWhiteSpace(solicitud.Contrasena))
        {
            return ApiResponse<RespuestaIngreso>
                .Error("CREDENCIALES_INVALIDAS", CredencialesIncorrectas)
                .AHttp();
        }

        var usuario = await BuscarAsync(solicitud.Credencial, cancellationToken);

        if (usuario is null)
        {
            // Se registra el intento con lo que la persona escribió, que es el dato que sirve para
            // encontrar un ataque, y no se registra la contraseña nunca, ni por error.
            _log.LogWarning(
                "Intento de ingreso con una credencial que no existe. Petición {Referencia}.",
                HttpContext.TraceIdentifier);

            return ApiResponse<RespuestaIngreso>
                .Error("CREDENCIALES_INVALIDAS", CredencialesIncorrectas)
                .AHttp();
        }

        var resultado = await _ingreso.PasswordSignInAsync(
            usuario, solicitud.Contrasena, lockoutOnFailure: true, isPersistent: false);

        if (resultado.IsLockedOut)
        {
            // Este caso sí dice qué pasó, porque la persona no puede hacer nada por su cuenta y
            // tiene que saber a quién llamar. Sin el tiempo restante el aviso no sirve de nada.
            var restante = TiempoDeDesbloqueo(usuario);

            _log.LogWarning(
                "Cuenta bloqueada por intentos fallidos: {Usuario}. Petición {Referencia}.",
                usuario.UserName,
                HttpContext.TraceIdentifier);

            return ApiResponse<RespuestaIngreso>
                .Error(
                    "CUENTA_BLOQUEADA",
                    $"La cuenta está bloqueada por intentos fallidos. Intente de nuevo en {restante} " +
                    "minutos o pida que la liberen.")
                .AHttp();
        }

        if (resultado.RequiresTwoFactor)
        {
            // El segundo factor está en la base pero no implementado. Decirlo explícito es mejor
            // que devolver "credenciales incorrectas": la persona tiene la contraseña correcta y va
            // a seguir probándola, y eso termina en un bloqueo por intentos de más.
            return ApiResponse<RespuestaIngreso>
                .Error(
                    "SEGUNDO_FACTOR_NO_IMPLEMENTADO",
                    "Esta cuenta exige un segundo factor que todavía no está habilitado. Pida que " +
                    "se lo revisen.")
                .AHttp();
        }

        if (!resultado.Succeeded || !usuario.EsActivo)
        {
            _log.LogWarning(
                "Intento de ingreso rechazado. {Usuario}, activo: {Activo}. Petición {Referencia}.",
                usuario.UserName,
                usuario.EsActivo,
                HttpContext.TraceIdentifier);

            return ApiResponse<RespuestaIngreso>
                .Error("CREDENCIALES_INVALIDAS", CredencialesIncorrectas)
                .AHttp();
        }

        await AnotarAccesoAsync(usuario, cancellationToken);

        var token = await _emisor.EmitirAsync(usuario, cancellationToken);

        return ApiResponse<RespuestaIngreso>
            .Ok(new RespuestaIngreso(
                token.Token,
                token.ExpiraUtc,
                A(usuario),
                token.Permisos))
            .AHttp();
    }

    /// <summary>
    /// Quién es el usuario con el token que trae esta petición.
    /// <para>El <c>DebeCambiarContrasena</c> sí viene de la base, aunque ya esté en la respuesta
    /// del ingreso. Si el valor viviera solo en el token, cambiar la clave no surtiría efecto
    /// hasta que venciera, que es justo lo contrario de lo que se quiere.</para>
    /// <para><b>Por qué se vuelve a consultar:</b> un token válido no garantiza que la cuenta siga
    /// activa. A alguien se le puede dar de baja sin esperar a que su token venza, y en ese
    /// intervalo la única forma de enterarse es mirando la base. Sale 401 y no 404 porque, desde
    /// el punto de vista de quien llama, no hay sesión.</para>
    /// </summary>
    [HttpGet("yo")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IResult> YoAsync(CancellationToken cancellationToken)
    {
        var id = _quien.UsuarioId;

        if (id is null)
        {
            return ApiResponse<DatosUsuario>
                .Error("NO_AUTENTICADO", "La petición no trae un token válido.")
                .AHttp();
        }

        var usuario = await BuscarPorIdAsync(id.Value, cancellationToken);

        if (usuario is null || !usuario.EsActivo)
        {
            return ApiResponse<DatosUsuario>
                .Error("NO_AUTENTICADO", "La sesión de esta cuenta ya no está activa.")
                .AHttp();
        }

        return ApiResponse<DatosUsuario>.Ok(A(usuario)).AHttp();
    }

    /// <summary>
    /// Cierra la sesión: revoca el token que trae esta petición.
    /// <para><b>Por qué siempre responde lo mismo:</b> cerrar sesión "sale bien" incluso si el token
    /// ya estaba vencido o nunca existió. Si devolviera un error, la interfaz tendría que
    /// distinguir "no pude cerrar" de "ya estaba cerrada", y en los dos casos el resultado real es el
    /// mismo: borrar el token del navegador. Un <c>401</c> —que es lo que se recibe si el token ya
    /// venció— también es un cierre de sesión exitoso para quien lo pide.</para>
    /// <para><b>Por qué la revocación es real:</b> se marca el identificador del token hasta su
    /// propia vencimiento, así que deja de servir aunque siga siendo criptográficamente válido.
    /// Ver <see cref="RevocacionTokens"/>.</para>
    /// </summary>
    [HttpPost("salir")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IResult> SalirAsync()
    {
        var identificador = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (!string.IsNullOrEmpty(identificador))
        {
            await _revocacion.RegistrarAsync(identificador, VencimientoDelToken(), CancellationToken.None);
        }

        return ApiResponse<RespuestaSalida>
            .Ok(new RespuestaSalida("Sesión cerrada."))
            .AHttp();
    }

    /// <summary>
    /// Hasta cuándo hay que conservar la revocación.
    /// <para>Lo natural sería leer el vencimiento del propio token, y eso es lo que se intenta
    /// primero. Si el claim no está —y no siempre está, porque el <c>exp</c> puede quedar fuera de
    /// la <c>ClaimsPrincipal</c> según cómo se configuró la validación— se recurre a un día.</para>
    /// <para><b>Por qué un día y no lo que diga la configuración:</b> revocar de más no pasa nada,
    /// revocar de menos sí. Guardar la entrada un poco más de la vida del token cuesta unas pocas
    /// bytes de memoria y ningún otro efecto: para entonces el token ya está muerto igual. Y un día
    /// cubre con holgura el máximo posible, porque la validación de <c>Seguridad:HorasValidez</c> no
    /// deja pasar de 24 horas.</para>
    /// </summary>
    private DateTime VencimientoDelToken()
    {
        var texto = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        if (texto is not null && long.TryParse(texto, out var segundos))
        {
            return DateTimeOffset.FromUnixTimeSeconds(segundos).UtcDateTime;
        }

        return DateTime.UtcNow.AddDays(1);
    }

    /// <summary>
    /// Busca por correo o por nombre de cuenta, entre los usuarios activos.
    /// <para>Se hacen dos consultas en vez de una con un <c>OR</c> porque el índice de cada columna
    /// es distinto: con un <c>OR</c>, SQL Server no puede usar ninguno de los dos y termina
    /// recorriendo la tabla entera. En una tabla de usuarios esto no se nota, y en una que crece
    /// con cada alta sí.</para>
    /// </summary>
    private async Task<Usuario?> BuscarAsync(string credencial, CancellationToken cancellationToken)
    {
        var clave = credencial.Trim();

        return await _contexto.Usuario
            .FirstOrDefaultAsync(
                u => u.EsActivo
                     && (u.NormalizedUserName != null && u.NormalizedUserName == clave.ToUpperInvariant()),
                cancellationToken)
            ?? await _contexto.Usuario
                .FirstOrDefaultAsync(
                    u => u.EsActivo
                         && (u.NormalizedEmail != null && u.NormalizedEmail == clave.ToUpperInvariant()),
                    cancellationToken);
    }

    private async Task<Usuario?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await _contexto.Usuario
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <summary>
    /// Cuánto falta para que se desbloquee la cuenta, en minutos y redondeado hacia arriba.
    /// <para>Redondear hacia arriba es a propósito: decir "0 minutos" cuando faltan cuarenta
    /// segundos hace que la persona pruebe otra vez de inmediato, y ese intento cuenta como fallido
    /// y vuelve a alargar el bloqueo. Un minuto de más en el mensaje no le cuesta nada a nadie.</para>
    /// <para>Se lee <c>LockoutEnd</c> de la entidad y no se llama a
    /// <c>UserManager.GetLockoutEndDateAsync</c> porque el <c>SignInManager</c> no lo expone, y
    /// agregar el <c>UserManager</c> al constructor solo para leer un campo que ya está en la mano
    /// es una dependencia de más.</para>
    /// </summary>
    private static int TiempoDeDesbloqueo(Usuario usuario)
    {
        if (usuario.LockoutEnd is not { } fin)
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling((fin - DateTimeOffset.UtcNow).TotalMinutes));
    }

    /// <summary>
    /// Anota el último acceso.
    /// <para><b>Por qué el fallo acá no se come:</b> parecería tentador, sobre todo porque la
    /// contraseña ya se validó y el ingreso va a salir bien. Pero este <c>SaveChangesAsync</c> es el
    /// mismo que escribe la fila en <c>aud.RegistroAuditoria</c>: si se traga el error, alguien
    /// puede entrar y no queda rastro de que entró. Un ingreso que falla al anotarse es un
    /// <c>500</c> y una línea en el log; un ingreso que se completa sin rastro es una puerta
    /// abierta que nadie está mirando.</para>
    /// <para><b>Por qué en esta petición la bitácora queda sin autor:</b> el identificador de la
    /// fila de auditoría lo toma del token, y en un ingreso todavía no hay token —el usuario es
    /// justamente lo que se está autenticando—. La fila se escribe igual, con el identificador en
    /// blanco, y el log del intento queda en el archivo de texto, que sí sabe qué pasó.</para>
    /// </summary>
    private async Task AnotarAccesoAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        usuario.UltimoAccesoUtc = DateTime.UtcNow;
        usuario.UltimoAccesoIp = _quien.Ip;

        await _contexto.SaveChangesAsync(cancellationToken);
    }

    private static DatosUsuario A(Usuario usuario) => new(
        usuario.Id,
        usuario.NombreCompleto,
        usuario.Cargo,
        usuario.DebeCambiarContrasena);
}
