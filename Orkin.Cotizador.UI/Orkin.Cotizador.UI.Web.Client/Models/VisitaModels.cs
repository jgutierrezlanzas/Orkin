namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>
/// Modelos de la agenda propia. Las claves son las del ERP: <c>IdVisita</c> e
/// <c>IdCalendario</c>; no se inventa un identificador nuevo (D-22).
/// </summary>

public sealed class VisitaTecnicoModel
{
    public long IdVisita { get; set; }
    public int Linea { get; set; }
    public string? CodigoTecnico { get; set; }
    public string? Iniciales { get; set; }
}

public sealed class VisitaModel
{
    public long IdVisita { get; set; }
    public string IdCalendario { get; set; } = string.Empty;
    public string? Cliente { get; set; }
    public string? ClienteNombre { get; set; }
    public string? Locacion { get; set; }
    public string? TipoServicio { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public TimeSpan? HoraInicio { get; set; }
    public DateOnly? FechaFinal { get; set; }
    public TimeSpan? HoraFinal { get; set; }
    public string? CodigoVehiculo { get; set; }
    public int? CantidadTecnicos { get; set; }
    public int Tecnicos { get; set; }

    /// <summary>Códigos ERP de los técnicos asignados: base de los calendarios por técnico.</summary>
    public List<string> CodigosTecnico { get; set; } = [];
    public string? Nota { get; set; }
    public long? Plantilla { get; set; }
    public string? DescripTrabajo { get; set; }
    public string? OrdenNumero { get; set; }
    public List<VisitaTecnicoModel> TecnicosDetalle { get; set; } = [];
    public List<long> Calendarios { get; set; } = [];

    public string Titulo => ClienteNombre ?? Cliente ?? $"Visita {IdVisita}";

    public string HoraTexto => HoraInicio is { } h ? h.ToString(@"hh\:mm") : "--:--";
}

/// <summary>
/// Contrato del endpoint de detalle (<c>GET api/visitas/{id}</c>). La API devuelve
/// <c>tecnicos</c> como lista de detalle, mientras que el resumen de la grilla lo devuelve
/// como entero; por eso se mapea explícitamente a <see cref="VisitaModel"/>.
/// </summary>
public sealed class VisitaDetalleModel
{
    public long IdVisita { get; set; }
    public string IdCalendario { get; set; } = string.Empty;
    public string? Cliente { get; set; }
    public string? ClienteNombre { get; set; }
    public string? Locacion { get; set; }
    public string? TipoServicio { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public TimeSpan? HoraInicio { get; set; }
    public DateOnly? FechaFinal { get; set; }
    public TimeSpan? HoraFinal { get; set; }
    public string? CodigoVehiculo { get; set; }
    public int? CantidadTecnicos { get; set; }
    public string? Nota { get; set; }
    public long? Plantilla { get; set; }
    public string? DescripTrabajo { get; set; }
    public string? OrdenNumero { get; set; }
    public List<VisitaTecnicoModel> Tecnicos { get; set; } = [];
    public List<long> Calendarios { get; set; } = [];

    public VisitaModel AVista() => new()
    {
        IdVisita = IdVisita,
        IdCalendario = IdCalendario,
        Cliente = Cliente,
        ClienteNombre = ClienteNombre,
        Locacion = Locacion,
        TipoServicio = TipoServicio,
        FechaInicio = FechaInicio,
        HoraInicio = HoraInicio,
        FechaFinal = FechaFinal,
        HoraFinal = HoraFinal,
        CodigoVehiculo = CodigoVehiculo,
        CantidadTecnicos = CantidadTecnicos,
        Tecnicos = CantidadTecnicos ?? Tecnicos.Count,
        CodigosTecnico = Tecnicos
            .Where(t => !string.IsNullOrWhiteSpace(t.CodigoTecnico))
            .Select(t => t.CodigoTecnico!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList(),
        Nota = Nota,
        Plantilla = Plantilla,
        DescripTrabajo = DescripTrabajo,
        OrdenNumero = OrdenNumero,
        TecnicosDetalle = Tecnicos,
        Calendarios = Calendarios
    };
}

public sealed class VisitaGuardarModel
{
    /// <summary>Clave legacy. Cero en altas; solo se usa para elegir POST/PUT.</summary>
    public long IdVisita { get; set; }

    public string? IdCalendario { get; set; }
    public string? Cliente { get; set; }
    public string? Locacion { get; set; }
    public string? TipoServicio { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public TimeSpan? HoraInicio { get; set; }
    public DateOnly? FechaFinal { get; set; }
    public TimeSpan? HoraFinal { get; set; }
    public string? CodigoVehiculo { get; set; }
    public string? Nota { get; set; }
    public long? Plantilla { get; set; }
    public List<VisitaTecnicoModel> Tecnicos { get; set; } = [];

    public static VisitaGuardarModel Desde(VisitaModel visita) => new()
    {
        IdVisita = visita.IdVisita,
        IdCalendario = visita.IdCalendario,
        Cliente = visita.Cliente,
        Locacion = visita.Locacion,
        TipoServicio = visita.TipoServicio,
        FechaInicio = visita.FechaInicio,
        HoraInicio = visita.HoraInicio,
        FechaFinal = visita.FechaFinal,
        HoraFinal = visita.HoraFinal,
        CodigoVehiculo = visita.CodigoVehiculo,
        Nota = visita.Nota,
        Plantilla = visita.Plantilla,
        Tecnicos = visita.TecnicosDetalle.Select(t => new VisitaTecnicoModel
        {
            CodigoTecnico = t.CodigoTecnico, Iniciales = t.Iniciales
        }).ToList()
    };
}

// ── Importador de Google Calendar ────────────────────────────────────────────────

public sealed class EventoGoogleModel
{
    public string? EventoId { get; set; }
    public string? CalendarId { get; set; }
    public string? Summary { get; set; }
    public string? Ubicacion { get; set; }
    public string? Descripcion { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}

public sealed class ImportacionGoogleResumenModel
{
    public Guid LoteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int TotalEventos { get; set; }
    public int Listos { get; set; }
    public int Conflictos { get; set; }
}

public sealed class ImportacionGoogleEventoModel
{
    public Guid Id { get; set; }
    public Guid LoteId { get; set; }
    public string EventoId { get; set; } = string.Empty;
    public string? CalendarId { get; set; }
    public string? Summary { get; set; }
    public string? Ubicacion { get; set; }
    public string? Descripcion { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? ClienteParseado { get; set; }
    public string? LocacionParseada { get; set; }
    public string? TipoServicioParseado { get; set; }
    public string? VehiculoParseado { get; set; }
    public string? TecnicosParseados { get; set; }
    public string? ClienteCodigo { get; set; }
    public string? LocacionCodigo { get; set; }
    public string? TipoServicioCodigo { get; set; }
    public string? VehiculoCodigo { get; set; }
    public string? TecnicosCodigos { get; set; }
    public string? Nota { get; set; }
    public long? Plantilla { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public string? Error { get; set; }
    public long? IdVisita { get; set; }
    public DateTime? ProcesadoUtc { get; set; }
}

public sealed class CorregirEventoGoogleModel
{
    public string? ClienteCodigo { get; set; }
    public string? LocacionCodigo { get; set; }
    public string? TipoServicioCodigo { get; set; }
    public string? VehiculoCodigo { get; set; }
    public string? TecnicosCodigos { get; set; }
    public string? Nota { get; set; }
    public long? Plantilla { get; set; }

    public static CorregirEventoGoogleModel Desde(ImportacionGoogleEventoModel e) => new()
    {
        ClienteCodigo = e.ClienteCodigo,
        LocacionCodigo = e.LocacionCodigo,
        TipoServicioCodigo = e.TipoServicioCodigo,
        VehiculoCodigo = e.VehiculoCodigo,
        TecnicosCodigos = e.TecnicosCodigos,
        Nota = e.Nota,
        Plantilla = e.Plantilla
    };
}

public sealed class MapeoEventoGoogleModel
{
    public Guid Id { get; set; }
    public string Campo { get; set; } = string.Empty;
    public string ValorOrigen { get; set; } = string.Empty;
    public string ValorDestino { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}
