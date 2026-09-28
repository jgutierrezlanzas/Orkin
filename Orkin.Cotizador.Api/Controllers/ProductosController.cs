using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Controllers;

[ApiController]
[Route("api/catalogos/productos")]
[Produces("application/json")]
[Authorize("CATALOGOS.VER")]
public sealed class ProductosController : ControllerBase
{
    private readonly ProductoService _servicio;

    public ProductosController(ProductoService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var entidades = await _servicio.ListarAsync(cancellationToken: cancellationToken);
        var dtos = entidades.Select(AResponse).ToList();
        return ApiResponse<IReadOnlyList<ProductoResponse>>.Ok(dtos).AHttp();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerPorIdAsync(id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<ProductoResponse>
                .Error("PRODUCTO_NO_ENCONTRADO", "El producto no existe.")
                .AHttp();
        }

        return ApiResponse<ProductoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Crear(
        [FromBody] CrearProductoRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.CrearAsync(
            request.Codigo,
            request.Nombre,
            request.Descripcion,
            request.CategoriaId,
            request.UnidadMedidaId,
            request.CostoActual,
            request.PrecioVentaActual,
            request.VidaUtilMeses,
            cancellationToken);

        return ApiResponse<ProductoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarProductoRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(
            id,
            request.Nombre,
            request.Descripcion,
            request.CategoriaId,
            request.UnidadMedidaId,
            request.CostoActual,
            request.PrecioVentaActual,
            request.VidaUtilMeses,
            request.EsActivo,
            cancellationToken);

        return ApiResponse<ProductoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    private static ProductoResponse AResponse(Orkin.Cotizador.Core.Domain.Entidades.Producto p)
        => new(
            p.Id,
            p.Codigo,
            p.Nombre,
            p.Descripcion,
            p.CategoriaId,
            p.Categoria?.Nombre,
            p.UnidadMedidaId,
            p.UnidadMedida?.Nombre ?? string.Empty,
            p.UnidadMedida?.Abreviatura ?? string.Empty,
            p.CostoActual,
            p.PrecioVentaActual,
            p.VidaUtilMeses,
            p.IsActive);
}
