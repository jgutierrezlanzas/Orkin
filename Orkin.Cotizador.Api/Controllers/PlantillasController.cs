using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Plantillas;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Plantillas de orden de trabajo (Fase 2). Entidad compuesta: cabecera, productos, servicios y
/// equipos se guardan en una sola transacción. Los catálogos del ERP se envían como códigos texto
/// (D-20); los FK apuntan solo a catálogos compartidos de la plataforma (D-21).
/// </summary>
[ApiController]
[Route("api/plantillas")]
[Produces("application/json")]
[Authorize("PLANTILLAS.VER")]
public sealed class PlantillasController : ControllerBase
{
    private readonly PlantillaService _servicio;

    public PlantillasController(PlantillaService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IResult> Listar(
        [FromQuery] string? texto,
        [FromQuery] Guid? clienteId,
        [FromQuery] Guid? tecnicoId,
        [FromQuery] bool soloActivas = true,
        CancellationToken cancellationToken = default)
    {
        var entidades = await _servicio.ListarAsync(
            texto, clienteId, tecnicoId, soloActivas, cancellationToken);
        var dtos = entidades.Select(AResponse).ToList();
        return ApiResponse<IReadOnlyList<PlantillaResponse>>.Ok(dtos).AHttp();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerPorIdAsync(id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<PlantillaResponse>
                .Error("PLANTILLA_NO_ENCONTRADA", "La plantilla no existe.")
                .AHttp();
        }

        return ApiResponse<PlantillaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost]
    [Authorize("PLANTILLAS.EDITAR")]
    public async Task<IResult> Crear(
        [FromBody] CrearPlantillaRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.CrearAsync(
            request.Codigo,
            request.Nombre,
            request.ClienteId,
            request.LocacionCodigo,
            request.Direccion,
            request.TecnicoId,
            request.Contacto,
            request.Telefono,
            request.VehiculoCodigo,
            request.DescripcionTrabajo,
            request.Notas,
            request.Detalles.Select(ADetalle).ToList(),
            request.Servicios.Select(AServicio).ToList(),
            request.Equipos.Select(AEquipo).ToList(),
            cancellationToken);

        return ApiResponse<PlantillaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("PLANTILLAS.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarPlantillaRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(
            id,
            request.Nombre,
            request.ClienteId,
            request.LocacionCodigo,
            request.Direccion,
            request.TecnicoId,
            request.Contacto,
            request.Telefono,
            request.VehiculoCodigo,
            request.DescripcionTrabajo,
            request.Notas,
            request.EsActivo,
            request.Detalles.Select(ADetalle).ToList(),
            request.Servicios.Select(AServicio).ToList(),
            request.Equipos.Select(AEquipo).ToList(),
            cancellationToken);

        return ApiResponse<PlantillaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost("{id:guid}/copiar")]
    [Authorize("PLANTILLAS.COPIAR")]
    public async Task<IResult> Copiar(
        Guid id,
        [FromBody] CopiarPlantillaRequest request,
        CancellationToken cancellationToken)
    {
        var copia = await _servicio.CopiarAsync(id, request.Codigo, request.Nombre, cancellationToken);
        return ApiResponse<PlantillaResponse>.Ok(AResponse(copia)).AHttp();
    }

    [HttpDelete("{id:guid}")]
    [Authorize("PLANTILLAS.ELIMINAR")]
    public async Task<IResult> Eliminar(Guid id, CancellationToken cancellationToken)
    {
        await _servicio.DarDeBajaAsync(id, cancellationToken);
        return ApiResponse<object>.SinContenido().AHttpSinCuerpo();
    }

    // ── Mapas ────────────────────────────────────────────────────────────────────

    private static PlantillaResponse AResponse(Plantilla p) => new(
        p.Id,
        p.Codigo,
        p.Nombre,
        p.ClienteId,
        p.Cliente?.Nombre,
        p.LocacionCodigo,
        p.Direccion,
        p.TecnicoId,
        p.Tecnico?.Nombre,
        p.Contacto,
        p.Telefono,
        p.VehiculoCodigo,
        p.DescripcionTrabajo,
        p.Notas,
        p.IsActive,
        p.Detalles.OrderBy(d => d.Consecutivo).Select(d => new PlantillaDetalleResponse(
            d.Id,
            d.Consecutivo,
            d.ProductoId,
            d.Producto?.Nombre,
            d.DescripcionArticulo,
            d.TipoPlagaCodigo,
            d.LugarTratarCodigo,
            d.Area,
            d.Concentracion,
            d.DosificacionCodigo,
            d.MetodoAplicacionCodigo,
            d.Cantidad,
            d.UnidadMedidaId,
            d.UnidadMedida?.Abreviatura,
            d.CostoUnitario,
            d.CostoTotal,
            d.MaximoPermitido)).ToList(),
        p.Servicios.OrderBy(s => s.Consecutivo).Select(s => new PlantillaServicioResponse(
            s.Id,
            s.Consecutivo,
            s.TipoServicioId,
            s.TipoServicio?.Nombre,
            s.FrecuenciaCodigo)).ToList(),
        p.Equipos.OrderBy(e => e.Consecutivo).Select(e => new PlantillaEquipoResponse(
            e.Id,
            e.Consecutivo,
            e.EquipoCodigo,
            e.Utilizado)).ToList());

    private static PlantillaDetalle ADetalle(PlantillaDetalleGuardarRequest r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        Consecutivo = r.Consecutivo,
        ProductoId = r.ProductoId,
        DescripcionArticulo = r.DescripcionArticulo,
        TipoPlagaCodigo = r.TipoPlagaCodigo,
        LugarTratarCodigo = r.LugarTratarCodigo,
        Area = r.Area,
        Concentracion = r.Concentracion,
        DosificacionCodigo = r.DosificacionCodigo,
        MetodoAplicacionCodigo = r.MetodoAplicacionCodigo,
        Cantidad = r.Cantidad,
        UnidadMedidaId = r.UnidadMedidaId,
        CostoUnitario = r.CostoUnitario,
        CostoTotal = r.CostoTotal,
        MaximoPermitido = r.MaximoPermitido
    };

    private static PlantillaServicio AServicio(PlantillaServicioGuardarRequest r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        Consecutivo = r.Consecutivo,
        TipoServicioId = r.TipoServicioId,
        FrecuenciaCodigo = r.FrecuenciaCodigo
    };

    private static PlantillaEquipo AEquipo(PlantillaEquipoGuardarRequest r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        Consecutivo = r.Consecutivo,
        EquipoCodigo = r.EquipoCodigo,
        Utilizado = r.Utilizado
    };
}
