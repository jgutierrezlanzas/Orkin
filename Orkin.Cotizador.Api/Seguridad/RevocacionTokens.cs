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
/// </summary>
public sealed class RevocacionTokens
{
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

    /// <summary><c>true</c> si ese token fue revocado explícitamente.</summary>
    public bool EstaRevocado(string identificador) =>
        !string.IsNullOrWhiteSpace(identificador)
        && _cache.TryGetValue(Clave(identificador), out bool _);

    /// <summary>
    /// La clave incluye el nombre del proceso, porque la caché es por instancia: en una
    /// configuración de varios nodos cada uno tiene la suya, y el prefijo evita que un identificador
    /// de un nodo se confunda con el de otro al leer logs o al depurar.
    /// </summary>
    private static string Clave(string identificador) => $"token-revocado:{Environment.MachineName}:{identificador}";
}
