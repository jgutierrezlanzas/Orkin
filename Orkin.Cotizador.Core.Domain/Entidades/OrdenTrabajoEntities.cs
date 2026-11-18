/// <summary>
/// Entidades de la Fase 4 sobre las tablas <b>legacy existentes</b> de orden de trabajo
/// (<c>dbo.Orden_Trabajo</c> y sus cuatro hijas). Se mapean con <c>ExcludeFromMigrations()</c>:
/// EF las lee y escribe, pero nunca las crea ni las altera (D-22). Las claves son las del ERP,
/// incluida la inconsistencia histórica de tipo en <c>PLANTILLA</c> (<c>bigint</c> en cabecera,
/// servicios y técnicos; <c>int</c> en detalle y equipos).
/// </summary>
namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>Cabecera de <c>dbo.Orden_Trabajo</c>. La clave compuesta es la del WinForms.</summary>
public class OrdenTrabajo
{
    public string OrdenNumero { get; set; } = null!;
    public long IdVisita { get; set; }
    public long Plantilla { get; set; }
    public string? Cliente { get; set; }
    public DateOnly? FechaOrden { get; set; }
    public TimeSpan? Hora { get; set; }
    public string? Direccion { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Vehiculo { get; set; }

    /// <summary><c>SI</c>/<c>NO</c>. El SP de generación lo deja en <c>NO</c>.</summary>
    public string? ServicioRealizado { get; set; }

    public string? DescripTrabajo { get; set; }
    public string? Notas { get; set; }
    public string? Instrucciones { get; set; }

    /// <summary><c>GE</c> generada, <c>AP</c> aprobada, <c>AN</c> anulada.</summary>
    public string? Estado { get; set; }

    public DateOnly? FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; }
}

/// <summary>Líneas de producto de <c>dbo.Orden_Trabajo_Detalle</c>.</summary>
public class OrdenTrabajoDetalle
{
    public string OrdenNumero { get; set; } = null!;
    public int Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? DescripArticulo { get; set; }
    public string? TipoPlaga { get; set; }
    public string? LugarTratar { get; set; }
    public decimal? Area { get; set; }
    public string? Concentracion { get; set; }
    public string? CodigoDosificacion { get; set; }
    public string? MetodoAplicacion { get; set; }
    public decimal? Cantidad { get; set; }
    public string? Umedida { get; set; }
    public decimal? CostoUnitario { get; set; }
    public decimal? CostoTotal { get; set; }
}

/// <summary>Equipos de <c>dbo.Orden_Trabajo_Equipos</c>.</summary>
public class OrdenTrabajoEquipo
{
    public string OrdenNumero { get; set; } = null!;
    public int Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string? CodigoEquipo { get; set; }
    public string? Utilizado { get; set; }
}

/// <summary>Servicios de <c>dbo.Orden_Trabajo_Servicio</c>.</summary>
public class OrdenTrabajoServicio
{
    public string OrdenNumero { get; set; } = null!;
    public long Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string CodigoServicio { get; set; } = null!;
    public string CodigoFrecuencia { get; set; } = null!;
}

/// <summary>Técnicos de <c>dbo.Orden_Trabajo_Tecnicos</c>.</summary>
public class OrdenTrabajoTecnico
{
    public string OrdenNumero { get; set; } = null!;
    public long Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string CodigoTecnico { get; set; } = null!;
    public string Iniciales { get; set; } = null!;
}
