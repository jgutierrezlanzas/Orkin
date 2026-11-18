using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Visitas;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Importador de eventos de Google Calendar hacia la agenda propia (Fase 3). El evento se
/// revisa en staging (<c>int.ImportacionGoogleEvento</c>) y solo se confirma en las tablas legacy
/// cuando el usuario lo aprueba; la homologación es configurable en
/// <c>int.ErpMapeoEventoGoogle</c>, visible en grilla y nunca como JSON crudo.
/// </summary>
[ApiController]
[Route("api/visitas/importacion")]
[Produces("application/json")]
[Authorize("VISITAS.IMPORTAR")]
public sealed class ImportacionGoogleController : ControllerBase
{
    private readonly ImportadorGoogleService _servicio;

    public ImportacionGoogleController(ImportadorGoogleService servicio)
    {
        _servicio = servicio;
    }

    [HttpPost]
    public async Task<IResult> Importar(
        [FromBody] ImportarGoogleRequest request,
        CancellationToken cancellationToken)
    {
        var datos = new ImportacionGoogleRequest(
            request.Nombre,
            request.Origen,
            request.Eventos.Select(e => new EventoGoogleImportar(
                e.EventoId, e.CalendarId, e.Summary, e.Ubicacion, e.Descripcion,
                e.FechaInicio, e.FechaFin)).ToList());
        var resumen = await _servicio.ImportarAsync(datos, cancellationToken);
        return ApiResponse<ImportacionGoogleResumenResponse>.Ok(AResumen(resumen)).AHttp();
    }

    [HttpGet("lotes")]
    public async Task<IResult> ListarLotes(CancellationToken cancellationToken)
    {
        var lotes = await _servicio.ListarLotesAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<ImportacionGoogleLoteResponse>>.Ok(
            lotes.Select(ALote).ToList()).AHttp();
    }

    [HttpGet("eventos")]
    public async Task<IResult> ListarEventos(
        [FromQuery] Guid? loteId,
        [FromQuery] string? estado,
        CancellationToken cancellationToken)
    {
        var eventos = await _servicio.ListarEventosAsync(loteId, estado, cancellationToken);
        return ApiResponse<IReadOnlyList<ImportacionGoogleEventoResponse>>.Ok(
            eventos.Select(AEvento).ToList()).AHttp();
    }

    [HttpGet("eventos/{id:guid}")]
    public async Task<IResult> ObtenerEvento(Guid id, CancellationToken cancellationToken)
    {
        var evento = await _servicio.ObtenerEventoAsync(id, cancellationToken);
        if (evento is null)
        {
            return ApiResponse<ImportacionGoogleEventoResponse>
                .Error("EVENTO_NO_ENCONTRADO", "El evento no existe.").AHttp();
        }

        return ApiResponse<ImportacionGoogleEventoResponse>.Ok(AEvento(evento)).AHttp();
    }

    [HttpPut("eventos/{id:guid}")]
    public async Task<IResult> Corregir(
        Guid id,
        [FromBody] CorregirEventoGoogleRequest request,
        CancellationToken cancellationToken)
    {
        var evento = await _servicio.CorregirAsync(id, new CorregirEventoGoogleDatos(
            request.ClienteCodigo, request.LocacionCodigo, request.TipoServicioCodigo,
            request.VehiculoCodigo, request.TecnicosCodigos, request.Nota, request.Plantilla),
            cancellationToken);

        if (evento is null)
        {
            return ApiResponse<ImportacionGoogleEventoResponse>
                .Error("EVENTO_NO_ENCONTRADO", "El evento no existe.").AHttp();
        }

        return ApiResponse<ImportacionGoogleEventoResponse>.Ok(AEvento(evento)).AHttp();
    }

    [HttpDelete("eventos/{id:guid}")]
    public async Task<IResult> Descartar(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _servicio.DescartarAsync(id, cancellationToken);
        return ok
            ? ApiResponse<object>.SinContenido().AHttpSinCuerpo()
            : ApiResponse<object>.Error("EVENTO_NO_ENCONTRADO", "El evento no existe.").AHttp();
    }

    [HttpPost("eventos/{id:guid}/confirmar")]
    [Authorize("VISITAS.EDITAR")]
    public async Task<IResult> Confirmar(Guid id, CancellationToken cancellationToken)
    {
        var idVisita = await _servicio.ConfirmarAsync(id, cancellationToken);
        return ApiResponse<ConfirmarEventoGoogleResponse>.Ok(new(idVisita)).AHttp();
    }

    [HttpGet("homologacion")]
    public async Task<IResult> ListarMapeos(
        [FromQuery] bool soloActivos = false,
        CancellationToken cancellationToken = default)
    {
        var mapeos = await _servicio.ListarMapeosAsync(soloActivos, cancellationToken);
        return ApiResponse<IReadOnlyList<MapeoEventoGoogleResponse>>.Ok(
            mapeos.Select(AMapeo).ToList()).AHttp();
    }

    [HttpPost("homologacion")]
    public async Task<IResult> GuardarMapeo(
        [FromBody] MapeoEventoGoogleRequest request,
        CancellationToken cancellationToken)
    {
        var mapeo = await _servicio.GuardarMapeoAsync(new MapeoEventoGoogleGuardar(
            request.Campo, request.ValorOrigen, request.ValorDestino,
            request.Descripcion, request.Activo), cancellationToken: cancellationToken);
        return ApiResponse<MapeoEventoGoogleResponse>.Ok(AMapeo(mapeo)).AHttp();
    }

    [HttpPut("homologacion/{id:guid}")]
    public async Task<IResult> ActualizarMapeo(
        Guid id,
        [FromBody] MapeoEventoGoogleRequest request,
        CancellationToken cancellationToken)
    {
        var mapeo = await _servicio.GuardarMapeoAsync(new MapeoEventoGoogleGuardar(
            request.Campo, request.ValorOrigen, request.ValorDestino,
            request.Descripcion, request.Activo), id, cancellationToken);
        return ApiResponse<MapeoEventoGoogleResponse>.Ok(AMapeo(mapeo)).AHttp();
    }

    [HttpDelete("homologacion/{id:guid}")]
    public async Task<IResult> EliminarMapeo(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _servicio.EliminarMapeoAsync(id, cancellationToken);
        return ok
            ? ApiResponse<object>.SinContenido().AHttpSinCuerpo()
            : ApiResponse<object>.Error("MAPEO_NO_ENCONTRADO", "La homologación no existe.").AHttp();
    }

    // ── Mapas ────────────────────────────────────────────────────────────────────

    private static ImportacionGoogleResumenResponse AResumen(ImportacionGoogleResumen r)
        => new(r.LoteId, r.Nombre, r.TotalEventos, r.Listos, r.Conflictos);

    private static ImportacionGoogleLoteResponse ALote(ImportacionGoogleLote l)
        => new(l.Id, l.Nombre, l.Origen, l.FechaImportacionUtc, l.Estado,
            l.TotalEventos, l.Confirmados, l.Descartados, l.ConError, l.Observaciones);

    private static ImportacionGoogleEventoResponse AEvento(ImportacionGoogleEvento e)
        => new(e.Id, e.LoteId, e.EventoId, e.CalendarId, e.Summary, e.Ubicacion, e.Descripcion,
            e.FechaInicio, e.FechaFin, e.ClienteParseado, e.LocacionParseada,
            e.TipoServicioParseado, e.VehiculoParseado, e.TecnicosParseados,
            e.ClienteCodigo, e.LocacionCodigo, e.TipoServicioCodigo, e.VehiculoCodigo,
            e.TecnicosCodigos, e.Nota, e.Plantilla, e.Estado, e.Error, e.IdVisita, e.ProcesadoUtc);

    private static MapeoEventoGoogleResponse AMapeo(ErpMapeoEventoGoogle m)
        => new(m.Id, m.Campo, m.ValorOrigen, m.ValorDestino, m.Descripcion, m.Activo);
}
