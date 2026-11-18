using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Inventario;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Fase 6: documentos de inventario sobre <c>dbo.Documento_Inv</c> y
/// <c>dbo.Documento_Inv_linea</c>. Generación y traslado delegan en los SPs originales a través de
/// <see cref="IExactusService"/>, con parámetros y registro del resultado.
/// </summary>
[ApiController]
[Route("api/inventario/documentos")]
[Produces("application/json")]
[Authorize("INVENTARIO.VER")]
public sealed class InventarioController : ControllerBase
{
    private readonly InventarioService _servicio;

    public InventarioController(InventarioService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Filtro <c>transferido=Todos|SI|NO</c>, igual que el combo del WinForms.</summary>
    [HttpGet]
    public async Task<IResult> Listar(
        [FromQuery] string? texto,
        [FromQuery] string? transferido,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int limite = 200,
        CancellationToken cancellationToken = default)
    {
        var resumen = await _servicio.ListarAsync(
            texto, transferido, desde, hasta, limite, cancellationToken);
        return ApiResponse<IReadOnlyList<DocumentoResumenResponse>>.Ok(
            resumen.Select(AResumen).ToList()).AHttp();
    }

    [HttpGet("{paquete}/{documento}")]
    public async Task<IResult> Obtener(
        string paquete,
        string documento,
        CancellationToken cancellationToken)
    {
        var entidad = await _servicio.ObtenerAsync(paquete, documento, cancellationToken);

        return entidad is null
            ? ApiResponse<DocumentoResponse>.Error("NO_ENCONTRADO", "El documento de inventario no existe.").AHttp()
            : ApiResponse<DocumentoResponse>.Ok(AResponse(entidad)).AHttp();
    }

    /// <summary>Ejecuta <c>SINCO_GENERA_DOCINV_EXACTUS</c> sobre una requisa en <c>GE</c>.</summary>
    [HttpPost("generar-desde-requisa/{requisaNumero}")]
    [Authorize("INVENTARIO.EDITAR")]
    public Task<IResult> GenerarDesdeRequisa(
        string requisaNumero,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () =>
        {
            var resultado = await _servicio.GenerarDesdeRequisaAsync(requisaNumero, cancellationToken);
            return ApiResponse<ExactusResponse>.Ok(AResultado(resultado)).AHttp();
        });

    /// <summary>Ejecuta <c>SINCO_TRASLADAR_DOCINV_EXACTUS</c>; no es reversible.</summary>
    [HttpPost("{paquete}/{documento}/transferir")]
    [Authorize("INVENTARIO.TRANSFERIR")]
    public Task<IResult> Transferir(
        string paquete,
        string documento,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () =>
        {
            var resultado = await _servicio.TransferirAsync(paquete, documento, cancellationToken);
            return ApiResponse<ExactusResponse>.Ok(AResultado(resultado)).AHttp();
        });

    /// <summary>Las reglas del legacy llegan como <see cref="InvalidOperationException"/> con el
    /// texto exacto del <c>RAISERROR</c>; acá solo se les pone el sobre de la API.</summary>
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

    private static DocumentoResumenResponse AResumen(DocumentoResumen r) => new(
        r.Cabecera.PaqueteInventario,
        r.Cabecera.DocumentoInv,
        r.Cabecera.Consecutivo,
        r.Cabecera.OrdenNumero,
        r.Cabecera.RequisaNumero,
        r.Cabecera.Referencia,
        r.Cabecera.FechaDocumento,
        r.Cabecera.FechaHorCreacion,
        r.Cabecera.Transferido,
        r.Bodega,
        r.NombreBodega,
        r.Lineas,
        r.Cabecera.Usuario,
        r.Cabecera.MensajeSistema);

    private static DocumentoResponse AResponse(DocumentoCompleto r) => new(
        r.Cabecera.PaqueteInventario,
        r.Cabecera.DocumentoInv,
        r.Cabecera.Consecutivo,
        r.Cabecera.OrdenNumero,
        r.Cabecera.RequisaNumero,
        r.Cabecera.Referencia,
        r.Cabecera.FechaDocumento,
        r.Cabecera.FechaHorCreacion,
        r.Cabecera.Seleccionado,
        r.Cabecera.Usuario,
        r.Cabecera.MensajeSistema,
        r.Cabecera.UsuarioApro,
        r.Cabecera.FechaHoraAprob,
        r.Cabecera.Aprobado,
        r.Cabecera.Transferido,
        r.Lineas.Select(l => new DocumentoLineaResponse(
            l.Linea,
            l.Articulo,
            l.DescripcionArticulo,
            l.Unidad,
            l.Bodega,
            l.NombreBodega,
            l.BodegaDestino,
            l.NombreBodegaDestino,
            l.Cantidad,
            l.CostoUnitario,
            l.CostoTotalLocal,
            l.CentroCosto,
            l.CuentaContable)).ToList());

    private static ExactusResponse AResultado(ExactusResultado r)
        => new(r.Exitoso, r.Mensaje, r.ResultadoSp);
}
