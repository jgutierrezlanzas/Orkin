using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Application.Cotizaciones;
using Orkin.Cotizador.Core.Application.Sistema;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

[ApiController]
[Route("api/cotizaciones")]
[Produces("application/json")]
[Authorize("COTIZACIONES.VER")]
public sealed class CotizacionesController : ControllerBase
{
    private readonly CotizacionService _servicio;
    private readonly ConsecutivoService _consecutivo;
    private readonly TipoCambioService _tipoCambio;
    private readonly CotizadorDbContext _contexto;
    private readonly Pdf.GeneradorPdfCotizacion _generadorPdf;

    public CotizacionesController(
        CotizacionService servicio,
        ConsecutivoService consecutivo,
        TipoCambioService tipoCambio,
        CotizadorDbContext contexto,
        Pdf.GeneradorPdfCotizacion generadorPdf)
    {
        _servicio = servicio;
        _consecutivo = consecutivo;
        _tipoCambio = tipoCambio;
        _contexto = contexto;
        _generadorPdf = generadorPdf;
    }

    [HttpGet]
    public async Task<IResult> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanio = 20,
        [FromQuery] Guid? clienteId = null,
        [FromQuery] string? estado = null,
        CancellationToken cancellationToken = default)
    {
        // Nota: Cotizacion no tiene IsActive. Se filtra por estado: no mostrar anuladas.
        // El nombre sale del snapshot ClienteNombre y no de la ficha: una cotización vieja
        // tiene que seguir mostrando el nombre con el que se cotizó, aunque el cliente haya
        // cambiado de nombre, se haya desactivado o nunca llegó a existir en el catálogo.
        var query = _contexto.Cotizacion
            .AsNoTracking()
            .Include(c => c.TipoServicio)
            .Include(c => c.Estado)
            .Where(c => c.Estado != null && c.Estado.Codigo != "ANULADA");

        if (clienteId.HasValue)
        {
            query = query.Where(c => c.ClienteId == clienteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(c => c.Estado.Codigo == estado);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.CreatedAt)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(c => new CotizacionResponse(
                c.Id,
                c.Numero,
                c.Anio,
                c.ClienteId,
                c.ClienteNombre,
                c.TipoServicioId,
                c.TipoServicio.Nombre,
                c.Fecha,
                c.Modalidad != null ? c.Modalidad.Codigo : string.Empty,
                c.MargenAplicado,
                c.Estado != null ? c.Estado.Codigo : string.Empty,
                c.Estado != null ? c.Estado.Nombre : string.Empty,
                c.TotalInsumosSemestre1,
                c.TotalInsumosSemestre2,
                c.TotalInsumos,
                c.TotalAmortizablesMensual,
                c.CostoTotal,
                c.PrecioCordobas,
                c.PrecioUsd,
                c.TipoCambioValor,
                c.TipoCambioFecha,
                c.TipoCambioId,
                c.MetodoCosteoId,
                c.PorcentajePlantilla,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return ApiResponse<PaginatedResponse<CotizacionResponse>>.Ok(
            new PaginatedResponse<CotizacionResponse>(items, total, pagina, tamanio)).AHttp();
    }

    [HttpGet("opciones")]
    public async Task<IResult> Opciones(CancellationToken cancellationToken)
    {
        var clientes = await _contexto.Cliente
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Nombre)
            .Select(c => new ClienteOption(c.Id, c.Codigo, c.Nombre, c.TipoCliente.Nombre))
            .ToListAsync(cancellationToken);

        var tiposServicio = await _contexto.TipoServicio
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Orden)
            .Select(t => new TipoServicioOption(t.Id, t.Codigo, t.Nombre, t.EsResidencialPorDefecto, t.EsEmpresarialPorDefecto))
            .ToListAsync(cancellationToken);

        // Los tipos de cliente van para el caso "cliente nuevo": quien cotiza elige el tipo y
        // ese tipo es el que decide el método de costeo del precio.
        var tiposCliente = await _contexto.TipoClienteCatalogo
            .AsNoTracking()
            .Where(t => t.EsActivo)
            .OrderBy(t => t.Id)
            .Select(t => new TipoClienteOpcion(t.Id, t.Codigo, t.Nombre))
            .ToListAsync(cancellationToken);

        var modalidades = await _contexto.ModalidadCatalogo
            .AsNoTracking()
            .Where(m => m.EsActivo)
            .OrderBy(m => m.Orden)
            .Select(m => new ModalidadOption(m.Codigo, m.Nombre))
            .ToListAsync(cancellationToken);

        var respuesta = new
        {
            Clientes = clientes,
            TiposServicio = tiposServicio,
            TiposCliente = tiposCliente,
            Modalidades = modalidades
        };

        return ApiResponse<object>.Ok(respuesta).AHttp();
    }

    [HttpPost("calcular")]
    public async Task<IResult> Calcular(
        [FromBody] CalcularCotizacionRequest request,
        CancellationToken cancellationToken)
    {
        var amortizables = request.Amortizables is { Count: > 0 }
            ? request.Amortizables.Select(a => (a.ActivoId, a.Cantidad)).ToList()
            : null;

        var resultado = await _servicio.CalcularAsync(
            request.ClienteId,
            request.TipoClienteId,
            request.Fecha,
            request.Margen,
            request.Insumos.Select(i => (i.ProductoId, i.Cantidad, i.SemestreId)).ToList(),
            amortizables,
            cancellationToken);

        var tc = await _tipoCambio.ObtenerVigenteAsync(request.Fecha, cancellationToken);

        var response = new CalculoCotizacionResponse(
            resultado.TotalInsumosSemestre1,
            resultado.TotalInsumosSemestre2,
            resultado.TotalInsumos,
            resultado.TotalAmortizablesMensual,
            resultado.CostoTotal,
            resultado.PrecioCordobas,
            resultado.PrecioUsd,
            resultado.MargenAplicado,
            resultado.TipoCambioValor,
            tc != null ? tc.Fecha : request.Fecha,
            tc != null ? tc.Id : null,
            resultado.MetodoCosteoId,
            resultado.PorcentajePlantilla);

        return ApiResponse<CalculoCotizacionResponse>.Ok(response).AHttp();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entidad = await _contexto.Cotizacion
            .AsNoTracking()
            .Include(c => c.TipoServicio)
            .Include(c => c.Estado)
            .Include(c => c.Modalidad)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<CotizacionDetalleResponse>
                .Error("COTIZACION_NO_ENCONTRADA", "La cotización no existe.")
                .AHttp();
        }

        // Las líneas de insumo van con la cotización: el detalle las muestra y el formulario de
        // edición las necesita para recargar la pantalla. Sin ellas, editar significaría
        // rearmar los insumos de memoria, y nadie debería tener que adivinar lo que ya guardó.
        var insumos = await _contexto.DetalleInsumo
            .AsNoTracking()
            .Where(d => d.CotizacionId == id)
            .Select(d => new DetalleInsumoResponse(d.ProductoId, d.Cantidad, d.SemestreId))
            .ToListAsync(cancellationToken);

        var response = new CotizacionDetalleResponse(
            entidad.Id,
            entidad.Numero,
            entidad.Anio,
            entidad.ClienteId,
            entidad.ClienteNombre,
            entidad.TipoServicioId,
            entidad.TipoServicio.Nombre,
            entidad.Fecha,
            entidad.Modalidad?.Codigo ?? string.Empty,
            entidad.MargenAplicado,
            entidad.Estado.Codigo,
            entidad.Estado.Nombre,
            entidad.TotalInsumosSemestre1,
            entidad.TotalInsumosSemestre2,
            entidad.TotalInsumos,
            entidad.TotalAmortizablesMensual,
            entidad.CostoTotal,
            entidad.PrecioCordobas,
            entidad.PrecioUsd,
            entidad.TipoCambioValor,
            entidad.TipoCambioFecha,
            entidad.TipoCambioId,
            entidad.MetodoCosteoId,
            entidad.PorcentajePlantilla,
            entidad.CreatedAt,
            insumos);

        return ApiResponse<CotizacionDetalleResponse>.Ok(response).AHttp();
    }

    /// <summary>
    /// Descarga el PDF de la cotización — el documento que viaja al cliente.
    /// <para>El PDF es un <b>snapshot</b>: muestra los números que estaban guardados al momento
    /// de generarlo. No recalcula nada, porque un documento que puede cambiar debajo de quien
    /// lo está leyendo no es un documento.</para>
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Authorize("COTIZACIONES.DESCARGAR_PDF")]
    public async Task<IResult> DescargarPdf(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var cotizacion = await _contexto.Cotizacion
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => c.Numero)
                .FirstOrDefaultAsync(cancellationToken);

            if (cotizacion is null)
            {
                return ApiResponse<object>.Error("NO_ENCONTRADO", "La cotización no existe.").AHttp();
            }

            var pdf = await _generadorPdf.GenerarAsync(id, cancellationToken);

            return Results.File(pdf, "application/pdf", $"{cotizacion}.pdf");
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<object>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
    }

    [HttpPost]
    [Authorize("COTIZACIONES.CREAR")]
    public async Task<IResult> Crear(
        [FromBody] CrearCotizacionRequest request,
        CancellationToken cancellationToken)
    {
        // Nota: no se abre una transacción explícita aquí porque la estrategia de reintento de
        // EF Core (SqlServerRetryingExecutionStrategy) no las admite iniciadas por el usuario.
        // El ConsecutivoService ya gestiona su propia transacción internamente.
        var numero = await _consecutivo.SiguienteAsync("COTIZACION", request.Fecha.Year, cancellationToken: cancellationToken);

            // O cotiza para un cliente del catálogo, o para alguien nuevo que todavía no tiene
            // ficha. No hay tercera opción: una cotización sin cliente no lleva a nadie y una
            // con las dos cosas a la vez es una contradicción que no se sabe cómo leer.
            Orkin.Cotizador.Core.Domain.Entidades.Cliente? cliente = null;
            byte tipoClienteId;

            if (request.ClienteId is { } delCatalogo)
            {
                cliente = await _contexto.Cliente
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == delCatalogo && c.IsActive, cancellationToken)
                    ?? throw new InvalidOperationException("El cliente no existe o está inactivo.");

                tipoClienteId = cliente.TipoClienteId;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.ClienteNombre))
                {
                    throw new InvalidOperationException(
                        "Para un cliente nuevo hace falta el nombre con el que se cotiza.");
                }

                tipoClienteId = request.TipoClienteId
                    ?? throw new InvalidOperationException(
                        "Para un cliente nuevo hace falta el tipo de cliente: es lo que decide el método de costeo.");
            }

            if (!await _contexto.TipoServicio.AnyAsync(t => t.Id == request.TipoServicioId && t.IsActive, cancellationToken))
            {
                throw new InvalidOperationException("El tipo de servicio no existe o está inactivo.");
            }

            byte? modalidadId = null;
            if (!string.IsNullOrWhiteSpace(request.ModalidadCodigo))
            {
                var modalidad = await _contexto.ModalidadCatalogo
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Codigo == request.ModalidadCodigo && m.EsActivo, cancellationToken);
                if (modalidad is null)
                {
                    throw new InvalidOperationException("La modalidad no existe o está inactiva.");
                }
                modalidadId = modalidad.Id;
            }

            var estadoBorrador = await _contexto.EstadoCotizacionCatalogo
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Codigo == "BORRADOR", cancellationToken)
                ?? throw new InvalidOperationException("El estado BORRADOR no está configurado.");

            var tipoConsecutivo = await _contexto.TipoConsecutivoCatalogo
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Codigo == "COTIZACION", cancellationToken);

            // Calcular usando el servicio
            var amortizables = request.Amortizables is { Count: > 0 }
                ? request.Amortizables.Select(a => (a.ActivoId, a.Cantidad)).ToList()
                : null;

            var resultado = await _servicio.CalcularAsync(
                request.ClienteId,
                request.TipoClienteId,
                request.Fecha,
                request.Margen,
                request.Insumos.Select(i => (i.ProductoId, i.Cantidad, i.SemestreId)).ToList(),
                amortizables,
                cancellationToken);

            var tc = await _tipoCambio.ObtenerVigenteAsync(request.Fecha, cancellationToken);

            var cotizacion = new Orkin.Cotizador.Core.Domain.Entidades.Cotizacion
            {
                Id = Guid.NewGuid(),
                Numero = numero,
                Anio = (short)request.Fecha.Year,
                ClienteId = request.ClienteId,
                TipoClienteId = tipoClienteId,
                ClienteNombre = cliente?.Nombre ?? request.ClienteNombre!.Trim(),
                TipoServicioId = request.TipoServicioId,
                Fecha = request.Fecha,
                ModalidadId = modalidadId,
                MargenAplicado = request.Margen,
                EstadoId = estadoBorrador.Id,
                TipoConsecutivoId = tipoConsecutivo?.Id,
                TotalInsumosSemestre1 = resultado.TotalInsumosSemestre1,
                TotalInsumosSemestre2 = resultado.TotalInsumosSemestre2,
                TotalInsumos = resultado.TotalInsumos,
                TotalAmortizablesMensual = resultado.TotalAmortizablesMensual,
                CostoTotal = resultado.CostoTotal,
                PrecioCordobas = resultado.PrecioCordobas,
                PrecioUsd = resultado.PrecioUsd,
                TipoCambioValor = resultado.TipoCambioValor,
                TipoCambioFecha = tc?.Fecha ?? request.Fecha,
                TipoCambioId = tc?.Id,
                MetodoCosteoId = resultado.MetodoCosteoId,
                PorcentajePlantilla = resultado.PorcentajePlantilla,
                HashCalculo = resultado.HashCalculo,
                ParametrosCalculo = resultado.ParametrosCalculo,
                OrigenDatoId = (byte)OrigenDatoCatalogoSeed.Manual,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _contexto.Cotizacion.Add(cotizacion);

            // Detalles de insumos
            foreach (var insumo in request.Insumos)
            {
                var producto = await _contexto.Producto
                    .AsNoTracking()
                    .Include(p => p.UnidadMedida)
                    .FirstOrDefaultAsync(p => p.Id == insumo.ProductoId, cancellationToken);

                if (producto is not null)
                {
                    _contexto.DetalleInsumo.Add(new Orkin.Cotizador.Core.Domain.Entidades.DetalleInsumo
                    {
                        Id = Guid.NewGuid(),
                        CotizacionId = cotizacion.Id,
                        ProductoId = insumo.ProductoId,
                        ProductoNombre = producto.Nombre,
                        UnidadMedidaTexto = producto.UnidadMedida?.Abreviatura ?? string.Empty,
                        Cantidad = insumo.Cantidad,
                        PrecioUnitario = producto.CostoActual,
                        SemestreId = insumo.SemestreId
                    });
                }
            }

            await _contexto.SaveChangesAsync(cancellationToken);

            var response = new CotizacionResponse(
                cotizacion.Id,
                cotizacion.Numero,
                cotizacion.Anio,
                cotizacion.ClienteId,
                cotizacion.ClienteNombre,
                cotizacion.TipoServicioId,
                (await _contexto.TipoServicio.FindAsync([cotizacion.TipoServicioId], cancellationToken))?.Nombre ?? string.Empty,
                cotizacion.Fecha,
                cotizacion.Modalidad?.Codigo ?? string.Empty,
                cotizacion.MargenAplicado,
                estadoBorrador.Codigo,
                estadoBorrador.Nombre,
                cotizacion.TotalInsumosSemestre1,
                cotizacion.TotalInsumosSemestre2,
                cotizacion.TotalInsumos,
                cotizacion.TotalAmortizablesMensual,
                cotizacion.CostoTotal,
                cotizacion.PrecioCordobas,
                cotizacion.PrecioUsd,
                cotizacion.TipoCambioValor,
                cotizacion.TipoCambioFecha,
                cotizacion.TipoCambioId,
                cotizacion.MetodoCosteoId,
                cotizacion.PorcentajePlantilla,
                cotizacion.CreatedAt);

            return ApiResponse<CotizacionResponse>.Ok(response).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("COTIZACIONES.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarCotizacionRequest request,
        CancellationToken cancellationToken)
    {
        var cotizacion = await _contexto.Cotizacion
            .Include(c => c.Estado)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("La cotización no existe.");

        if (cotizacion.Estado.Codigo != "BORRADOR" && cotizacion.Estado.Codigo != "RECHAZADA")
        {
            return ApiResponse<CotizacionResponse>
                .Error("COTIZACION_NO_EDITABLE", "Solo se pueden editar cotizaciones en BORRADOR o RECHAZADA.")
                .AHttp();
        }

        if (!await _contexto.TipoServicio.AnyAsync(t => t.Id == request.TipoServicioId && t.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("El tipo de servicio no existe o está inactivo.");
        }

        byte? modalidadId = null;
        if (!string.IsNullOrWhiteSpace(request.ModalidadCodigo))
        {
            var modalidad = await _contexto.ModalidadCatalogo
                .FirstOrDefaultAsync(m => m.Codigo == request.ModalidadCodigo && m.EsActivo, cancellationToken);
            if (modalidad is null)
            {
                throw new InvalidOperationException("La modalidad no existe o está inactiva.");
            }
            modalidadId = modalidad.Id;
        }

        // Recalcular. Para un cliente del catálogo el tipo sale de su ficha; para un cliente
        // nuevo sale del snapshot con el que se cotizó, porque no hay ficha de donde sacarlo.
        var amortizablesAct = request.Amortizables is { Count: > 0 }
            ? request.Amortizables.Select(a => (a.ActivoId, a.Cantidad)).ToList()
            : null;

        var resultado = await _servicio.CalcularAsync(
            cotizacion.ClienteId,
            cotizacion.ClienteId is null ? cotizacion.TipoClienteId : null,
            request.Fecha,
            request.Margen,
            request.Insumos.Select(i => (i.ProductoId, i.Cantidad, i.SemestreId)).ToList(),
            amortizablesAct,
            cancellationToken);

        var tc = await _tipoCambio.ObtenerVigenteAsync(request.Fecha, cancellationToken);

        cotizacion.TipoServicioId = request.TipoServicioId;
        cotizacion.Fecha = request.Fecha;
        cotizacion.ModalidadId = modalidadId;
        cotizacion.MargenAplicado = request.Margen;
        cotizacion.TotalInsumosSemestre1 = resultado.TotalInsumosSemestre1;
        cotizacion.TotalInsumosSemestre2 = resultado.TotalInsumosSemestre2;
        cotizacion.TotalInsumos = resultado.TotalInsumos;
        cotizacion.TotalAmortizablesMensual = resultado.TotalAmortizablesMensual;
        cotizacion.CostoTotal = resultado.CostoTotal;
        cotizacion.PrecioCordobas = resultado.PrecioCordobas;
        cotizacion.PrecioUsd = resultado.PrecioUsd;
        cotizacion.TipoCambioValor = resultado.TipoCambioValor;
        cotizacion.TipoCambioFecha = tc?.Fecha ?? request.Fecha;
        cotizacion.TipoCambioId = tc?.Id;
        cotizacion.MetodoCosteoId = resultado.MetodoCosteoId;
        cotizacion.PorcentajePlantilla = resultado.PorcentajePlantilla;
        cotizacion.UpdatedAt = DateTime.UtcNow;

        // Actualizar detalles
        _contexto.DetalleInsumo.RemoveRange(_contexto.DetalleInsumo.Where(d => d.CotizacionId == id));
        foreach (var insumo in request.Insumos)
        {
            var producto = await _contexto.Producto
                .AsNoTracking()
                .Include(p => p.UnidadMedida)
                .FirstOrDefaultAsync(p => p.Id == insumo.ProductoId, cancellationToken);
            
            if (producto is not null)
            {
                _contexto.DetalleInsumo.Add(new Orkin.Cotizador.Core.Domain.Entidades.DetalleInsumo
                {
                    Id = Guid.NewGuid(),
                    CotizacionId = cotizacion.Id,
                    ProductoId = insumo.ProductoId,
                    ProductoNombre = producto.Nombre,
                    UnidadMedidaTexto = producto.UnidadMedida?.Abreviatura ?? string.Empty,
                    Cantidad = insumo.Cantidad,
                    PrecioUnitario = producto.CostoActual,
                    SemestreId = insumo.SemestreId
                });
            }
        }

        await _contexto.SaveChangesAsync(cancellationToken);

        var response = new CotizacionResponse(
            cotizacion.Id,
            cotizacion.Numero,
            cotizacion.Anio,
            cotizacion.ClienteId,
            cotizacion.ClienteNombre,
            cotizacion.TipoServicioId,
            (await _contexto.TipoServicio.FindAsync([cotizacion.TipoServicioId], cancellationToken))?.Nombre ?? string.Empty,
            cotizacion.Fecha,
            cotizacion.Modalidad?.Codigo ?? string.Empty,
            cotizacion.MargenAplicado,
            cotizacion.Estado.Codigo,
            cotizacion.Estado.Nombre,
            cotizacion.TotalInsumosSemestre1,
            cotizacion.TotalInsumosSemestre2,
            cotizacion.TotalInsumos,
            cotizacion.TotalAmortizablesMensual,
            cotizacion.CostoTotal,
            cotizacion.PrecioCordobas,
            cotizacion.PrecioUsd,
            cotizacion.TipoCambioValor,
            cotizacion.TipoCambioFecha,
            cotizacion.TipoCambioId,
            cotizacion.MetodoCosteoId,
            cotizacion.PorcentajePlantilla,
            cotizacion.CreatedAt);

        return ApiResponse<CotizacionResponse>.Ok(response).AHttp();
    }

    /// <summary>
    /// Anula una cotización. Estado terminal: la fila y su historial se conservan — anular no es
    /// borrar. Se exige un motivo, porque anular algo sin decir por qué es la puerta abierta a
    /// «se anuló por accidente» sin rastro de qué pasó.
    /// </summary>
    [HttpPut("{id:guid}/anular")]
    [Authorize("COTIZACIONES.ANULAR")]
    public async Task<IResult> Anular(
        Guid id,
        [FromBody] AnularCotizacionRequest request,
        CancellationToken cancellationToken)
    {
        var cotizacion = await _contexto.Cotizacion
            .Include(c => c.Estado)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("La cotización no existe.");

        if (cotizacion.Estado.Codigo == "ANULADA")
        {
            return ApiResponse<CotizacionResponse>
                .Error("YA_ANULADA", "La cotización ya está anulada.")
                .AHttp();
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return ApiResponse<CotizacionResponse>
                .Error("VALIDACION", "El motivo de la anulación es obligatorio.")
                .AHttp();
        }

        var estadoAnulada = await _contexto.EstadoCotizacionCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Codigo == "ANULADA", cancellationToken)
            ?? throw new InvalidOperationException("El estado ANULADA no está configurado.");

        cotizacion.EstadoId = estadoAnulada.Id;
        cotizacion.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return ApiResponse<CotizacionResponse>.Ok(new CotizacionResponse(
            cotizacion.Id,
            cotizacion.Numero,
            cotizacion.Anio,
            cotizacion.ClienteId,
            cotizacion.ClienteNombre,
            cotizacion.TipoServicioId,
            (await _contexto.TipoServicio.FindAsync([cotizacion.TipoServicioId], cancellationToken))?.Nombre ?? string.Empty,
            cotizacion.Fecha,
            cotizacion.Modalidad?.Codigo ?? string.Empty,
            cotizacion.MargenAplicado,
            estadoAnulada.Codigo,
            estadoAnulada.Nombre,
            cotizacion.TotalInsumosSemestre1,
            cotizacion.TotalInsumosSemestre2,
            cotizacion.TotalInsumos,
            cotizacion.TotalAmortizablesMensual,
            cotizacion.CostoTotal,
            cotizacion.PrecioCordobas,
            cotizacion.PrecioUsd,
            cotizacion.TipoCambioValor,
            cotizacion.TipoCambioFecha,
            cotizacion.TipoCambioId,
            cotizacion.MetodoCosteoId,
            cotizacion.PorcentajePlantilla,
            cotizacion.CreatedAt)).AHttp();
    }
}

/// <summary>Solicitud para anular una cotización. El motivo es obligatorio.</summary>
public sealed record AnularCotizacionRequest(string Motivo);

/// <summary>Línea de insumo de una cotización existente, para el detalle y la edición.</summary>
public sealed record DetalleInsumoResponse(
    Guid ProductoId,
    decimal Cantidad,
    byte? SemestreId);

/// <summary>
/// Una cotización con sus líneas de insumo. El listado usa <c>CotizacionResponse</c> (sin
/// insumos, que es todo lo que hace falta para la tabla); el detalle y la edición usan esta,
/// que trae las líneas para que editar sea recargar la pantalla y no rearmar de memoria.
/// </summary>
public sealed record CotizacionDetalleResponse(
    Guid Id,
    string Numero,
    int Anio,
    Guid? ClienteId,
    string ClienteNombre,
    Guid TipoServicioId,
    string TipoServicioNombre,
    DateOnly Fecha,
    string ModalidadCodigo,
    decimal Margen,
    string EstadoCodigo,
    string EstadoNombre,
    decimal TotalInsumosSemestre1,
    decimal TotalInsumosSemestre2,
    decimal TotalInsumos,
    decimal TotalAmortizablesMensual,
    decimal CostoTotal,
    decimal PrecioCordobas,
    decimal PrecioUsd,
    decimal TipoCambioValor,
    DateOnly TipoCambioFecha,
    Guid? TipoCambioId,
    byte MetodoCosteoId,
    decimal? PorcentajePlantilla,
    DateTime CreatedAt,
    IReadOnlyList<DetalleInsumoResponse> Insumos);

/// <summary>Respuesta paginada genérica.</summary>
public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Pagina,
    int TamanioPagina)
{
    public int TotalPaginas => (int)Math.Ceiling((double)Total / TamanioPagina);
    public bool TienePaginaAnterior => Pagina > 1;
    public bool TienePaginaSiguiente => Pagina < TotalPaginas;
}