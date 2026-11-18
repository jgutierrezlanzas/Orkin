using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Visitas;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Agenda propia (Fase 3) sobre las tablas <b>legacy existentes</b> (D-22):
/// <c>ProgramacionVisitaConfirmadas</c>, <c>ProgramacionVisitaDetalleConfirmada</c> y
/// <c>Plantilla_Calendario</c>. No hay tablas de visitas paralelas ni importación de datos.
/// </summary>
[ApiController]
[Route("api/visitas")]
[Produces("application/json")]
[Authorize("VISITAS.VER")]
public sealed class VisitasController : ControllerBase
{
    private readonly VisitaService _servicio;

    public VisitasController(VisitaService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IResult> Listar(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] string? texto,
        [FromQuery] string? cliente,
        [FromQuery] long? plantilla,
        CancellationToken cancellationToken)
    {
        var resumen = await _servicio.ListarAsync(
            desde, hasta, texto, cliente, plantilla, cancellationToken);
        return ApiResponse<IReadOnlyList<VisitaResumenResponse>>.Ok(
            resumen.Select(AResumen).ToList()).AHttp();
    }

    [HttpGet("{idVisita:long}")]
    public async Task<IResult> Obtener(long idVisita, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerAsync(idVisita, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<VisitaResponse>
                .Error("VISITA_NO_ENCONTRADA", "La visita no existe.")
                .AHttp();
        }

        return ApiResponse<VisitaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost]
    [Authorize("VISITAS.EDITAR")]
    public async Task<IResult> Crear(
        [FromBody] VisitaGuardarRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.CrearAsync(AGuardar(request), cancellationToken);
        return ApiResponse<VisitaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{idVisita:long}")]
    [Authorize("VISITAS.EDITAR")]
    public async Task<IResult> Actualizar(
        long idVisita,
        [FromBody] VisitaGuardarRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(idVisita, AGuardar(request), cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<VisitaResponse>
                .Error("VISITA_NO_ENCONTRADA", "La visita no existe.")
                .AHttp();
        }

        return ApiResponse<VisitaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpDelete("{idVisita:long}")]
    [Authorize("VISITAS.REVERSAR")]
    public async Task<IResult> Reversar(long idVisita, CancellationToken cancellationToken)
    {
        await _servicio.ReversarAsync(idVisita, cancellationToken);
        return ApiResponse<object>.SinContenido().AHttpSinCuerpo();
    }

    [HttpPost("{idVisita:long}/generar-orden")]
    [Authorize("VISITAS.GENERAR_OT")]
    public async Task<IResult> GenerarOrden(long idVisita, CancellationToken cancellationToken)
    {
        var numero = await _servicio.GenerarOrdenTrabajoAsync(idVisita, cancellationToken);
        return ApiResponse<GenerarOrdenResponse>.Ok(new GenerarOrdenResponse(idVisita, numero)).AHttp();
    }

    // ── Mapas ────────────────────────────────────────────────────────────────────

    private static VisitaResumenResponse AResumen(VisitaResumen v) => new(
        v.Visita.IdVisita,
        v.Visita.IdCalendario,
        v.Visita.Cliente,
        v.ClienteNombre,
        v.Visita.Locacion,
        v.Visita.TipoServicio,
        v.Visita.FechaInicio,
        v.Visita.HoraInicio,
        v.Visita.FechaFinal,
        v.Visita.HoraFinal,
        v.Visita.CodigoVehiculo,
        v.Tecnicos,
        v.CodigosTecnico,
        v.Visita.Nota,
        v.Plantilla,
        v.DescripTrabajo,
        v.OrdenNumero);

    private static VisitaResponse AResponse(VisitaCompleta v) => new(
        v.Visita.IdVisita,
        v.Visita.IdCalendario,
        v.Visita.Cliente,
        v.ClienteNombre,
        v.Visita.Locacion,
        v.Visita.TipoServicio,
        v.Visita.FechaInicio,
        v.Visita.HoraInicio,
        v.Visita.FechaFinal,
        v.Visita.HoraFinal,
        v.Visita.CodigoVehiculo,
        v.Visita.CantidadTecnicos,
        v.Visita.Nota,
        v.Plantilla,
        v.DescripTrabajo,
        v.OrdenNumero,
        v.Tecnicos.OrderBy(t => t.Linea).Select(t => new VisitaTecnicoResponse(
            t.IdVisita, t.Linea, t.CodigoTecnico, t.Iniciales)).ToList(),
        v.Calendarios.Select(c => c.Plantilla).ToList());

    private static VisitaGuardar AGuardar(VisitaGuardarRequest r) => new(
        r.IdCalendario,
        r.Cliente,
        r.Locacion,
        r.TipoServicio,
        r.FechaInicio,
        r.HoraInicio,
        r.FechaFinal,
        r.HoraFinal,
        r.CodigoVehiculo,
        r.Nota,
        r.Plantilla,
        r.Tecnicos.Select(t => new VisitaTecnicoGuardar(t.CodigoTecnico, t.Iniciales)).ToList());
}
