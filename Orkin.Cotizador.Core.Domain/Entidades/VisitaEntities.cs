namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Visita confirmada: <c>dbo.ProgramacionVisitaConfirmadas</c> (tabla legacy existente, D-22).
/// <para>
/// No hereda de <see cref="Comun.BaseEntity"/>: la tabla no tiene <c>Id</c>, <c>CreatedAt</c> ni
/// <c>RowVersion</c>. Su clave es <c>ID_VISITA</c> (bigint, sin identity) y el consecutivo lo
/// asigna el servicio con <c>MAX + 1</c>, igual que el WinForms.
/// </para>
/// <para>
/// <c>ID_CALENDARIO</c> es el identificador del evento de Google Calendar y es la llave de unión
/// con <see cref="PlantillaCalendario"/>: cada visita apunta a la plantilla que le dio origen.
/// Los códigos de cliente, locación, servicio, vehículo y técnico son los del ERP (D-20), sin FK
/// local.
/// </para>
/// </summary>
public class ProgramacionVisitaConfirmada
{
    /// <summary>Clave legacy. No es identity: el servicio asigna <c>MAX(ID_VISITA) + 1</c>.</summary>
    public long IdVisita { get; set; }

    /// <summary>Identificador del evento (varchar 100). En el legacy viene de Google Calendar.</summary>
    public string IdCalendario { get; set; } = null!;

    /// <summary>Código de cliente de SOFTLAND (varchar 20).</summary>
    public string? Cliente { get; set; }

    /// <summary>Código de <c>vvLocacionesClientes</c> (varchar 5).</summary>
    public string? Locacion { get; set; }

    /// <summary>Código de <c>vvTipodeServicios</c> (varchar 4).</summary>
    public string? TipoServicio { get; set; }

    public DateOnly? FechaInicio { get; set; }

    public TimeSpan? HoraInicio { get; set; }

    public DateOnly? FechaFinal { get; set; }

    public TimeSpan? HoraFinal { get; set; }

    /// <summary>Código de <c>vvVehiculos</c> (varchar 4).</summary>
    public string? CodigoVehiculo { get; set; }

    public int? CantidadTecnicos { get; set; }

    public string? Nota { get; set; }
}

/// <summary>
/// Técnico asignado a una visita: <c>dbo.ProgramacionVisitaDetalleConfirmada</c>. Clave compuesta
/// <c>(ID_VISITA, LINEA)</c>, exactamente como en la tabla legacy.
/// </summary>
public class ProgramacionVisitaDetalleConfirmada
{
    public long IdVisita { get; set; }

    public int Linea { get; set; }

    /// <summary>Código de <c>vvTecnicos</c> (varchar 4).</summary>
    public string? CodigoTecnico { get; set; }

    /// <summary>Iniciales del técnico tal como las guarda el legacy (varchar 2).</summary>
    public string? Iniciales { get; set; }
}

/// <summary>
/// Vinculación plantilla ↔ evento de calendario: <c>dbo.Plantilla_Calendario</c> (tabla legacy).
/// Clave compuesta <c>(PLANTILLA, ID_CALENDARIO)</c>. Una plantilla puede tener muchas visitas
/// programadas y una visita apunta a una plantilla a través de <see cref="IdCalendario"/>.
/// </summary>
public class PlantillaCalendario
{
    /// <summary>Número de plantilla (<c>PLANTILLA</c>, bigint). Parte de la clave legacy.</summary>
    public long Plantilla { get; set; }

    /// <summary>Identificador del evento (varchar 100). Parte de la clave legacy.</summary>
    public string IdCalendario { get; set; } = null!;

    public DateOnly? FechaServicio { get; set; }

    public TimeSpan? HoraServicio { get; set; }

    public DateOnly? FechaCreacion { get; set; }

    public string? UsuarioCreacion { get; set; }
}

/// <summary>
/// Proyección mínima de <c>dbo.Orden_Trabajo</c> (tabla legacy, D-22). Fase 3 solo necesita saber
/// si una visita ya tiene orden generada —el legacy prohibe reversarla en ese caso— y leer su
/// número. La gestión completa de órdenes corresponde a la Fase 4.
/// </summary>
public class OrdenTrabajoVisita
{
    public string OrdenNumero { get; set; } = null!;

    public long IdVisita { get; set; }

    public long Plantilla { get; set; }

    /// <summary><c>GE</c> (generada), <c>AP</c> (aplicada) o <c>AN</c> (anulada).</summary>
    public string? Estado { get; set; }
}
