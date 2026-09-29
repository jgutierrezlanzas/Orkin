using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Resuelve <em>cualquier</em> nombre de política a un permiso, sin tener que registrar uno por
/// uno.
/// <para><b>Por qué hace falta.</b> El código de un permiso (<c>CATALOGOS.ORIGENES</c>) es
/// también el nombre de la política de autorización, por contrato con la API — así lo dice
/// <c>Claims.Permiso</c>—. Pero Blazor WASM no tiene idea de qué permisos existen: no lee
/// <c>seg.Permiso</c>, y registrar las políticas a mano en el cliente obligaría a tocar el
/// cliente cada vez que la base gana un permiso. Sin este proveedor, <c>AuthorizeRouteView</c>
/// lanza <c>InvalidOperationException: The AuthorizationPolicy named 'X' was not found</c> y la
/// página se cae con la pantalla de "Ha ocurrido un error inesperado", que no dice ni qué
/// política faltaba ni de dónde.</para>
/// <para><b>Por qué falla cerrado.</b> Un nombre de política equivocado también produce una
/// política, y esa política exige un claim con ese nombre, que nadie tiene. El error de
/// desarrollo es una página en blanco, no un acceso indebido: escribirse mal un permiso quita
/// acceso, no lo da. Es el sentido en que conviene que se equivoque.</para>
/// </summary>
public sealed class PoliticaPermisoProvider : IAuthorizationPolicyProvider
{
    /// <summary>
    /// Claim de permiso denegado explícitamente, que le gana al del rol. Mismo nombre que en
    /// la API.
    /// </summary>
    private const string ClaimPermisoDenegado = "orkin:permiso:no";

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        Task.FromResult(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy?>(null);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (string.IsNullOrWhiteSpace(policyName))
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        var politica = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new RequisitoPermiso(policyName))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(politica);
    }
}

/// <summary>
/// El permiso que una política exige. Solo guarda el código: el handler vive en
/// <see cref="ManejadorPermiso"/>.
/// </summary>
public sealed class RequisitoPermiso : IAuthorizationRequirement
{
    public RequisitoPermiso(string codigo)
    {
        Codigo = codigo;
    }

    public string Codigo { get; }
}

/// <summary>
/// Evalúa <see cref="RequisitoPermiso"/> contra los claims del token.
/// <para>La comparación es <see cref="StringComparison.OrdinalIgnoreCase"/> porque el código del
/// claim lo arma la API desde <c>seg.Permiso.Codigo</c> y el de la política lo escribe una
/// persona dentro de un atributo: si los dos no coinciden en mayúsculas la página se niega sin
/// que nadie entienda por qué, y ese tipo de falla se acaba resolviendo con más paciencia, no
/// con menos comillas.</para>
/// <para>La denegación explícita se comprueba <b>antes</b> que la concesión y gana. Sin eso,
/// quitarle un permiso a alguien dentro de su rol no serviría de nada: el claim del rol sigue
/// ahí y la página se abriría igual.</para>
/// </summary>
public sealed class ManejadorPermiso : AuthorizationHandler<RequisitoPermiso>
{
    private const string ClaimPermiso = "orkin:permiso";
    private const string ClaimPermisoDenegado = "orkin:permiso:no";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext contexto,
        RequisitoPermiso requisito)
    {
        var usuario = contexto.User;

        if (usuario.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var denegado = usuario
            .FindAll(ClaimPermisoDenegado)
            .Any(c => string.Equals(c.Value, requisito.Codigo, StringComparison.OrdinalIgnoreCase));

        if (denegado)
        {
            return Task.CompletedTask;
        }

        var concedido = usuario
            .FindAll(ClaimPermiso)
            .Any(c => string.Equals(c.Value, requisito.Codigo, StringComparison.OrdinalIgnoreCase));

        if (concedido)
        {
            contexto.Succeed(requisito);
        }

        return Task.CompletedTask;
    }
}
