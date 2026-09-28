namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Registro de auditoría. <b>Append-only</b>: no se actualiza ni se borra jamás, y un trigger
/// bloquea <c>UPDATE</c> y <c>DELETE</c> sobre la tabla.
/// <para>Por eso <b>no</b> hereda <c>BaseEntity</c> ni lleva <c>IsActive</c>: el modelo de borrado
/// lógico de las tablas de negocio no aplica aquí.</para>
/// <para>La escribe un <c>SaveChangesInterceptor</c>, no el código de cada servicio. Por eso el
/// código de negocio no necesita acordarse de auditar.</para>
/// </summary>
public class RegistroAuditoria
{
    /// <summary><c>bigint IDENTITY</c>: es de alto volumen y nunca se reescribe.</summary>
    public long Id { get; set; }

    /// <summary>Nombre de la entidad afectada: "Cotizacion", "Producto", "Usuario".</summary>
    public string Entidad { get; set; } = null!;

    public Guid EntidadId { get; set; }

    /// <summary>FK → <c>dbo.AccionAuditoriaCatalogo</c> (§5.13).</summary>
    public byte AccionId { get; set; }

    public Guid? UsuarioId { get; set; }

    /// <summary>Snapshot: la bitácora tiene que sobrevivir al borrado del usuario.</summary>
    public string? NombreUsuario { get; set; }

    public DateTime FechaUtc { get; set; }

    /// <summary><c>nvarchar(45)</c>: soporta IPv6.</summary>
    public string? Ip { get; set; }

    public string? Agente { get; set; }

    /// <summary>Correlaciona con los logs de la API.</summary>
    public string? TraceId { get; set; }

    /// <summary>JSON con los nombres de los campos que cambiaron: <c>["PrecioCordobas","Estado"]</c>.</summary>
    public string? CamposModificados { get; set; }

    /// <summary>JSON con el valor anterior, solo de los campos modificados.</summary>
    public string? ValorAnteriorJson { get; set; }

    /// <summary>JSON con el valor nuevo, solo de los campos modificados.</summary>
    public string? ValorNuevoJson { get; set; }

    /// <summary>Texto libre: "anulada por error de tipo de cambio".</summary>
    public string? Motivo { get; set; }
}
