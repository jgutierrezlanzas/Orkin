using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Orkin.Cotizador.Api.Seguridad;

/// <summary>
/// Convierte cualquier nombre de política en un requisito de permiso, sin registrarlo de antemano.
/// <para><b>Por qué hace falta:</b> en ASP.NET Core, <c>[Authorize(Policy = "X")]</c> falla si la
/// política <c>X</c> no está registrada. Como los nombres de política son los <c>Codigo</c> de
/// <c>seg.Permiso</c>, y esos códigos son <em>datos</em> que cambian cuando alguien agrega un
/// permiso, registrarlos uno por uno en C# significaría que cada permiso nuevo necesita un
/// despliegue. Eso es un catálogo quemado en el código, que es justo lo que ADR-018 elimina: el
/// conjunto de permisos sale de la base, no del código.</para>
/// <para><b>Por qué las políticas por rol sí llevan prefijo:</b> este proveedor trata <b>todo</b>
/// nombre sin prefijo como un código de permiso, y eso incluye los nombres mal escritos. Un
/// <c>Policy = "COTZACIONES.VER"</c> —con Z— produce una política válida de un permiso que nadie
/// tiene, y el síntoma es un 403, no un error de compilación. Se acepta, y por eso los atributos
/// llevan el código commented con el nombre de la tabla al lado, y hay una prueba que recorre todos
/// los códigos de <c>seg.Permiso</c> y comprueba que cada política resuelve. La alternativa —
/// registro explícito— es peor: obliga a un despliegue por cada permiso nuevo, que es la razón de
/// ser de este proveedor.</para>
/// <para><b>Por qué el caché:</b> <see cref="GetPolicyAsync"/> se llama en cada autorización. Sin
/// memoización, crear un objeto de política por petición es trabajo que se tira. Y el resultado
/// memoizado es correcto: la política no depende del usuario —depende de qué permisos se le
/// exigen—, y lo que cambia por usuario lo decide el manejador, no el proveedor.</para>
/// <para><b>Por qué las políticas no se redefinen en caliente:</b> los permisos que se agreguen
/// después no necesitan una política nueva, porque el manejador acepta cualquier código que venga
/// en el token. Por eso <see cref="GetPolicyAsync"/> de un nombre nuevo lo construye en el
/// momento, y por eso no hay caché que invalidar: no existe tal cosa como "redefinir" un
/// requisito.</para>
/// </summary>
public sealed class ProveedorPoliticaPermisos : IAuthorizationPolicyProvider
{
    /// <summary>
    /// Prefijo de las políticas que <b>no</b> son códigos de permiso. Hoy solo hay una: la que
    /// exige un rol.
    /// <para>El prefijo está al revés del que se podría esperar —es una marca de excepción— porque
    /// lo común son permisos y lo raro son roles. Si el prefijo fuera de permisos, habría que
    /// escribirlo en veinte atributos y olvidar uno abriría la puerta de arriba.</para>
    /// </summary>
    public const string PrefijoRol = "ROL:";

    /// <summary>
    /// Las políticas registradas a mano en <c>AddAuthorization</c>, y la política por defecto.
    /// <para><b>Por qué <see cref="AuthorizationOptions"/> y no el <see cref="IAuthorizationPolicyProvider"/>
    /// original:</b> este registro <em>reemplaza</em> al proveedor por defecto, así que pedir
    /// <c>IAuthorizationPolicyProvider</c> en el constructor devuelve esta misma clase y el
    /// contenedor de servicios no puede construirla. El error sale al arrancar, y es de los que
    /// salen con veinte servicios apilados en el mensaje y ninguna pista de cuál de ellos se está
    /// pidiendo a sí mismo.</para>
    /// <para>La salida es leer las opciones directamente: son el lugar donde de verdad viven las
    /// políticas registradas, y <see cref="AuthorizationOptions.GetPolicy"/> tiene la misma
    /// semántica que el proveedor original —devuelve <c>null</c> si no existe— y
    /// <see cref="AuthorizationOptions.DefaultPolicy"/> es, por defecto, la que exige usuario
    /// autenticado. Delegar en las opciones mantiene las dos cosas sin el ciclo.</para>
    /// </summary>
    private readonly IOptions<AuthorizationOptions> _opciones;

    /// <summary>
    /// Políticas ya construidas. El candado es un <see cref="Lock"/> y no un <c>object</c> porque el
    /// framework lo pide así desde .NET 9, y porque el error de tomar un candado reentrante se ve al
    /// compilar en vez de en producción.
    /// </summary>
    private readonly Dictionary<string, AuthorizationPolicy> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _candado = new();

    public ProveedorPoliticaPermisos(IOptions<AuthorizationOptions> opciones)
    {
        _opciones = opciones;
    }

    /// <summary>
    /// Nombres que empiezan por <see cref="PrefijoRol"/> se resuelven con las políticas registradas
    /// a mano, y cualquier otro nombre se interpreta como un código de <c>seg.Permiso</c>.
    /// </summary>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PrefijoRol, StringComparison.Ordinal))
        {
            // null si ese nombre no está registrado, que es lo que hacía el proveedor original.
            // Con null, AuthorizationPolicy.CombineAsync se queda con la política por defecto, y el
            // endpoint queda exigiendo solamente iniciar sesión.
            return Task.FromResult(_opciones.Value.GetPolicy(policyName));
        }

        lock (_candado)
        {
            if (_cache.TryGetValue(policyName, out var cacheada))
            {
                return Task.FromResult<AuthorizationPolicy?>(cacheada);
            }
        }

        // Se construye fuera del candado a propósito: el requisito es un record inmutable y la
        // construcción no hace E/S, así que dos peticiones que lo construyan a la vez producen dos
        // objetos iguales y una de ellas se descarta al escribir. Mantener el candado durante la
        // construcción serializaría todas las autorizaciones del proceso.
        var construida = Construir(policyName);

        lock (_candado)
        {
            _cache[policyName] = construida;
        }

        return Task.FromResult<AuthorizationPolicy?>(construida);
    }

    /// <summary>
    /// Sin política de respaldo. El punto de esto es explícito: un endpoint sin
    /// <c>[Authorize]</c> queda abierto, y no tiene por qué ser un error —<c>/health</c> es
    /// justamente uno de esos endpoints, a propósito—. Lo que no se hace es poner una política de
    /// respaldo que cierre por defecto, porque cerraría también las sondas de salud y obligaría a
    /// marcarlas una por una con <c>AllowAnonymous</c>.
    /// </summary>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy?>(null);

    /// <summary>
    /// La política que se aplica a un endpoint con <c>[Authorize]</c> sin nombre.
    /// <para>Se toma de <see cref="AuthorizationOptions.DefaultPolicy"/>, y el tipo de retorno es
    /// <c>Task&lt;AuthorizationPolicy&gt;</c> sin interrogación a diferencia de los otros dos métodos
    /// de la interfaz. Esa diferencia de nulabilidad es deliberada en el framework: esta política
    /// <b>siempre</b> existe —la de <c>AddAuthorization</c> exige que el usuario esté autenticado—,
    /// mientras que las otras dos pueden no existir. Delegar en vez de inventar una propia mantiene
    /// ese comportamiento: un <c>[Authorize]</c> pelado dice "algo hay que iniciar sesión" y nada
    /// más, sin exigir ningún permiso en particular.</para>
    /// </summary>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        Task.FromResult(_opciones.Value.DefaultPolicy);

    private static AuthorizationPolicy Construir(string policyName) =>
        new AuthorizationPolicyBuilder()
            .AddRequirements(new PermisoRequerido(policyName))
            .Build();
}

/// <summary>
/// El requisito: "este usuario tiene el permiso con este código".
/// <para>Es un <c>record</c> porque ASP.NET Core compara requisitos por igualdad, y dos requisitos
/// con el mismo código tienen que contar como el mismo. Con una clase que solo comparara por
/// referencia, la misma política evaluada dos veces crearía dos requisitos distintos y el
/// manejador los trataría como dos condiciones diferentes.</para>
/// <para>Implementa <see cref="IAuthorizationRequirement"/> porque esa interfaz es la marca que le
/// pone el framework a "esto es una condición de autorización", y sin ella el manejador genérico
/// ni siquiera compila. No declara ningún miembro: la interfaz es completamente vacía a propósito,
/// para que un requisito sea solo un dato y la lógica viva en el manejador.</para>
/// </summary>
/// <param name="Codigo">El <c>Codigo</c> de <c>seg.Permiso</c>, tal como se escribió en el
/// atributo <c>[Authorize]</c>.</param>
public sealed record PermisoRequerido(string Codigo) : IAuthorizationRequirement;

/// <summary>
/// Decide si el usuario tiene el permiso.
/// <para><b>Por qué el claim y no la base:</b> los permisos se resolvieron al emitir el token, y
/// volver a consultarlos por petición convertiría el token en una mera credencial sin contenido.
/// El precio es la ventana de retraso al revocar, y por eso la validez del token es corta.</para>
/// <para><b>Por qué se mira la denegación primero:</b> un código puede estar en los dos conjuntos —
/// concedido por el rol y denegado explícitamente— y el que gana es la denegación. Se comprueba
/// primero para que el resultado no dependa del orden en que se recorran los claims, que es un
/// detalle de la biblioteca y no una decisión de negocio.</para>
/// </summary>
public sealed class ManejadorPermisoRequerido : AuthorizationHandler<PermisoRequerido>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext contexto,
        PermisoRequerido requisito)
    {
        var denegado = contexto.User.HasClaim(Claims.PermisoDenegado, requisito.Codigo);

        if (denegado)
        {
            // No se llama a Succeed: sin eso, el requisito queda pendiente y la autorización falla,
            // que es lo que tiene que pasar.
            return Task.CompletedTask;
        }

        if (contexto.User.HasClaim(Claims.Permiso, requisito.Codigo))
        {
            contexto.Succeed(requisito);
        }

        return Task.CompletedTask;
    }
}
