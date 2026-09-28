using Microsoft.Extensions.Caching.Memory;

namespace Orkin.Cotizador.Api.Seguridad;

/// <summary>
/// Los tokens que alguien cerró sesión y que hay que rechazar aunque no hayan vencido.
/// <para><b>Por qué hace falta esto:</b> sin lista de revocación, "cerrar sesión" es borrar el token
/// del navegador y nada más. El token sigue siendo criptográficamente válido hasta que expira, así
/// que el equipo compartido de una sucursal sigue entrando al sistema con la sesión que dejó
/// abierta la persona anterior. Eso no es un detalle teórico: es exactamente lo que hace que un
/// sistema de cotizaciones sensitive se considere inseguro en una auditoría.</para>
/// <para><b>Por qué en memoria y no en la base:</b> porque perder la lista al reiniciar el proceso
/// solo tiene una consecuencia —que un token revocado vuelve a valer— y esa consecuencia dura ocho
/// horas como máximo, mientras que una tabla de revocados es una escritura extra en cada cierre de
/// sesión, una lectura extra en cada petición y una tabla que hay que respaldar y purgar. El
/// intercambio no sale barato. Cuando haga falta revocar de inmediato —un robo de credenciales—,
/// la lista de la memoria del proceso sigue sirviendo: revocar no reinicia el proceso.</para>
/// <para><b>Por qué la entrada caduca con el token:</b> guardarla más tiempo no sirve de nada, una
/// vez que el token venció la lista ya no lo distingue de uno válido. La entrada se borra sola, y
/// la memoria no crece con los ingresos del sistema sino solo con los cierres de sesión que aún
/// importan.</para>
/// <para><b>Una entrada por proceso:</b> con varias réplicas, cerrar sesión en una no revoca el
/// token en las otras. Es una limitación conocida y asumida, y la consecuencia real es que el token
/// sigue sirviendo en las otras réplicas hasta que vence. Lo que no se hace es fingir lo contrario:
/// el token se valida en todas, y el que se revoca es el identificador, que es lo único que la
/// lista puede comprobar sin volver a la base.</para>
/// <para><b>Por qué hace falta el corte además de la revocación por token:</b> cambiar la contraseña
/// tiene que matar las sesiones abiertas de esa persona en otros navegadores, y esos tokens no están
/// en el servidor: están en el navegador de cada uno. La lista solo conoce los identificadores que
/// alguien le pasó, y en el momento de cambiar la contraseña el servidor conoce uno —el de quien está
/// haciendo el cambio—. Por eso, además de revocar identificadores, hay un corte por usuario: la
/// hora desde la que ningún token de esa persona sirve. Un token anterior al corte está muerto, y
/// tampoco hace falta saber cuáles son.</para>
/// <para><b>Por qué no se consulta el sello de seguridad en cada petición:</b> sería la otra manera
/// de resolverlo —poner el sello en el token y compararlo con el de la base—, y funciona. Pero es
/// una consulta a la base en cada petición, que es justo lo que se pagó para no tener: los permisos
/// viajan en el token para que decidir sea comparar cadenas en memoria. El corte mantiene la misma
/// propiedad: se consulta en memoria, y solo se escribe cuando alguien cambia su contraseña.</para>
/// </summary>
public sealed class RevocacionTokens
{
    /// <summary>
    /// Cuánto vive una entrada de corte. Es el techo de lo que un token puede tardar en morir, y
    /// <c>Seguridad:HorasValidez</c> está acotado a 24 horas en el arranque, así que un día cubre
    /// con holgura cualquier token posible. Pasado ese día la entrada se borra sola, y para entonces
    /// todos los tokens anteriores al corte ya vencieron igual.
    /// </summary>
    private static readonly TimeSpan VidaDelCorte = TimeSpan.FromDays(1);

    private readonly IMemoryCache _cache;

    public RevocacionTokens(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>Marca un token como revocado hasta su propia fecha de vencimiento.</summary>
    public Task RegistrarAsync(string identificador, DateTime expiraUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identificador);

        var vida = expiraUtc - DateTime.UtcNow;

        if (vida <= TimeSpan.Zero)
        {
            // Un token que ya venció no hay que revocarlo: el validador lo rechaza por fecha. Guardarlo
            // sería una entrada que ocupa memoria y que nadie va a consultar nunca.
            return Task.CompletedTask;
        }

        _cache.Set(Clave(identificador), true, vida);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Mata todos los tokens de esa persona que se emitieron antes de <paramref name="desdeUtc"/>.
    /// <para><b>Por qué no se retrocede un corte ya puesto:</b> si alguien cambia la contraseña dos
    /// veces en el mismo minuto, el segundo corte puede tener la misma fecha o una anterior. Retroceder
    /// dejaría vivos los tokens que el primer corte había matado, y el orden importa: un corte es una
    /// promesa de que antes de esa hora no vale nada.</para>
    /// </summary>
    public void CortarSesiones(Guid usuarioId, DateTime desdeUtc)
    {
        if (usuarioId == Guid.Empty)
        {
            return;
        }

        var clave = ClaveDelCorte(usuarioId.ToString("D"));

        if (_cache.TryGetValue(clave, out DateTime actual) && actual <= desdeUtc)
        {
            return;
        }

        _cache.Set(clave, desdeUtc, VidaDelCorte);
    }

    /// <summary>
    /// <c>true</c> si ese token fue revocado explícitamente, o si es anterior al corte de su usuario.
    /// <para><b>Por qué los dos datos son opcionales:</b> un token puede venir sin usuario o sin
    /// fecha de emisión si lo emitió otro sistema, y en ese caso solo se puede comprobar la lista
    /// de identificadores. Un token al que le falta la fecha no queda vivo por eso: lo que no se
    /// puede comprobar es que no fue revocado, y el resto de la validación —firma, emisor, audiencia
    /// y vigencia— ya la hizo el <c>JwtBearer</c> antes de llegar acá.</para>
    /// </summary>
    public bool EstaRevocado(string identificador, string? usuario = null, DateTime? emitidoUtc = null)
    {
        if (!string.IsNullOrWhiteSpace(identificador)
            && _cache.TryGetValue(Clave(identificador), out bool _))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(usuario) || emitidoUtc is not { } emitido)
        {
            return false;
        }

        return _cache.TryGetValue(ClaveDelCorte(usuario), out DateTime corte) && emitido < corte;
    }

    /// <summary>
    /// La clave incluye el nombre del proceso, porque la caché es por instancia: en una
    /// configuración de varios nodos cada uno tiene la suya, y el prefijo evita que un identificador
    /// de un nodo se confunda con el de otro al leer logs o al depurar.
    /// </summary>
    private static string Clave(string identificador) => $"token-revocado:{Environment.MachineName}:{identificador}";

    /// <summary>
    /// El prefijo es distinto a propósito. Si compartiera función con la clave de revocación, un
    /// identificador de token tendría que ser un <c>Guid</c> con la misma forma que el de usuario
    /// para poder pisar la entrada, y dos cosas que solo se diferencian en el texto no deberían
    /// depender de que nadie se acuerde de la diferencia.
    /// </summary>
    private static string ClaveDelCorte(string usuario) => $"token-corte:{Environment.MachineName}:{usuario}";
}
