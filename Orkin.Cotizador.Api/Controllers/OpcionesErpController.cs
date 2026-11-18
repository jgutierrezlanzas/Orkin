using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Opciones legibles para los códigos ERP que el WinForms capturaba a ciegas. Todo sale de las
/// vistas <c>vv*</c> existentes: no se copian catálogos (D-20), no hay tablas paralelas (D-22) y
/// esta es la única API de opciones (D-21). Lo que se guarda en la plantilla sigue siendo el código.
/// </summary>
[ApiController]
[Route("api/catalogos/opciones-erp")]
[Produces("application/json")]
[Authorize("CATALOGOS.VER")]
public sealed class OpcionesErpController : ControllerBase
{
    private const string Activo = "SI";
    // vvClientes y vvArticulos usan 'S'; vvTipoServicios y vvTecnicos usan 'SI'. Verificados en
    // SINCOERPORKIN: 935, 201, 43 y 14 filas activas respectivamente (consulta de solo lectura).
    private const string ActivoCorto = "S";
    private readonly CotizadorDbContext _contexto;

    public OpcionesErpController(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    [HttpGet]
    public async Task<IResult> Listar(
        [FromQuery] string? cliente = null,
        CancellationToken cancellationToken = default)
    {
        // El detalle de la locación es el NOMBRE del cliente, no su código: el combo debe leerse
        // solo. Si el cliente ya no está en vvClientes se conserva el código como último recurso.
        var locaciones = await (
            from l in _contexto.VistaLocacionErp.AsNoTracking()
            where l.Activo == Activo &&
                  (string.IsNullOrWhiteSpace(cliente) || l.Cliente == cliente.Trim())
            join c in _contexto.VistaClienteErp.AsNoTracking() on l.Cliente equals c.Cliente into clientesJoin
            from c in clientesJoin.DefaultIfEmpty()
            orderby l.Codigo
            select new OpcionErpResponse(
                l.Codigo,
                string.IsNullOrWhiteSpace(l.Nombre) ? l.Codigo : l.Nombre!,
                c == null || string.IsNullOrWhiteSpace(c.NombreCliente) ? l.Cliente : c.NombreCliente))
            .ToListAsync(cancellationToken);

        var vehiculos = await _contexto.VistaVehiculoErp
            .AsNoTracking()
            .Where(v => v.Activo == Activo)
            .OrderBy(v => v.Codigo)
            .Select(v => new OpcionErpResponse(
                v.Codigo,
                string.IsNullOrWhiteSpace(v.Descripcion) ? v.Codigo : v.Descripcion!,
                DetalleVehiculo(v.Placa, v.TipoVehiculo)))
            .ToListAsync(cancellationToken);

        var plagas = await ListarOpcionSimpleAsync(_contexto.VistaPlagaErp, cancellationToken);
        var ubicaciones = await ListarOpcionSimpleAsync(_contexto.VistaUbicacionErp, cancellationToken);

        var dosificaciones = await _contexto.VistaDosificacionErp
            .AsNoTracking()
            .Where(v => v.Activo == Activo)
            .OrderBy(v => v.Codigo)
            .Select(v => new OpcionErpResponse(
                v.Codigo,
                string.IsNullOrWhiteSpace(v.Descripcion) ? v.Codigo : v.Descripcion!,
                v.Cantidad.HasValue
                    ? v.Cantidad.Value.ToString("0.########", CultureInfo.InvariantCulture)
                    : null,
                v.CodigoUnidadMedida))
            .ToListAsync(cancellationToken);

        var metodos = await ListarOpcionSimpleAsync(_contexto.VistaMetodoAplicacionErp, cancellationToken);

        var frecuencias = await _contexto.VistaFrecuenciaErp
            .AsNoTracking()
            .Where(v => v.Activo == Activo)
            .OrderBy(v => v.Codigo)
            .Select(v => new OpcionErpResponse(
                v.Codigo,
                string.IsNullOrWhiteSpace(v.Descripcion) ? v.Codigo : v.Descripcion!,
                v.NumeroServicios.HasValue
                    ? v.NumeroServicios.Value.ToString(CultureInfo.InvariantCulture)
                    : null))
            .ToListAsync(cancellationToken);

        var equipos = await ListarOpcionSimpleAsync(_contexto.VistaEquipoErp, cancellationToken);

        // Fase 5: las bodegas salen de vvBodegas, la misma vista que validaba el WinForms. No se
        // copian a una tabla propia y tampoco se crea un endpoint nuevo (D-20/D-21).
        var bodegas = await _contexto.VistaBodegaErp
            .AsNoTracking()
            .Where(b => b.Activo == Activo)
            .OrderBy(b => b.Codigo)
            .Select(b => new OpcionErpResponse(
                b.Codigo,
                string.IsNullOrWhiteSpace(b.Descripcion) ? b.Codigo : b.Descripcion!,
                b.BodegaPrincipal == "SI" ? "Principal" : null))
            .ToListAsync(cancellationToken);

        // Catálogos que antes se duplicaban en dbo.* y que el cotizador ahora consume por código
        // (D-24). Todo sale de las mismas vv*: CAT_CLIENTE, DESCRIPCION/TIPOACTIVIDAD, INICIALES y
        // COSTO_UNITARIO/UNIDAD_VENTA/CLASIFICACION. Unidades y clasificaciones se derivan de los
        // propios artículos para no crear un catálogo paralelo con los 12/10 códigos descartados.
        var clientes = await _contexto.VistaClienteErp
            .AsNoTracking()
            .Where(c => c.Activo == ActivoCorto)
            .OrderBy(c => c.Cliente)
            .Select(c => new OpcionErpResponse(
                c.Cliente,
                string.IsNullOrWhiteSpace(c.NombreCliente) ? c.Cliente : c.NombreCliente!,
                null,
                null,
                c.CategoriaCliente))
            .ToListAsync(cancellationToken);

        var tiposServicio = await _contexto.VistaTipoServicioErp
            .AsNoTracking()
            .Where(v => v.Activo == Activo)
            .OrderBy(v => v.Codigo)
            .Select(v => new OpcionErpResponse(
                v.Codigo,
                string.IsNullOrWhiteSpace(v.Descripcion) ? v.Codigo : v.Descripcion!,
                v.TipoActividad))
            .ToListAsync(cancellationToken);

        var tecnicos = await _contexto.VistaTecnicoErp
            .AsNoTracking()
            .Where(v => v.Activo == Activo)
            .OrderBy(v => v.Codigo)
            .Select(v => new OpcionErpResponse(
                v.Codigo,
                string.IsNullOrWhiteSpace(v.NombreCompleto) ? v.Codigo : v.NombreCompleto!,
                v.Iniciales))
            .ToListAsync(cancellationToken);

        var articulos = await _contexto.VistaArticuloErp
            .AsNoTracking()
            .Where(a => a.Activo == ActivoCorto)
            .OrderBy(a => a.CodigoArticulo)
            .Select(a => new OpcionErpResponse(
                a.CodigoArticulo,
                !string.IsNullOrWhiteSpace(a.DescripArticulo)
                    ? a.DescripArticulo!
                    : (string.IsNullOrWhiteSpace(a.Descripcion) ? a.CodigoArticulo : a.Descripcion!),
                a.Amortizable,
                a.UnidadVenta,
                a.Clasificacion,
                a.CostoUnitario))
            .ToListAsync(cancellationToken);

        var activosAmortizables = articulos
            .Where(a => string.Equals(a.Detalle, Activo, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var unidades = articulos
            .Where(a => !string.IsNullOrWhiteSpace(a.UnidadMedida))
            .GroupBy(a => a.UnidadMedida!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new OpcionErpResponse(g.Key, g.Key))
            .OrderBy(o => o.Codigo)
            .ToList();

        var clasificaciones = articulos
            .Where(a => !string.IsNullOrWhiteSpace(a.Clasificacion))
            .GroupBy(a => a.Clasificacion!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new OpcionErpResponse(g.Key, g.Key))
            .OrderBy(o => o.Codigo)
            .ToList();

        var respuesta = new OpcionesErpResponse(
            locaciones, vehiculos, plagas, ubicaciones, dosificaciones, metodos, frecuencias, equipos, bodegas,
            clientes, tiposServicio, tecnicos, articulos, activosAmortizables, unidades, clasificaciones);
        return ApiResponse<OpcionesErpResponse>.Ok(respuesta).AHttp();
    }

    private static async Task<List<OpcionErpResponse>> ListarOpcionSimpleAsync<TEntidad>(
        DbSet<TEntidad> origen,
        CancellationToken cancellationToken)
        where TEntidad : class, IOpcionErpView
    {
        return await origen
            .AsNoTracking()
            .Where(v => v.Activo == Activo)
            .OrderBy(v => v.Codigo)
            .Select(v => new OpcionErpResponse(
                v.Codigo,
                string.IsNullOrWhiteSpace(v.Descripcion) ? v.Codigo : v.Descripcion!))
            .ToListAsync(cancellationToken);
    }

    private static string? DetalleVehiculo(string? placa, string? tipo)
    {
        var partes = new[] { placa, tipo }
            .Where(p => !string.IsNullOrWhiteSpace(p) && !p!.Trim().Equals("ND", StringComparison.OrdinalIgnoreCase))
            .Select(p => p!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return partes.Length == 0 ? null : string.Join(" · ", partes);
    }
}
