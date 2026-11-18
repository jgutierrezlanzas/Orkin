using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Ordenes;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Órdenes de trabajo (Fase 4) sobre las cinco tablas <b>legacy existentes</b> (D-22). El alta
/// reutiliza <c>SINCO_GENERA_ORDENE_DE_TRABAJO</c> desde una visita confirmada; la anulación y la
/// requisa reutilizan sus SPs. No se crean tablas paralelas ni se copian catálogos (D-20).
/// </summary>
[ApiController]
[Route("api/ordenes")]
[Produces("application/json")]
[Authorize("OT.VER")]
public sealed class OrdenesController : ControllerBase
{
    private readonly OrdenTrabajoService _servicio;

    public OrdenesController(OrdenTrabajoService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IResult> Listar(
        [FromQuery] string? texto,
        [FromQuery] string? cliente,
        [FromQuery] string? estado,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int limite = 200,
        CancellationToken cancellationToken = default)
    {
        var resumen = await _servicio.ListarAsync(
            texto, cliente, estado, desde, hasta, limite, cancellationToken);
        return ApiResponse<IReadOnlyList<OrdenResumenResponse>>.Ok(
            resumen.Select(AResumen).ToList()).AHttp();
    }

    [HttpGet("{ordenNumero}/{idVisita:long}/{plantilla:long}")]
    public async Task<IResult> Obtener(
        string ordenNumero,
        long idVisita,
        long plantilla,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerAsync(
            new OrdenClave(ordenNumero, idVisita, plantilla), cancellationToken);

        return entidad is null
            ? ApiResponse<OrdenResponse>.Error("NO_ENCONTRADO", "La orden no existe.").AHttp()
            : ApiResponse<OrdenResponse>.Ok(AResponse(entidad)).AHttp();
    }

    [HttpPost("desde-visita/{idVisita:long}")]
    [Authorize("OT.EDITAR")]
    public Task<IResult> CrearDesdeVisita(
        long idVisita,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () =>
        {
            var orden = await _servicio.CrearDesdeVisitaAsync(idVisita, cancellationToken);
            return ApiResponse<OrdenResponse>.Ok(AResponse(orden)).AHttp();
        });

    [HttpPut("{ordenNumero}/{idVisita:long}/{plantilla:long}")]
    [Authorize("OT.EDITAR")]
    public Task<IResult> Actualizar(
        string ordenNumero,
        long idVisita,
        long plantilla,
        [FromBody] OrdenGuardarRequest request,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () =>
        {
            var entidad = await _servicio.ActualizarAsync(
                new OrdenClave(ordenNumero, idVisita, plantilla),
                AGuardar(request),
                cancellationToken);
            return ApiResponse<OrdenResponse>.Ok(AResponse(entidad)).AHttp();
        });

    [HttpPost("{ordenNumero}/{idVisita:long}/{plantilla:long}/anular")]
    [Authorize("OT.ANULAR")]
    public Task<IResult> Anular(
        string ordenNumero,
        long idVisita,
        long plantilla,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () =>
        {
            await _servicio.AnularAsync(
                new OrdenClave(ordenNumero, idVisita, plantilla), cancellationToken);
            return ApiResponse<object>.SinContenido().AHttpSinCuerpo();
        });

    [HttpPost("{ordenNumero}/{idVisita:long}/{plantilla:long}/requisa")]
    [Authorize("OT.APROBAR")]
    public Task<IResult> GenerarRequisa(
        string ordenNumero,
        long idVisita,
        long plantilla,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () =>
        {
            await _servicio.GenerarRequisaAsync(
                new OrdenClave(ordenNumero, idVisita, plantilla), cancellationToken);
            return ApiResponse<object>.SinContenido().AHttpSinCuerpo();
        });

    /// <summary>
    /// Las reglas del legacy se informan con <see cref="InvalidOperationException"/> y el mensaje
    /// exacto del WinForms (o del RAISERROR del SP). Acá se convierten en un sobre con código
    /// estable para que la UI lo muestre sin tener que adivinar.
    /// </summary>
    private static async Task<IResult> EjecutarAsync(Func<Task<IResult>> accion)
    {
        try
        {
            return await accion();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<object>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<object>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    private static OrdenResumenResponse AResumen(OrdenResumen r) => new(
        r.Cabecera.OrdenNumero,
        r.Cabecera.IdVisita,
        r.Cabecera.Plantilla,
        r.Cabecera.Cliente,
        r.ClienteNombre,
        r.Cabecera.FechaOrden,
        r.Cabecera.Hora,
        r.Cabecera.Direccion,
        r.Cabecera.DescripTrabajo,
        r.Cabecera.Estado,
        r.Cabecera.ServicioRealizado,
        r.Detalles,
        r.Servicios,
        r.Equipos,
        r.Tecnicos);

    private static OrdenResponse AResponse(OrdenCompleta o) => new(
        o.Cabecera.OrdenNumero,
        o.Cabecera.IdVisita,
        o.Cabecera.Plantilla,
        o.Cabecera.Cliente,
        o.ClienteNombre,
        o.Cabecera.FechaOrden,
        o.Cabecera.Hora,
        o.Cabecera.Direccion,
        o.Cabecera.Contacto,
        o.Cabecera.Telefono,
        o.Cabecera.Vehiculo,
        o.Cabecera.ServicioRealizado,
        o.Cabecera.DescripTrabajo,
        o.Cabecera.Notas,
        o.Cabecera.Instrucciones,
        o.Cabecera.Estado,
        o.Cabecera.FechaCreacion,
        o.Cabecera.UsuarioCreacion,
        o.Detalles.OrderBy(d => d.Consecutivo).Select(d => new OrdenProductoResponse(
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
            d.CostoTotal)).ToList(),
        o.Servicios.OrderBy(s => s.Consecutivo).Select(s => new OrdenServicioResponse(
            s.Plantilla, s.Consecutivo, s.CodigoServicio, s.CodigoFrecuencia)).ToList(),
        o.Equipos.OrderBy(e => e.Consecutivo).Select(e => new OrdenEquipoResponse(
            e.Plantilla, e.Consecutivo, e.CodigoEquipo, e.Utilizado)).ToList(),
        o.Tecnicos.OrderBy(t => t.Consecutivo).Select(t => new OrdenTecnicoResponse(
            t.Plantilla, t.Consecutivo, t.CodigoTecnico, t.Iniciales)).ToList());

    private static OrdenGuardar AGuardar(OrdenGuardarRequest r) => new(
        r.Cliente,
        r.FechaOrden,
        r.Hora,
        r.Direccion,
        r.Contacto,
        r.Telefono,
        r.Vehiculo,
        r.ServicioRealizado,
        r.DescripTrabajo,
        r.Notas,
        r.Instrucciones,
        r.Detalles.Select(d => new OrdenTrabajoDetalle
        {
            Consecutivo = d.Consecutivo,
            CodigoArticulo = d.CodigoArticulo,
            DescripArticulo = d.DescripcionArticulo,
            TipoPlaga = d.TipoPlaga,
            LugarTratar = d.LugarTratar,
            Area = d.Area,
            Concentracion = d.Concentracion,
            CodigoDosificacion = d.CodigoDosificacion,
            MetodoAplicacion = d.MetodoAplicacion,
            Cantidad = d.Cantidad,
            Umedida = d.Umedida,
            CostoUnitario = d.CostoUnitario
        }).ToList(),
        r.Servicios.Select(s => new OrdenTrabajoServicio
        {
            Consecutivo = s.Consecutivo,
            CodigoServicio = s.CodigoServicio,
            CodigoFrecuencia = s.CodigoFrecuencia
        }).ToList(),
        r.Equipos.Select(e => new OrdenTrabajoEquipo
        {
            Consecutivo = e.Consecutivo,
            CodigoEquipo = e.CodigoEquipo,
            Utilizado = e.Utilizado
        }).ToList(),
        r.Tecnicos.Select(t => new OrdenTrabajoTecnico
        {
            Consecutivo = t.Consecutivo,
            CodigoTecnico = t.CodigoTecnico,
            Iniciales = t.Iniciales
        }).ToList());
}
