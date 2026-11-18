using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Plantillas;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Plantillas de orden de trabajo (D-22). La API expone las tablas <b>legacy existentes</b>:
/// <c>Plantilla_Orden_Trabajo</c>, <c>_Detalle</c>, <c>_Servicio</c> y <c>_Equipos</c>. No hay
/// tablas paralelas ni importación: los 3.503 registros reales se gestionan donde siempre estuvieron.
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
        [FromQuery] string? cliente,
        [FromQuery] bool soloActivas = true,
        CancellationToken cancellationToken = default)
    {
        var resumen = await _servicio.ListarAsync(texto, cliente, soloActivas, cancellationToken);
        var dtos = resumen.Select(AResumen).ToList();
        return ApiResponse<IReadOnlyList<PlantillaResumenResponse>>.Ok(dtos).AHttp();
    }

    [HttpGet("{plantilla:long}")]
    public async Task<IResult> Obtener(long plantilla, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerAsync(plantilla, cancellationToken);

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
        var entidad = await _servicio.CrearAsync(AGuardar(request, activo: true), cancellationToken);
        return ApiResponse<PlantillaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPut("{plantilla:long}")]
    [Authorize("PLANTILLAS.EDITAR")]
    public async Task<IResult> Actualizar(
        long plantilla,
        [FromBody] ActualizarPlantillaRequest request,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ActualizarAsync(
            plantilla, AGuardar(request, request.Activo), request.Activo, cancellationToken);
        return ApiResponse<PlantillaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost("{plantilla:long}/copiar")]
    [Authorize("PLANTILLAS.COPIAR")]
    public async Task<IResult> Copiar(
        long plantilla,
        [FromBody] CopiarPlantillaRequest request,
        CancellationToken cancellationToken)
    {
        var copia = await _servicio.CopiarAsync(plantilla, request.DescripcionTrabajo, cancellationToken);
        return ApiResponse<PlantillaResponse>.Ok(AResponse(copia)).AHttp();
    }

    [HttpDelete("{plantilla:long}")]
    [Authorize("PLANTILLAS.ELIMINAR")]
    public async Task<IResult> Eliminar(long plantilla, CancellationToken cancellationToken)
    {
        await _servicio.DarDeBajaAsync(plantilla, cancellationToken);
        return ApiResponse<object>.SinContenido().AHttpSinCuerpo();
    }

    // ── Mapas ────────────────────────────────────────────────────────────────────

    private static PlantillaResumenResponse AResumen(PlantillaResumen r) => new(
        r.Cabecera.Plantilla,
        r.Cabecera.Cliente,
        r.ClienteNombre,
        r.Cabecera.Locacion,
        r.Cabecera.Direccion,
        r.Cabecera.DescripTrabajo,
        r.Cabecera.Activo,
        r.Cabecera.FechaCreacion,
        r.Cabecera.UsuarioCreacion,
        r.Detalles,
        r.Servicios,
        r.Equipos);

    private static PlantillaResponse AResponse(PlantillaCompleta p) => new(
        p.Cabecera.Plantilla,
        p.Cabecera.Cliente,
        p.ClienteNombre,
        p.Cabecera.Locacion,
        p.Cabecera.Direccion,
        p.Cabecera.Tecnico,
        p.Cabecera.Contacto,
        p.Cabecera.Telefono,
        p.Cabecera.Vehiculo,
        p.Cabecera.DescripTrabajo,
        p.Cabecera.Notas,
        p.Cabecera.Activo,
        p.Cabecera.FechaCreacion,
        p.Cabecera.UsuarioCreacion,
        p.Detalles.Count,
        p.Servicios.Count,
        p.Equipos.Count,
        p.Detalles.OrderBy(d => d.Consecutivo).Select(d => new PlantillaDetalleResponse(
            d.Plantilla,
            d.Consecutivo,
            d.CodigoArticulo,
            d.DescripArticulo,
            d.TipoPlaga,
            d.LugarTratar,
            d.Area,
            d.Concentracion,
            d.CodigoDosificacion,
            d.MetodoAplicacion,
            d.Cantidad,
            d.Umedida,
            d.CostoUnitario,
            d.CostoTotal,
            d.MaximoPermitido)).ToList(),
        p.Servicios.OrderBy(s => s.Consecutivo).Select(s => new PlantillaServicioResponse(
            s.Plantilla,
            s.Consecutivo,
            s.CodigoServicio,
            s.CodigoFrecuencia)).ToList(),
        p.Equipos.OrderBy(e => e.Consecutivo).Select(e => new PlantillaEquipoResponse(
            e.Plantilla,
            e.Consecutivo,
            e.CodigoEquipo,
            e.Utilizado)).ToList());

    private static PlantillaGuardar AGuardar(CrearPlantillaRequest r, bool activo) => new(
        r.Cliente,
        r.Locacion,
        r.Direccion,
        r.Tecnico,
        r.Contacto,
        r.Telefono,
        r.Vehiculo,
        r.DescripcionTrabajo,
        r.Notas,
        activo ? "SI" : "NO",
        r.Detalles.Select(d => new PlantillaOrdenTrabajoDetalle
        {
            CodigoArticulo = d.CodigoArticulo,
            DescripArticulo = d.DescripArticulo,
            TipoPlaga = d.TipoPlaga,
            LugarTratar = d.LugarTratar,
            Area = d.Area,
            Concentracion = d.Concentracion,
            CodigoDosificacion = d.CodigoDosificacion,
            MetodoAplicacion = d.MetodoAplicacion,
            Cantidad = d.Cantidad,
            Umedida = d.Umedida,
            CostoUnitario = d.CostoUnitario,
            MaximoPermitido = d.MaximoPermitido
        }).ToList(),
        r.Servicios.Select(s => new PlantillaOrdenTrabajoServicio
        {
            CodigoServicio = s.CodigoServicio,
            CodigoFrecuencia = s.CodigoFrecuencia
        }).ToList(),
        r.Equipos.Select(e => new PlantillaOrdenTrabajoEquipos
        {
            CodigoEquipo = e.CodigoEquipo,
            Utilizado = e.Utilizado
        }).ToList());

    private static PlantillaGuardar AGuardar(ActualizarPlantillaRequest r, bool activo) => new(
        r.Cliente,
        r.Locacion,
        r.Direccion,
        r.Tecnico,
        r.Contacto,
        r.Telefono,
        r.Vehiculo,
        r.DescripcionTrabajo,
        r.Notas,
        activo ? "SI" : "NO",
        r.Detalles.Select(d => new PlantillaOrdenTrabajoDetalle
        {
            CodigoArticulo = d.CodigoArticulo,
            DescripArticulo = d.DescripArticulo,
            TipoPlaga = d.TipoPlaga,
            LugarTratar = d.LugarTratar,
            Area = d.Area,
            Concentracion = d.Concentracion,
            CodigoDosificacion = d.CodigoDosificacion,
            MetodoAplicacion = d.MetodoAplicacion,
            Cantidad = d.Cantidad,
            Umedida = d.Umedida,
            CostoUnitario = d.CostoUnitario,
            MaximoPermitido = d.MaximoPermitido
        }).ToList(),
        r.Servicios.Select(s => new PlantillaOrdenTrabajoServicio
        {
            CodigoServicio = s.CodigoServicio,
            CodigoFrecuencia = s.CodigoFrecuencia
        }).ToList(),
        r.Equipos.Select(e => new PlantillaOrdenTrabajoEquipos
        {
            CodigoEquipo = e.CodigoEquipo,
            Utilizado = e.Utilizado
        }).ToList());
}
