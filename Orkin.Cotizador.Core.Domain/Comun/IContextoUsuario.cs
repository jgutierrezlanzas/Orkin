namespace Orkin.Cotizador.Core.Domain.Comun;

/// <summary>
/// Quién está haciendo la operación ahora mismo. Lo implementa la API leyendo el
/// <c>HttpContext</c>; los trabajos en segundo plano usan <c>ContextoUsuarioSistema</c>.
/// <para>Existe para que el <c>SaveChangesInterceptor</c> pueda sellar <c>CreatedBy</c> y escribir
/// la bitácora sin que el código de negocio se acuerde de hacerlo.</para>
/// </summary>
public interface IContextoUsuario
{
    /// <summary><c>null</c> en operaciones de sistema (migraciones, semilla,_jobs).</summary>
    Guid? UsuarioId { get; }

    /// <summary>Snapshot para la bitácora: tiene que sobrevivir al borrado del usuario.</summary>
    string? NombreUsuario { get; }

    string? Ip { get; }

    string? Agente { get; }

    /// <summary>Correlaciona la fila de <c>aud.RegistroAuditoria</c> con los logs de la API.</summary>
    string? TraceId { get; }
}

/// <summary>
/// Contexto de usuario vacío, para migraciones, semilla y trabajos programados. Registrado como
/// implementación por defecto para que ninguna parte falle por no tener un <c>HttpContext</c>.
/// </summary>
public sealed class ContextoUsuarioSistema : IContextoUsuario
{
    public Guid? UsuarioId => null;

    public string? NombreUsuario => null;

    public string? Ip => null;

    public string? Agente => null;

    public string? TraceId => null;
}
