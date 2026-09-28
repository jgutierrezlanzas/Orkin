using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Controllers;

[ApiController]
[Route("api/catalogos/categorias-producto")]
[Produces("application/json")]
[Authorize("CATALOGOS.VER")]
public sealed class CategoriasProductoController : ControllerBase
{
    private readonly CategoriaProductoService _servicio;

    public CategoriasProductoController(CategoriaProductoService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var entidades = await _servicio.ListarAsync(cancellationToken: cancellationToken);
        var dtos = entidades.Select(AResponse).ToList();
        return ApiResponse<IReadOnlyList<CategoriaProductoResponse>>.Ok(dtos).AHttp();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerPorIdAsync(id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<CategoriaProductoResponse>
                .Error("CATEGORIA_NO_ENCONTRADA", "La categoría no existe.")
                .AHttp();
        }

        return ApiResponse<CategoriaProductoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Crear(
        [FromBody] CrearCategoriaProductoRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.CrearAsync(
            request.Codigo,
            request.Nombre,
            request.Descripcion,
            request.Orden,
            cancellationToken);

        return ApiResponse<CategoriaProductoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarCategoriaProductoRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(
            id,
            request.Nombre,
            request.Descripcion,
            request.Orden,
            request.EsActivo,
            cancellationToken);

        return ApiResponse<CategoriaProductoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    private static CategoriaProductoResponse AResponse(Orkin.Cotizador.Core.Domain.Catalogos.CategoriaProducto c)
        => new(c.Id, c.Codigo, c.Nombre, c.Descripcion, c.Orden, c.IsActive);
}
