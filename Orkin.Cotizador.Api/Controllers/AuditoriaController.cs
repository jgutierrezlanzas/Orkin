using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Bitácora de auditoría (AUDITORIA.VER). <b>Solo lectura</b>: la tabla es append-only con un
/// trigger que bloquea UPDATE y DELETE, y esta controladora no va a ser la excepción.
/// </summary>
[ApiController]
[Route("api/auditoria")]
[Produces("application/json")]
[Authorize("AUDITORIA.VER")]
public sealed class AuditoriaController : ControllerBase
{
    private readonly CotizadorDbContext _contexto;

    public AuditoriaController(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    [HttpGet]
    public async Task<IResult> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanio = 20,
        [FromQuery] string? entidad = null,
        [FromQuery] Guid? usuarioId = null,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        CancellationToken cancellationToken = default)
    {
        var query = _contexto.RegistroAuditoria.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entidad))
        {
            query = query.Where(a => a.Entidad == entidad);
        }
        if (usuarioId is { } uid)
        {
            query = query.Where(a => a.UsuarioId == uid);
        }
        if (desde is { } d)
        {
            query = query.Where(a => a.FechaUtc >= d);
        }
        if (hasta is { } h)
        {
            query = query.Where(a => a.FechaUtc <= h);
        }

        var total = await query.CountAsync(cancellationToken);

        // La bitácora se lee de lo más reciente a lo más viejo: quien entra a mirar quiere saber
        // qué pasó hace un rato, no el primer insert de la historia.
        var registros = await query
            .OrderByDescending(a => a.FechaUtc)
            .ThenByDescending(a => a.Id)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Join(_contexto.AccionAuditoriaCatalogo.AsNoTracking(),
                a => a.AccionId,
                x => x.Id,
                (a, x) => new { a, Accion = x.Nombre ?? x.Id.ToString() })
            .Select(r => new RegistroAuditoriaResponse(
                r.a.Id,
                r.a.Entidad,
                r.a.EntidadId,
                r.Accion,
                r.a.NombreUsuario,
                r.a.FechaUtc,
                r.a.CamposModificados,
                r.a.ValorAnteriorJson,
                r.a.ValorNuevoJson))
            .ToListAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { items = registros, total, pagina, tamanio }).AHttp();
    }
}
