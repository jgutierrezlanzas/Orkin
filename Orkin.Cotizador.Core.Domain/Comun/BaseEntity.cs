namespace Orkin.Cotizador.Core.Domain.Comun;

/// <summary>
/// Columnas base de auditoría, presentes en toda tabla de negocio (ADR-014, `BASE-DE-DATOS.md` §3.1).
/// <para>
/// <see cref="RowVersion"/> es <c>rowversion</c>: lo administra SQL Server solo, y por eso no
/// tiene setter público (EF Core lo escribe al insertar).
/// </para>
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Momento de creación, en UTC. Se escribe en la base (<c>SYSUTCDATETIME()</c>).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Usuario de <c>seg.Usuario</c> que creó la fila. <c>null</c> en las siembras del sistema.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Última modificación, en UTC. La mantiene el <c>SaveChangesInterceptor</c>.</summary>
    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Baja lógica. **Solo** en catálogos y maestros: las cotizaciones se anulan, no se desactivan.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Concurrencia optimista. Lo genera SQL Server; no se asigna en C#.</summary>
    public byte[] RowVersion { get; private set; } = [];
}
