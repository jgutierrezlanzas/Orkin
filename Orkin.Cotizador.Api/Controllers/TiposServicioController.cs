using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Controllers;

[ApiController]
[Route("api/catalogos/tipos-servicio")]
[Produces("application/json")]
[Authorize("CATALOGOS.VER")]
public sealed class TiposServicioController : ControllerBase
{
    private readonly TipoServicioService _servicio;

    public TiposServicioController(TipoServicioService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var entidades = await _servicio.ListarAsync(cancellationToken: cancellationToken);
        var dtos = entidades.Select(AResponse).ToList();
        return ApiResponse<IReadOnlyList<TipoServicioResponse>>.Ok(dtos).AHttp();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerPorIdAsync(id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<TipoServicioResponse>
                .Error("TIPO_SERVICIO_NO_ENCONTRADO", "El tipo de servicio no existe.")
                .AHttp();
        }

        return ApiResponse<TipoServicioResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Crear(
        [FromBody] CrearTipoServicioRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.CrearAsync(
            request.Codigo,
            request.Nombre,
            request.Descripcion,
            request.UnidadMedidaId,
            request.EsResidencialPorDefecto,
            request.EsEmpresarialPorDefecto,
            request.Orden,
            cancellationToken);

        return ApiResponse<TipoServicioResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarTipoServicioRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(
            id,
            request.Nombre,
            request.Descripcion,
            request.UnidadMedidaId,
            request.EsResidencialPorDefecto,
            request.EsEmpresarialPorDefecto,
            request.Orden,
            request.EsActivo,
            cancellationToken);

        return ApiResponse<TipoServicioResponse>.Ok(AResponse(entidad)).AHttp();
    }

    private static TipoServicioResponse AResponse(Orkin.Cotizador.Core.Domain.Catalogos.TipoServicio t)
        => new(
            t.Id,
            t.Codigo,
            t.Nombre,
            t.Descripcion,
            t.UnidadMedidaId,
            t.UnidadMedida?.Nombre,
            t.EsResidencialPorDefecto,
            t.EsEmpresarialPorDefecto,
            t.Orden,
            t.IsActive);
}
