using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Seguridad;

/// <summary>
/// Emite el token de acceso con los permisos del usuario ya resueltos.
/// <para><b>Por qué los permisos van dentro del token y no se consultan en cada petición:</b> la
/// alternativa es ir a <c>seg.RolPermiso</c> y <c>seg.UsuarioPermiso</c> en cada authorize, y eso
/// son dos o tres consultas por petición para decidir algo que casi nunca cambia. Con los permisos
/// como <em>claims</em>, la decisión es una comparación de cadenas en memoria. El precio es que
/// revocar un permiso no surte efecto hasta que expire el token, y por eso la validez es corta:
/// ocho horas es un día de trabajo, no un mes.</para>
/// <para><b>Por qué la denegación explícita le gana al rol:</b> un usuario puede tener
/// <c>COTIZACIONES.VER.TODAS</c> por su rol y una denegación directa del mismo código, y entonces
/// lo que manda es la denegación. Es el mecanismo para decirle a un vendedor "no ves la sucursal
/// norte" sin inventar un rol entero para eso.</para>
/// <para><b>Por qué las denegaciones también viajan en el token:</b> si vivieran solo en la base,
/// el token traería el permiso concedido y el manejador no tendría cómo enterarse de la negación
/// sin volver a consultar, que es justo lo que se quiso evitar. Al viajar juntas, la información
/// llega completa y la decisión es local.</para>
/// <para><b>El permiso por defecto es denegar.</b> Si a un usuario no se le resuelve ningún permiso,
/// su token sale sin claims de permiso y la política dinámica lo rechaza. La alternativa —otorgar
/// todo a quien no se encontró— convertiría un fallo al calcular los permisos en un usuario con
/// acceso total, que es el peor fallo posible en este sistema.</para>
/// </summary>
public sealed class EmisorJwt
{
    private readonly CotizadorDbContext _contexto;
    private readonly SeguridadOptions _opciones;

    public EmisorJwt(
        CotizadorDbContext contexto,
        IOptions<SeguridadOptions> opciones)
    {
        _contexto = contexto;
        _opciones = opciones.Value;
    }

    /// <summary>Emite el token de un usuario con sus permisos ya resueltos.</summary>
    public async Task<TokenEmitido> EmitirAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var (concedidos, denegados) = await ResolverPermisosAsync(usuario.Id, cancellationToken);

        // Los denegados se descuentan de los concedidos antes de escribir los claims. Si un código
        // apareciera en los dos conjuntos —un permiso directo denegado y el mismo por el rol— sin
        // esto, el token llevaría las dos cosas y el resultado dependería del orden en que el
        // manejador mirara.
        var efectivos = concedidos.Except(denegados, StringComparer.OrdinalIgnoreCase).ToList();

        var ahora = DateTime.UtcNow;
        var expiracion = ahora.AddHours(_opciones.HorasValidez);
        var identificador = Guid.NewGuid().ToString("D");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString("D")),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString("D")),
            new(ClaimTypes.Name, usuario.UserName ?? string.Empty),

            // El nombre viaja en el token en vez de buscarse en la base en cada pantalla. Es dato de
            // presentación que cambia una vez cada años, y omitirlo significa una consulta por cada
            // encabezado de la interfaz.
            new(Claims.NombreCompleto, usuario.NombreCompleto ?? string.Empty),
            new(Claims.Id, usuario.Id.ToString("D")),

            new(JwtRegisteredClaimNames.Jti, identificador),
            new(
                JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(ahora).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        claims.AddRange(efectivos.Select(c => new Claim(Claims.Permiso, c)));
        claims.AddRange(denegados.Select(c => new Claim(Claims.PermisoDenegado, c)));

        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims: claims,
            notBefore: ahora,
            expires: expiracion,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.ClaveFirma)),
                SecurityAlgorithms.HmacSha256));

        // Acá no se toca la lista de revocados. Emitir y revocar son eventos distintos y en el
        // mismo instante: un token que se marca como revocado en el momento en que se firma
        // nace muerto, y la consecuencia es que la consulta de "quien soy" lo rechaza
        // acaba de entrar con la contraseña correcta. Es un error que se escribe con toda la
        // buena fe del mundo —"hay que registrar el token por si acaso"— y que no se detecta
        // leyendo el código, sino entrando con el usuario con el que se acaba de sembrar.
        //
        // La revocación vive en el cierre de sesión, que es donde significa algo. Ver
        // <see cref="RevocacionTokens"/> y el SalirAsync de <c>AuthController</c>.

        return new TokenEmitido(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiracion,
            ahora,
            efectivos);
    }

    /// <summary>
    /// Los códigos que le tocan a un usuario, separados en concedidos y denegados.
    /// <para>Un rol <b>inactivo</b> no aporta ningún permiso. Es lo que hace que desactivar un rol
    /// sirva de algo: si el permiso se leía solo del rol, desactivarlo no cambiaría nada hasta que
    /// el token venciera, y la respuesta a "ya le quité el acceso" sería "hasta dentro de ocho
    /// horas".</para>
    /// </summary>
    private async Task<(List<string> Concedidos, List<string> Denegados)> ResolverPermisosAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var ahora = DateTime.UtcNow;

        // Los roles del usuario se resuelven primero y en una sola consulta, porque no hay
        // navegación de Rol a Usuario en el modelo y filtrar por "los roles de este usuario"
        // tiene que hacerse desde la tabla puente. Las columnas se llaman UserId y RoleId y no
        // UsuarioId y RolId porque la tabla la mapea el IdentityUserRole<Guid> de Identity, que
        // no admite renombrar sus propiedades. La tabla sí se llama seg.UsuarioRol, y por eso la
        // propiedad del contexto se llama UsuarioRol y no UserRoles: el nombre de C# que se ve
        // arriba es el de la tabla, y el tipo que lleva dentro es el que Identity necesita.
        var rolesActivos = await _contexto.UsuarioRol
            .AsNoTracking()
            .Where(ur => ur.UserId == usuarioId)
            .Join(
                _contexto.Rol.AsNoTracking().Where(r => r.EsActivo),
                ur => ur.RoleId,
                r => r.Id,
                (_, r) => r.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        var porRol = await _contexto.RolPermiso
            .AsNoTracking()
            .Where(rp => rolesActivos.Contains(rp.RolId))
            .Join(
                _contexto.Permiso.AsNoTracking(),
                rp => rp.PermisoId,
                p => p.Id,
                (_, p) => p.Codigo)
            .ToListAsync(cancellationToken);

        var directos = await _contexto.Permiso
            .AsNoTracking()
            .Where(p => p.Usuarios.Any(up => up.UsuarioId == usuarioId))
            .Select(p => new
            {
                p.Codigo,
                Concedido = p.Usuarios
                    .Where(up => up.UsuarioId == usuarioId)
                    .Select(up => (bool?)up.Concedido)
                    .FirstOrDefault() ?? true,
                VigenteHasta = p.Usuarios
                    .Where(up => up.UsuarioId == usuarioId)
                    .Select(up => up.VigenteHasta)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var concedidos = porRol.ToList();
        var denegados = new List<string>();

        foreach (var directo in directos)
        {
            // Un permiso directo con fecha de vencimiento se ignora cuando ya venció, en vez de
            // seguir aplicando para siempre: una denegación temporal que no caduca es una
            // denegación permanente disfrazada.
            if (directo.VigenteHasta is not null && directo.VigenteHasta <= ahora)
            {
                continue;
            }

            if (directo.Concedido)
            {
                if (!concedidos.Contains(directo.Codigo, StringComparer.OrdinalIgnoreCase))
                {
                    concedidos.Add(directo.Codigo);
                }
            }
            else
            {
                denegados.Add(directo.Codigo);
            }
        }

        return (concedidos, denegados);
    }
}

/// <summary>El token emitido y lo que la sesión necesita saber de él.</summary>
/// <param name="Token">El <c>JWT</c> en texto. Viaja en el encabezado <c>Authorization</c>.</param>
/// <param name="ExpiraUtc">Cuándo deja de servir. La UI lo usa para saber cuándo reingresar.</param>
/// <param name="Permisos">Los códigos de permiso efectivos. La UI los usa para mostrar u ocultar
/// menús y botones <b>sin</b> preguntar a la API.</param>
public sealed record TokenEmitido(string Token, DateTime ExpiraUtc, DateTime EmitidoUtc, IReadOnlyList<string> Permisos);

/// <summary>
/// Los nombres de los <em>claims</em> del sistema.
/// <para><b>Por qué constantes y no literales:</b> el token lo escribe un proceso y lo lee otro —el
/// manejador de autorización—, y si el nombre se escribe a mano en los dos lados el error no
/// aparece al compilar: aparece como un token sin permisos que deja pasar a todo el mundo.
/// Estas no son constantes de catálogo —no dependen de datos de la base—, sino el contrato entre
/// quien emite y quien verifica.</para>
/// </summary>
public static class Claims
{
    /// <summary>
    /// Código de permiso concedido. El valor es el <c>Codigo</c> de <c>seg.Permiso</c>, que es
    /// también el nombre de la política de autorización: <c>COTIZACIONES.VER</c>.
    /// </summary>
    public const string Permiso = "orkin:permiso";

    /// <summary>Código de permiso denegado explícitamente, que le gana al del rol.</summary>
    public const string PermisoDenegado = "orkin:permiso:no";

    /// <summary>Nombre completo, para el encabezado de la interfaz.</summary>
    public const string NombreCompleto = "orkin:nombre";

    /// <summary>Identificador del usuario, en formato <c>D</c> con guiones.</summary>
    public const string Id = "orkin:usuario";
}
