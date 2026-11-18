using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Estados del evento en staging. Un evento pasa de <c>PENDIENTE</c> a <c>LISTO</c> cuando se
/// validó contra los catálogos ERP; solo los <c>LISTO</c> se pueden confirmar como visita.
/// Los que fallan quedan como <c>CONFLICTO</c> con el motivo en <c>Error</c>, para corregir la
/// homologación y reintentar sin volver a leer Google Calendar.
/// </summary>
public static class EstadoImportacionGoogle
{
    public const string Pendiente = "PENDIENTE";
    public const string Listo = "LISTO";
    public const string Conflicto = "CONFLICTO";
    public const string Confirmado = "CONFIRMADO";
    public const string Descartado = "DESCARTADO";
}

/// <summary>
/// Homologación configurable de eventos de Google Calendar (Fase 3). El legacy parsea con
/// índices fijos y exige códigos exactos del ERP; cuando el usuario exporta el calendario con
/// otro nombre (p. ej. <c>RESIDENCIAL</c> en vez de <c>C03</c>), la fila aquí traduce el
/// valor antes de validar. Es dato sembrable/editable, no una constante C# (D-21).
/// </summary>
public class ErpMapeoEventoGoogle : BaseEntity
{
    /// <summary>Campo al que aplica: CLIENTE / LOCACION / TIPO_SERVICIO / VEHICULO / TECNICO.</summary>
    public string Campo { get; set; } = null!;

    /// <summary>Texto tal como viene del evento.</summary>
    public string ValorOrigen { get; set; } = null!;

    /// <summary>Código real del ERP (el que espera <c>vv*</c>).</summary>
    public string ValorDestino { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>
/// Lote de importación de Google Calendar. Permite agrupar los eventos leídos en una corrida,
/// ver cuántos se confirmaron y dejar auditoría de quién importó y cuándo (enriquecimiento que
/// el WinForms no tenía: allí el evento se confirmaba directamente sin rastro del original).
/// </summary>
public class ImportacionGoogleLote : BaseEntity
{
    public string Nombre { get; set; } = null!;

    public string? Origen { get; set; }

    public DateTime FechaImportacionUtc { get; set; }

    /// <summary>BORRADOR / PROCESADO. El lote no se borra: es la auditoría de la importación.</summary>
    public string Estado { get; set; } = "BORRADOR";

    public int TotalEventos { get; set; }

    public int Confirmados { get; set; }

    public int Descartados { get; set; }

    public int ConError { get; set; }

    public string? Observaciones { get; set; }
}

/// <summary>
/// Evento de Google Calendar en staging. Guarda el texto original (<c>Summary</c>,
/// <c>Ubicacion</c>, <c>Descripcion</c>) y el resultado del parseo y la homologación, para que el
/// usuario pueda revisar la vista previa y corregirla antes de confirmar. No se expone JSON
/// crudo: cada campo tiene su columna y su grilla.
/// </summary>
public class ImportacionGoogleEvento : BaseEntity
{
    public Guid LoteId { get; set; }

    public ImportacionGoogleLote? Lote { get; set; }

    /// <summary>Id del evento de Google. Al confirmar se convierte en <c>ID_CALENDARIO</c>.</summary>
    public string EventoId { get; set; } = null!;

    /// <summary>Id del calendario de Google (informativo; el legacy no lo persistía).</summary>
    public string? CalendarId { get; set; }

    public string? Summary { get; set; }

    public string? Ubicacion { get; set; }

    public string? Descripcion { get; set; }

    public DateTime? FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    // ── Parseo (texto tal como venía en el evento) ──────────────────────────────

    public string? ClienteParseado { get; set; }

    public string? LocacionParseada { get; set; }

    public string? TipoServicioParseado { get; set; }

    public string? VehiculoParseado { get; set; }

    public string? TecnicosParseados { get; set; }

    public string? Nota { get; set; }

    // ── Homologación / corrección manual (códigos que se escribirán en la visita) ─

    public string? ClienteCodigo { get; set; }

    public string? LocacionCodigo { get; set; }

    public string? TipoServicioCodigo { get; set; }

    public string? VehiculoCodigo { get; set; }

    /// <summary>Iniciales de técnicos resueltas, separadas por coma.</summary>
    public string? TecnicosCodigos { get; set; }

    public long? Plantilla { get; set; }

    /// <summary><c>PENDIENTE</c>/<c>LISTO</c>/<c>CONFLICTO</c>/<c>CONFIRMADO</c>/<c>DESCARTADO</c>.</summary>
    public string Estado { get; set; } = EstadoImportacionGoogle.Pendiente;

    public string? Error { get; set; }

    /// <summary>Visita legacy creada al confirmar este evento (logical FK a <c>ID_VISITA</c>).</summary>
    public long? IdVisita { get; set; }

    public DateTime? ProcesadoUtc { get; set; }
}
