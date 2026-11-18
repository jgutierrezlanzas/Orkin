using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Ordenes;
using Orkin.Cotizador.Core.Application.Requisas;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Requisas (Fase 5) sobre las tres tablas <b>legacy existentes</b> (D-22). Se leen y se editan;
/// el alta reutiliza el flujo de la orden de trabajo y la anulación reutiliza su SP. No se copian
/// catálogos ni se crean tablas paralelas (D-20/D-21). Aprobación y PDF son fases posteriores.
/// </summary>
[ApiController]
[Route("api/requisas")]
[Produces("application/json")]
[Authorize("REQUISAS.VER")]
public sealed class RequisasController : ControllerBase
{
    private readonly RequisaService _servicio;

    public RequisasController(RequisaService servicio)
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
        return ApiResponse<IReadOnlyList<RequisaResumenResponse>>.Ok(
            resumen.Select(AResumen).ToList()).AHttp();
    }

    [HttpGet("{numero}")]
    public async Task<IResult> Obtener(string numero, CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerAsync(numero, cancellationToken);

        return entidad is null
            ? ApiResponse<RequisaResponse>.Error("NO_ENCONTRADO", "La requisa no existe.").AHttp()
            : ApiResponse<RequisaResponse>.Ok(AResponse(entidad)).AHttp();
    }

    /// <summary>
    /// «Nueva desde orden»: no regenera la requisa por su cuenta. Delega en el mismo servicio que
    /// la Fase 4 usa desde la orden, así que vuelve a correr <c>SINCO_GENERA_REQUISA</c> una sola
    /// vez y con sus validaciones originales.
    /// </summary>
    [HttpPost("desde-orden/{ordenNumero}/{idVisita:long}/{plantilla:long}")]
    [Authorize("REQUISAS.EDITAR")]
    public Task<IResult> CrearDesdeOrden(
        string ordenNumero,
        long idVisita,
        long plantilla,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () =>
        {
            var requisa = await _servicio.CrearDesdeOrdenAsync(
                new OrdenClave(ordenNumero, idVisita, plantilla), cancellationToken);
            return ApiResponse<RequisaResponse>.Ok(AResponse(requisa)).AHttp();
        });

    [HttpPut("{numero}")]
    [Authorize("REQUISAS.EDITAR")]
    public Task<IResult> Actualizar(
        string numero,
        [FromBody] RequisaGuardarRequest request,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () =>
        {
            var entidad = await _servicio.ActualizarAsync(numero, AGuardar(request), cancellationToken);
            return ApiResponse<RequisaResponse>.Ok(AResponse(entidad)).AHttp();
        });

    [HttpPost("{numero}/anular")]
    [Authorize("REQUISAS.ANULAR")]
    public Task<IResult> Anular(string numero, CancellationToken cancellationToken)
        => EjecutarAsync(async () =>
        {
            await _servicio.AnularAsync(numero, cancellationToken);
            return ApiResponse<object>.SinContenido().AHttpSinCuerpo();
        });

    /// <summary>Las reglas del legacy llegan como <see cref="InvalidOperationException"/> con su
    /// mensaje exacto (RAISERROR incluido); acá solo se les pone el sobre de la API.</summary>
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

    private static RequisaResumenResponse AResumen(RequisaResumen r) => new(
        r.Cabecera.RequisaNumero,
        r.Cabecera.OrdenNumero,
        r.Cabecera.Cliente,
        r.ClienteNombre,
        r.Cabecera.Locacion,
        r.LocacionNombre,
        r.Cabecera.FechaRequisa,
        r.Cabecera.FechaOrden,
        r.Cabecera.Estado,
        r.Bodega,
        r.Cabecera.Instrucciones,
        r.Lineas,
        r.Tecnicos);

    private static RequisaResponse AResponse(RequisaCompleta r) => new(
        r.Cabecera.RequisaNumero,
        r.Cabecera.OrdenNumero,
        r.Cabecera.Cliente,
        r.ClienteNombre,
        r.Cabecera.Locacion,
        r.LocacionNombre,
        r.Cabecera.FechaRequisa,
        r.Cabecera.FechaOrden,
        r.Cabecera.Instrucciones,
        r.Cabecera.RequisaPendiente,
        r.Cabecera.Estado,
        r.Cabecera.FechaCreacion,
        r.Cabecera.UsuarioCreacion,
        r.Bodega,
        r.Lineas.OrderBy(d => d.Consecutivo).Select(d => new RequisaLineaResponse(
            d.Consecutivo,
            d.Bodega,
            d.CodigoArticulo,
            d.DescripArticulo,
            d.Umedida,
            d.CantidadEntregada,
            d.CantidadUtilizada,
            d.CantidadDevuelta,
            d.CantidadSobfal,
            d.CostoUnitario,
            d.CostoTotal,
            d.CentroCosto,
            d.CuentaContable)).ToList(),
        r.Tecnicos.OrderBy(t => t.Consecutivo).Select(t => new RequisaTecnicoResponse(
            t.Consecutivo,
            t.CodigoTecnico,
            t.Iniciales,
            r.NombresTecnico.GetValueOrDefault(t.CodigoTecnico))).ToList());

    private static RequisaGuardar AGuardar(RequisaGuardarRequest r) => new(
        r.Cliente,
        r.FechaRequisa,
        r.FechaOrden,
        r.Locacion,
        r.Instrucciones,
        r.Bodega,
        r.Lineas.Select(d => new RequisaDetalle
        {
            Consecutivo = d.Consecutivo,
            CodigoArticulo = d.CodigoArticulo,
            DescripArticulo = d.DescripcionArticulo,
            CantidadEntregada = d.CantidadEntregada,
            CantidadUtilizada = d.CantidadUtilizada,
            CantidadDevuelta = d.CantidadDevuelta,
            CostoUnitario = d.CostoUnitario,
            CentroCosto = d.CentroCosto,
            CuentaContable = d.CuentaContable
        }).ToList(),
        r.Tecnicos.Select(t => new RequisaTecnico
        {
            Consecutivo = t.Consecutivo,
            CodigoTecnico = t.CodigoTecnico,
            Iniciales = t.Iniciales
        }).ToList());
}
