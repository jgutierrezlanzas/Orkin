namespace Orkin.Cotizador.Api.DTOs;

/// <summary>Contrato del importador de Google Calendar. El JSON crudo del evento nunca se expone:
/// cada campo tiene su columna en la vista previa y su homólogo configurable.</summary>

public sealed record EventoGoogleRequest(
    string? EventoId,
    string? CalendarId,
    string? Summary,
    string? Ubicacion,
    string? Descripcion,
    DateTime? FechaInicio,
    DateTime? FechaFin);

public sealed record ImportarGoogleRequest(
    string? Nombre,
    string? Origen,
    IReadOnlyList<EventoGoogleRequest> Eventos);

public sealed record ImportacionGoogleResumenResponse(
    Guid LoteId,
    string Nombre,
    int TotalEventos,
    int Listos,
    int Conflictos);

public sealed record ImportacionGoogleLoteResponse(
    Guid Id,
    string Nombre,
    string? Origen,
    DateTime FechaImportacionUtc,
    string Estado,
    int TotalEventos,
    int Confirmados,
    int Descartados,
    int ConError,
    string? Observaciones);

public sealed record ImportacionGoogleEventoResponse(
    Guid Id,
    Guid LoteId,
    string EventoId,
    string? CalendarId,
    string? Summary,
    string? Ubicacion,
    string? Descripcion,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    string? ClienteParseado,
    string? LocacionParseada,
    string? TipoServicioParseado,
    string? VehiculoParseado,
    string? TecnicosParseados,
    string? ClienteCodigo,
    string? LocacionCodigo,
    string? TipoServicioCodigo,
    string? VehiculoCodigo,
    string? TecnicosCodigos,
    string? Nota,
    long? Plantilla,
    string Estado,
    string? Error,
    long? IdVisita,
    DateTime? ProcesadoUtc);

public sealed record CorregirEventoGoogleRequest(
    string? ClienteCodigo,
    string? LocacionCodigo,
    string? TipoServicioCodigo,
    string? VehiculoCodigo,
    string? TecnicosCodigos,
    string? Nota,
    long? Plantilla);

public sealed record MapeoEventoGoogleResponse(
    Guid Id,
    string Campo,
    string ValorOrigen,
    string ValorDestino,
    string? Descripcion,
    bool Activo);

public sealed record MapeoEventoGoogleRequest(
    string Campo,
    string ValorOrigen,
    string ValorDestino,
    string? Descripcion,
    bool Activo);

public sealed record ConfirmarEventoGoogleResponse(long IdVisita);
