/// <summary>
/// Entidades de la Fase 5 sobre las tablas <b>legacy existentes</b> de requisa
/// (<c>dbo.Requisa</c>, <c>dbo.Requisa_Detalle</c> y <c>dbo.Requisa_Tecnicos</c>). Se mapean con
/// <c>ExcludeFromMigrations()</c>: EF las lee y escribe, pero nunca las crea ni las altera (D-22).
/// <para>La requisa no se inventa: <c>SINCO_GENERA_REQUISA</c> la crea desde la orden de trabajo y
/// usa el mismo número. Acá solo se consulta, se edita mientras está en <c>GE</c> y se anula
/// delegando en el SP original. No se agrega un <c>Id</c> nuevo (D-21).</para>
/// </summary>
namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>Cabecera de <c>dbo.Requisa</c>. La PK legacy es <c>REQUISA_NUMERO</c>.</summary>
public class Requisa
{
    public string RequisaNumero { get; set; } = null!;
    public string? OrdenNumero { get; set; }
    public DateOnly FechaRequisa { get; set; }
    public DateOnly? FechaOrden { get; set; }
    public string? Cliente { get; set; }
    public string? Locacion { get; set; }
    public string? Instrucciones { get; set; }

    /// <summary><c>SI</c>/<c>NO</c>. El SP de generación lo deja en <c>SI</c>.</summary>
    public string? RequisaPendiente { get; set; }

    /// <summary><c>GE</c> generada, <c>AP</c> aprobada, <c>AN</c> anulada.</summary>
    public string? Estado { get; set; }

    public DateOnly? FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; }
}

/// <summary>
/// Línea de <c>dbo.Requisa_Detalle</c>. La PK legacy es <c>REQUISA_NUMERO + CONSECUTIVO</c>.
/// <para>⚠️ El nombre real de la columna en <c>SINCOERPORKIN</c> es <c>CANTIDAD_SOBFAL</c>, aunque
/// el script y el WinForms lo llamen <c>SOBFAL</c>. Se respeta la base real.</para>
/// </summary>
public class RequisaDetalle
{
    public string RequisaNumero { get; set; } = null!;
    public int Consecutivo { get; set; }
    public string? Bodega { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? DescripArticulo { get; set; }
    public string? Umedida { get; set; }
    public decimal? CantidadEntregada { get; set; }
    public decimal? CantidadUtilizada { get; set; }
    public decimal? CantidadDevuelta { get; set; }
    public decimal? CantidadSobfal { get; set; }
    public decimal? CostoUnitario { get; set; }
    public decimal? CostoTotal { get; set; }
    public string? CentroCosto { get; set; }
    public string? CuentaContable { get; set; }
}

/// <summary>Técnico de <c>dbo.Requisa_Tecnicos</c>. La PK legacy incluye código e iniciales.</summary>
public class RequisaTecnico
{
    public string RequisaNumero { get; set; } = null!;
    public int Consecutivo { get; set; }
    public string CodigoTecnico { get; set; } = null!;
    public string Iniciales { get; set; } = null!;
}
