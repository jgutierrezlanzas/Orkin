using System.Security.Claims;

using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Seguridad;

/// <summary>
/// Quién está haciendo la operación, leído del <c>HttpContext</c>.
/// <para><b>Por qué vive en la API y no en la infraestructura:</b> leer el usuario actual es leer
/// el <c>HttpContext</c>, y el <c>HttpContext</c> es un concepto de ASP.NET. Si esta clase viviera
/// en la infraestructura, esa capa tendría que depender del framework web, y las pruebas de dominio
/// —que no levantan ningún servidor— no podrían resolver un <c>IContextoUsuario</c> sin inventarse
/// uno. Acá la interfaz queda en el Domain, que no sabe de qué viene, y cada parte decide qué
/// significa "usuario actual" para ella: la API, la petición; la semilla y los trabajos, nada.</para>
/// <para><b>Por qué el nombre y el identificador se copian y no se releen:</b> la bitácora es
/// append-only y tiene que seguir siendo legible después de que el usuario se borre o se renombre.
/// Guardar el nombre en el momento de la operación es lo que permite que <c>aud.RegistroAuditoria</c>
/// diga "lo cambió Fulana de Tal" aunque mañana ese usuario ya no exista o se llame de otra forma.
/// Una referencia viva a la tabla de usuarios daría un vacío en el informe, y ese informe es
/// precisamente lo que uno consulta después de un problema.</para>
/// </summary>
public sealed class ContextoUsuarioHttp : IContextoUsuario
{
    private readonly IHttpContextAccessor _accesor;

    public ContextoUsuarioHttp(IHttpContextAccessor accesor)
    {
        _accesor = accesor;
    }

    private ClaimsPrincipal? Usuario => _accesor.HttpContext?.User;

    /// <summary>
    /// El identificador del usuario autenticado, o <c>null</c> si la petición no tiene token.
    /// <para>Se lee del claim con el nombre que declara el framework y no del que puso este sistema
    /// para su propio identificador, porque el primero es el que el <c>JwtBearer</c> garantiza haber
    /// firmado. El segundo es un dato más dentro del token, y un dato del token es algo que el token
    /// dice de sí mismo.</para>
    /// <para>Si el claim existe pero no es un <c>Guid</c>, se devuelve <c>null</c> en vez de lanzar.
    /// Un token con el identificador malformado no es motivo para tirar abajo la petición —si el
    /// endpoint exige autorización, el middleware de autorización ya la rechazó—, y en uno público
    /// alcanza con decir que no hay usuario.</para>
    /// </summary>
    public Guid? UsuarioId
    {
        get
        {
            var valor = Usuario?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(valor, out var id) ? id : null;
        }
    }

    /// <summary>
    /// El nombre para la bitácora. Prefiere el nombre completo, que es lo que una persona reconoce
    /// en un informe, y cae al nombre de cuenta cuando el token no lo trae.
    /// </summary>
    public string? NombreUsuario =>
        Usuario?.FindFirst(Claims.NombreCompleto)?.Value
        ?? Usuario?.FindFirst(ClaimTypes.Name)?.Value;

    /// <summary>
    /// La IP del cliente.
    /// <para>Sale de <c>RemoteIpAddress</c> y no de una cabecera como <c>X-Forwarded-For</c>, porque
    /// esa cabecera la puede escribir cualquiera y lo que se registra aquí es quién hizo un cambio en
    /// la base. En producción hay un proxy de por delante —el de Traefik— y hay que configurar
    /// <c>UseForwardedHeaders</c> para que ASP.NET sepa qué cabecera es confiable y cuál no; sin
    /// eso, todas las filas de la bitácora dirán la dirección del proxy, que no sirve para nada.
    /// Queda anotado en el README porque es de esos detalles que solo se notan cuando ya hay que
    /// investigar un incidente.</para>
    /// </summary>
    public string? Ip => _accesor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// El agente del navegador, tal como lo declara. Va en la bitácora como contexto y no como dato
    /// confiable: es texto libre que escribe el cliente, sirve para distinguir "abrió la página" de
    /// "fue un proceso", y no para decidir nada.
    /// </summary>
    public string? Agente
    {
        get
        {
            var agente = _accesor.HttpContext?.Request.Headers.UserAgent.ToString();

            return string.IsNullOrWhiteSpace(agente) ? null : agente;
        }
    }

    /// <summary>El identificador de la petición, para correlacionar la fila con los logs.</summary>
    public string? TraceId => _accesor.HttpContext?.TraceIdentifier;
}
