using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

[ApiController]
[Route("api/catalogos/clientes")]
[Produces("application/json")]
[Authorize("CATALOGOS.VER")]
public sealed class ClientesController : ControllerBase
{
    private readonly ClienteService _servicio;
    private readonly CotizadorDbContext _contexto;

    public ClientesController(ClienteService servicio, CotizadorDbContext contexto)
    {
        _servicio = servicio;
        _contexto = contexto;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var entidades = await _servicio.ListarAsync(cancellationToken: cancellationToken);
        var dtos = entidades.Select(AResponse).ToList();
        return ApiResponse<IReadOnlyList<ClienteResponse>>.Ok(dtos).AHttp();
    }

    [HttpGet("tipos")]
    public async Task<IResult> TiposCliente(CancellationToken cancellationToken)
    {
        var tipos = await _contexto.TipoClienteCatalogo
            .AsNoTracking()
            .Where(t => t.EsActivo)
            .OrderBy(t => t.Orden)
            .Select(t => new TipoClienteOption(t.Id, t.Codigo, t.Nombre))
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyList<TipoClienteOption>>.Ok(tipos).AHttp();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerPorIdAsync(id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<ClienteResponse>
                .Error("CLIENTE_NO_ENCONTRADO", "El cliente no existe.")
                .AHttp();
        }

        return ApiResponse<ClienteResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost]
    [Authorize("CLIENTES.CREAR")]
    public async Task<IResult> Crear(
        [FromBody] CrearClienteRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.CrearAsync(
            request.Codigo,
            request.Nombre,
            request.RazonSocial,
            request.TipoClienteId,
            request.CedulaORuc,
            request.IdentificacionFiscal,
            request.Direccion,
            request.Ciudad,
            request.Departamento,
            request.Telefono,
            request.Correo,
            request.ContactoNombre,
            request.ContactoCargo,
            request.SitioWeb,
            request.Notas,
            cancellationToken);

        return ApiResponse<ClienteResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("CLIENTES.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarClienteRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(
            id,
            request.Nombre,
            request.RazonSocial,
            request.TipoClienteId,
            request.CedulaORuc,
            request.IdentificacionFiscal,
            request.Direccion,
            request.Ciudad,
            request.Departamento,
            request.Telefono,
            request.Correo,
            request.ContactoNombre,
            request.ContactoCargo,
            request.SitioWeb,
            request.Notas,
            request.EsActivo,
            cancellationToken);

        return ApiResponse<ClienteResponse>.Ok(AResponse(entidad)).AHttp();
    }

    private static ClienteResponse AResponse(Orkin.Cotizador.Core.Domain.Entidades.Cliente c)
        => new(
            c.Id,
            c.Codigo,
            c.Nombre,
            c.RazonSocial,
            c.TipoClienteId,
            c.TipoCliente?.Nombre ?? string.Empty,
            c.CedulaORuc,
            c.IdentificacionFiscal,
            c.Direccion,
            c.Ciudad,
            c.Departamento,
            c.Telefono,
            c.Correo,
            c.ContactoNombre,
            c.ContactoCargo,
            c.SitioWeb,
            c.Notas,
            c.IsActive);
}
