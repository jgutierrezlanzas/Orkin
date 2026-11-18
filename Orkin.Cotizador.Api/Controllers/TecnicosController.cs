using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Catálogo compartido de técnicos (D-21). Una sola entidad para el cotizador y para los
/// módulos operativos; el alta manual se usa solo cuando el ERP no tiene ese técnico.
/// </summary>
[ApiController]
[Route("api/catalogos/tecnicos")]
[Produces("application/json")]
[Authorize("CATALOGOS.VER")]
public sealed class TecnicosController : ControllerBase
{
    private readonly TecnicoService _servicio;

    public TecnicosController(TecnicoService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var entidades = await _servicio.ListarAsync(cancellationToken: cancellationToken);
        var dtos = entidades.Select(AResponse).ToList();
        return ApiResponse<IReadOnlyList<TecnicoResponse>>.Ok(dtos).AHttp();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerPorIdAsync(id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<TecnicoResponse>
                .Error("TECNICO_NO_ENCONTRADO", "El técnico no existe.")
                .AHttp();
        }

        return ApiResponse<TecnicoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Crear(
        [FromBody] CrearTecnicoRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.CrearAsync(
            request.Codigo,
            request.Nombre,
            request.Telefono,
            request.Correo,
            request.Cargo,
            request.Orden,
            request.ColorCalendario,
            cancellationToken);

        return ApiResponse<TecnicoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("CATALOGOS.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarTecnicoRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(
            id,
            request.Nombre,
            request.Telefono,
            request.Correo,
            request.Cargo,
            request.Orden,
            request.EsActivo,
            request.ColorCalendario,
            cancellationToken);

        return ApiResponse<TecnicoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    private static TecnicoResponse AResponse(Orkin.Cotizador.Core.Domain.Catalogos.Tecnico t)
        => new(t.Id, t.Codigo, t.Nombre, t.Telefono, t.Correo, t.Cargo, t.Orden, t.IsActive, t.ColorCalendario);
}
