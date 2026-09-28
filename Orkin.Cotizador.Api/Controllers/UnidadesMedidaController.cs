using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// CRUD de unidades de medida. Primer catálogo maestro con el que se prueba el patrón de
/// administración (Fase 2).
/// </summary>
[ApiController]
[Route("api/catalogos/unidades-medida")]
[Produces("application/json")]
[Authorize("CATALOGOS.VER")]
public sealed class UnidadesMedidaController : ControllerBase
{
    private readonly UnidadMedidaService _servicio;

    public UnidadesMedidaController(UnidadMedidaService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var entidades = await _servicio.ListarAsync(cancellationToken: cancellationToken);
        var dtos = entidades.Select(AResponse).ToList();
        return ApiResponse<IReadOnlyList<UnidadMedidaResponse>>.Ok(dtos).AHttp();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerPorIdAsync(id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<UnidadMedidaResponse>
                .Error("UNIDAD_MEDIDA_NO_ENCONTRADA", "La unidad de medida no existe.")
                .AHttp();
        }

        return ApiResponse<UnidadMedidaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Crear(
        [FromBody] CrearUnidadMedidaRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.CrearAsync(
            request.Codigo,
            request.Nombre,
            request.Abreviatura,
            request.PermiteDecimales,
            request.Orden,
            cancellationToken);

        return ApiResponse<UnidadMedidaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarUnidadMedidaRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(
            id,
            request.Nombre,
            request.Abreviatura,
            request.PermiteDecimales,
            request.Orden,
            request.EsActivo,
            cancellationToken);

        return ApiResponse<UnidadMedidaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    private static UnidadMedidaResponse AResponse(Orkin.Cotizador.Core.Domain.Catalogos.UnidadMedida u)
        => new(u.Id, u.Codigo, u.Nombre, u.Abreviatura, u.PermiteDecimales, u.Orden, u.IsActive);
}
