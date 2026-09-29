using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Integracion;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Orígenes de datos externos (Fase 12): las conexiones al ERP de los clientes y los flujos que
/// llenan nuestros catálogos desde ellas.
/// <para>Todo el grupo exige <c>CATALOGOS.ORIGENES</c>, un permiso aparte de
/// <c>CATALOGOS.EDITAR</c> a propósito: poder corregir un precio de un producto no es lo mismo
/// que poder abrir una conexión a la base de datos de un cliente. El segundo puede leer catálogos
/// completos del ERP y cambiar de dónde viene el dato; el primero no.</para>
/// <para>⚠️ La base del ERP del cliente se lee, nunca se escribe (D-19). La columna
/// <c>EsSoloLectura</c> tiene un <c>CHECK (= 1)</c> en la base.</para>
/// </summary>
[ApiController]
[Route("api/origenes")]
[Produces("application/json")]
[Authorize("CATALOGOS.ORIGENES")]
public sealed class OrigenesController : ControllerBase
{
    private readonly IntegracionService _servicio;
    private readonly CotizadorDbContext _contexto;

    public OrigenesController(IntegracionService servicio, CotizadorDbContext contexto)
    {
        _servicio = servicio;
        _contexto = contexto;
    }

    // ── Conexiones ────────────────────────────────────────────────────────────

    [HttpGet("conexiones")]
    public async Task<IResult> ListarConexiones(CancellationToken cancellationToken)
    {
        var conexiones = await _servicio.ListarConexionesAsync(cancellationToken);

        var flujosPorConexion = await _contexto.ErpFlujo
            .AsNoTracking()
            .GroupBy(f => f.ErpConexionId)
            .Select(g => new { ConexionId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.ConexionId, x => x.Total, cancellationToken);

        var dtos = conexiones
            .Select(c => AResponse(c, flujosPorConexion.GetValueOrDefault(c.Id)))
            .ToList();

        return ApiResponse<IReadOnlyList<ErpConexionResponse>>.Ok(dtos).AHttp();
    }

    [HttpPost("conexiones")]
    public async Task<IResult> CrearConexion(
        [FromBody] CrearErpConexionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.CrearConexionAsync(
                request.Nombre,
                request.Descripcion,
                request.Servidor,
                request.Puerto,
                request.NombreBaseDatos,
                request.Esquema,
                request.Usuario,
                request.Contrasena,
                request.TiempoEsperaSegundos ?? 30,
                cancellationToken);

            return ApiResponse<ErpConexionResponse>.Ok(AResponse(entidad, 0)).AHttp();
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ErpConexionResponse>.Error("VALIDACION", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ErpConexionResponse>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    [HttpPut("conexiones/{id:guid}")]
    public async Task<IResult> ActualizarConexion(
        Guid id,
        [FromBody] ActualizarErpConexionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.ActualizarConexionAsync(
                id,
                request.Descripcion,
                request.Servidor,
                request.Puerto,
                request.NombreBaseDatos,
                request.Esquema,
                request.Usuario,
                request.Contrasena,
                request.TiempoEsperaSegundos,
                cancellationToken);

            var flujos = await _contexto.ErpFlujo.CountAsync(f => f.ErpConexionId == id, cancellationToken);

            return ApiResponse<ErpConexionResponse>.Ok(AResponse(entidad, flujos)).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ErpConexionResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ErpConexionResponse>.Error("VALIDACION", ex.Message).AHttp();
        }
    }

    /// <summary>
    /// Abre la conexión y ejecuta un <c>SELECT 1</c>. Es lo que se corre antes de crear un flujo:
    /// descubrir que la contraseña está mal con la prueba es mejor que descubrirlo con una
    /// sincronización que no cargó nada.
    /// </summary>
    [HttpPost("conexiones/{id:guid}/probar")]
    public async Task<IResult> ProbarConexion(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _servicio.ProbarConexionAsync(id, cancellationToken);

            if (!resultado.Exito)
            {
                // La prueba fallida es un 400 y no un 200 con el fallo adentro: quien llama tiene
                // que poder distinguir "probé y falló" de "probé y funcionó" por el código, y no
                // leyendo un campo booleano que puede venir vacío.
                return ApiResponse<ProbarConexionResponse>
                    .Error("DATO_INVALIDO", resultado.Mensaje)
                    .AHttp();
            }

            var respuesta = new ProbarConexionResponse(
                resultado.Exito,
                resultado.Mensaje,
                resultado.VersionServidor,
                resultado.Milisegundos);

            return ApiResponse<ProbarConexionResponse>.Ok(respuesta).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ProbarConexionResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
    }

    // ── Flujos ────────────────────────────────────────────────────────────────

    [HttpGet("flujos")]
    public async Task<IResult> ListarFlujos(CancellationToken cancellationToken)
    {
        var flujos = await _servicio.ListarFlujosAsync(cancellationToken);
        var nombres = await _contexto.ErpConexion
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Nombre, cancellationToken);

        var dtos = flujos
            .Select(f => AResponse(f, nombres.GetValueOrDefault(f.ErpConexionId, "(conexión eliminada)")))
            .ToList();

        return ApiResponse<IReadOnlyList<ErpFlujoResponse>>.Ok(dtos).AHttp();
    }

    [HttpPost("flujos")]
    public async Task<IResult> CrearFlujo(
        [FromBody] CrearErpFlujoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.CrearFlujoAsync(
                request.ErpConexionId,
                request.Nombre,
                request.Descripcion,
                request.Catalogo,
                request.Origen,
                request.MapeoJson,
                cancellationToken);

            var nombreConexion = await _contexto.ErpConexion
                .Where(c => c.Id == entidad.ErpConexionId)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? "";

            return ApiResponse<ErpFlujoResponse>.Ok(AResponse(entidad, nombreConexion)).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("VALIDACION", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    [HttpPut("flujos/{id:guid}")]
    public async Task<IResult> ActualizarFlujo(
        Guid id,
        [FromBody] ActualizarErpFlujoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.ActualizarFlujoAsync(
                id,
                request.Descripcion,
                request.Catalogo,
                request.Origen,
                request.MapeoJson,
                cancellationToken);

            var nombreConexion = await _contexto.ErpConexion
                .Where(c => c.Id == entidad.ErpConexionId)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? "";

            return ApiResponse<ErpFlujoResponse>.Ok(AResponse(entidad, nombreConexion)).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("VALIDACION", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    [HttpDelete("flujos/{id:guid}")]
    public async Task<IResult> DarDeBajaFlujo(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _servicio.DarDeBajaFlujoAsync(id, cancellationToken);
            return ApiResponse<object>.Ok(new { }).AHttpSinCuerpo();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<object>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
    }

    /// <summary>
    /// Corre el flujo ahora: lee del origen configurado y llena el catálogo.
    /// <para>Devuelve el resultado de la carga (insertados, actualizados, omitidos, rechazados).
    /// Lo que se rechaza no es un fallo de la carga: son filas que no se pudieron transformar, y
    /// la respuesta las trae con su motivo para que seLean.</para>
    /// </summary>
    [HttpPost("flujos/{id:guid}/ejecutar")]
    public async Task<IResult> EjecutarFlujo(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _servicio.EjecutarFlujoAsync(id, cancellationToken);
            return ApiResponse<ResultadoCarga>.Ok(resultado).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ResultadoCarga>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            // Incluye "no hay cargador para este catálogo": es un problema de configuración del
            // flujo, no del servidor, y la respuesta lo dice.
            return ApiResponse<ResultadoCarga>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    /// <summary>Historial de corridas del flujo, de la más reciente a la más vieja.</summary>
    [HttpGet("flujos/{id:guid}/ejecuciones")]
    public async Task<IResult> ListarEjecuciones(
        Guid id,
        [FromQuery] int top = 20,
        CancellationToken cancellationToken = default)
    {
        var ejecuciones = await _servicio.ListarEjecucionesAsync(id, top, cancellationToken);
        var dtos = ejecuciones.Select(AResponse).ToList();

        return ApiResponse<IReadOnlyList<SincronizacionEjecucionResponse>>.Ok(dtos).AHttp();
    }

    // ── Mapeo de entidades a DTOs ─────────────────────────────────────────────

    private static ErpConexionResponse AResponse(ErpConexion c, int totalFlujos) => new(
        c.Id,
        c.Nombre,
        c.Descripcion,
        c.Servidor,
        c.Puerto,
        c.NombreBaseDatos,
        c.Esquema,
        c.Usuario,
        c.EsSoloLectura,
        c.TiempoEsperaSegundos,
        c.ContrasenaCifrada is { Length: > 0 },
        c.UltimaConexionOkUtc is null ? null : true,
        c.UltimaConexionOkUtc,
        c.UltimoError,
        c.UltimoErrorUtc,
        c.IsActive,
        totalFlujos);

    private static ErpFlujoResponse AResponse(ErpFlujo f, string conexionNombre) => new(
        f.Id,
        f.ErpConexionId,
        conexionNombre,
        f.Nombre,
        f.Descripcion,
        f.Catalogo,
        f.Origen,
        f.MapeoJson,
        f.IsActive,
        f.UltimaEjecucionUtc,
        f.UltimosInsertados,
        f.UltimosActualizados,
        f.UltimosOmitidos,
        f.UltimosRechazados,
        f.UltimoError);

    private static SincronizacionEjecucionResponse AResponse(SincronizacionEjecucion e) => new(
        e.Id,
        e.InicioUtc,
        e.FinUtc,
        e.DuracionMs,
        e.Estado switch
        {
            IntegracionCatalogos.EstadoSincronizacionEnCurso => "EN_CURSO",
            IntegracionCatalogos.EstadoSincronizacionCorrecta => "CORRECTA",
            IntegracionCatalogos.EstadoSincronizacionFallida => "FALLIDA",
            _ => "DESCONOCIDO"
        },
        e.RegistrosLeidos,
        e.RegistrosCreados,
        e.RegistrosActualizados,
        e.RegistrosSinCambio,
        e.RegistrosRechazados,
        e.MensajeError);
}
