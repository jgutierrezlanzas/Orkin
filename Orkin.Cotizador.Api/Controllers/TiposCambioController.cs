using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

[ApiController]
[Route("api/catalogos/tipos-cambio")]
[Produces("application/json")]
[Authorize("CATALOGOS.VER")]
public sealed class TiposCambioController : ControllerBase
{
    private readonly TipoCambioService _servicio;
    private readonly CotizadorDbContext _contexto;

    public TiposCambioController(TipoCambioService servicio, CotizadorDbContext contexto)
    {
        _servicio = servicio;
        _contexto = contexto;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var entidades = await _servicio.ListarAsync(cancellationToken: cancellationToken);
        var dtos = entidades.Select(AResponse).ToList();
        return ApiResponse<IReadOnlyList<TipoCambioResponse>>.Ok(dtos).AHttp();
    }

    [HttpGet("fuentes")]
    public async Task<IResult> Fuentes(CancellationToken cancellationToken)
    {
        var fuentes = await _contexto.FuenteTipoCambioCatalogo
            .AsNoTracking()
            .Where(f => f.EsActivo)
            .OrderBy(f => f.Orden)
            .Select(f => new FuenteTipoCambioOption(f.Id, f.Codigo, f.Nombre))
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyList<FuenteTipoCambioOption>>.Ok(fuentes).AHttp();
    }

    [HttpGet("vigente")]
    public async Task<IResult> Vigente(DateOnly fecha, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerVigenteAsync(fecha, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<TipoCambioResponse>
                .Error("TIPO_CAMBIO_NO_ENCONTRADO", $"No hay tipo de cambio vigente para {fecha:yyyy-MM-dd}.")
                .AHttp();
        }

        return ApiResponse<TipoCambioResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerPorIdAsync(id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<TipoCambioResponse>
                .Error("TIPO_CAMBIO_NO_ENCONTRADO", "El tipo de cambio no existe.")
                .AHttp();
        }

        return ApiResponse<TipoCambioResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Crear(
        [FromBody] CrearTipoCambioRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.CrearAsync(
            request.Fecha,
            request.Valor,
            request.FuenteId,
            cancellationToken);

        return ApiResponse<TipoCambioResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarTipoCambioRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(
            id,
            request.Fecha,
            request.Valor,
            request.FuenteId,
            request.EsActivo,
            cancellationToken);

        return ApiResponse<TipoCambioResponse>.Ok(AResponse(entidad)).AHttp();
    }

    private static TipoCambioResponse AResponse(Orkin.Cotizador.Core.Domain.Entidades.TipoCambio t)
        => new(
            t.Id,
            t.Fecha,
            t.Valor,
            t.FuenteId,
            t.Fuente?.Nombre ?? string.Empty,
            t.IsActive);
}