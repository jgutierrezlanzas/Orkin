namespace Orkin.Cotizador.Api.DTOs;

/// <summary>
/// Contrato de la API para la agenda propia. Las visitas viven en las tablas legacy existentes
/// (D-22); no se inventa un <c>Id</c> nuevo: la clave es <c>ID_VISITA</c> y el evento se identifica
/// por <c>ID_CALENDARIO</c>.
/// </summary>

public sealed record VisitaTecnicoResponse(
    long IdVisita,
    int Linea,
    string? CodigoTecnico,
    string? Iniciales);

public sealed record VisitaResponse(
    long IdVisita,
    string IdCalendario,
    string? Cliente,
    string? ClienteNombre,
    string? Locacion,
    string? TipoServicio,
    DateOnly? FechaInicio,
    TimeSpan? HoraInicio,
    DateOnly? FechaFinal,
    TimeSpan? HoraFinal,
    string? CodigoVehiculo,
    int? CantidadTecnicos,
    string? Nota,
    long? Plantilla,
    string? DescripTrabajo,
    string? OrdenNumero,
    IReadOnlyList<VisitaTecnicoResponse> Tecnicos,
    IReadOnlyList<long> Calendarios);

/// <summary>Fila de la grilla/calendario: sin cargar el detalle de técnicos.</summary>
public sealed record VisitaResumenResponse(
    long IdVisita,
    string IdCalendario,
    string? Cliente,
    string? ClienteNombre,
    string? Locacion,
    string? TipoServicio,
    DateOnly? FechaInicio,
    TimeSpan? HoraInicio,
    DateOnly? FechaFinal,
    TimeSpan? HoraFinal,
    string? CodigoVehiculo,
    int Tecnicos,
    IReadOnlyList<string> CodigosTecnico,
    string? Nota,
    long? Plantilla,
    string? DescripTrabajo,
    string? OrdenNumero);

public sealed record VisitaTecnicoRequest(string? CodigoTecnico, string? Iniciales);

public sealed record VisitaGuardarRequest(
    string? IdCalendario,
    string? Cliente,
    string? Locacion,
    string? TipoServicio,
    DateOnly? FechaInicio,
    TimeSpan? HoraInicio,
    DateOnly? FechaFinal,
    TimeSpan? HoraFinal,
    string? CodigoVehiculo,
    string? Nota,
    long? Plantilla,
    IReadOnlyList<VisitaTecnicoRequest> Tecnicos);

public sealed record GenerarOrdenResponse(long IdVisita, string? OrdenNumero);
