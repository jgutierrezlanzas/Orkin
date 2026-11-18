using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Application.Cotizaciones;
using Orkin.Cotizador.Core.Application.Sistema;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
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
    private readonly CatalogoErpService _catalogoErp;
    private readonly CotizadorDbContext _contexto;
    private readonly Pdf.GeneradorPdfCotizacion _generadorPdf;

    public CotizacionesController(
        CotizacionService servicio,
        ConsecutivoService consecutivo,
        TipoCambioService tipoCambio,
        CatalogoErpService catalogoErp,
        CotizadorDbContext contexto,
        Pdf.GeneradorPdfCotizacion generadorPdf)
    {
        _servicio = servicio;
        _consecutivo = consecutivo;
        _tipoCambio = tipoCambio;
        _catalogoErp = catalogoErp;
        _contexto = contexto;
        _generadorPdf = generadorPdf;
    }

    [HttpGet]
    public async Task<IResult> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanio = 20,
        [FromQuery] string? clienteCodigo = null,
        [FromQuery] string? estado = null,
        CancellationToken cancellationToken = default)
    {
        // Nota: Cotizacion no tiene IsActive. Se filtra por el snapshot del estado: no mostrar
        // anuladas. El nombre y el tipo de servicio también salen del snapshot, no de una ficha
        // viva: una cotización vieja tiene que seguir mostrando lo que se cotizó aunque el ERP
        // haya cambiado el cliente o el servicio.
        var query = _contexto.Cotizacion
            .AsNoTracking()
            .Where(c => c.EstadoCodigo != "ANULADA");

        if (!string.IsNullOrWhiteSpace(clienteCodigo))
        {
            var codigo = clienteCodigo.Trim();
            query = query.Where(c => c.ClienteCodigo == codigo);
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(c => c.EstadoCodigo == estado.Trim());
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
                c.ClienteCodigo,
                c.ClienteNombre,
                c.TipoServicioCodigo,
                c.TipoServicioNombre,
                c.Fecha,
                c.ModalidadCodigo ?? string.Empty,
                c.MargenAplicado,
                c.EstadoCodigo,
                c.EstadoNombre,
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

    /// <summary>
    /// Opciones de los combos del cotizador. Mantiene la ruta y el permiso
    /// <c>COTIZACIONES.VER</c> de siempre, pero la fuente es la misma que <c>opciones-erp</c>:
    /// <see cref="CatalogoErpService"/>. Así el cotizador no necesita <c>CATALOGOS.VER</c> y no
    /// hay dos contratos distintos para los mismos códigos.
    /// </summary>
    [HttpGet("opciones")]
    public async Task<IResult> Opciones(CancellationToken cancellationToken)
    {
        var opciones = await _catalogoErp.ObtenerOpcionesAsync(cancellationToken);

        // Tipos de cliente y modalidades son catálogos propios de la plataforma: no son copias
        // del ERP y por eso siguen viniendo de dbo.
        var tiposCliente = await _contexto.TipoClienteCatalogo
            .AsNoTracking()
            .Where(t => t.EsActivo)
            .OrderBy(t => t.Id)
            .Select(t => new TipoClienteOpcion(t.Id, t.Codigo, t.Nombre))
            .ToListAsync(cancellationToken);

        var nombresTiposCliente = tiposCliente.ToDictionary(t => t.Id, t => t.Nombre);

        var modalidades = await _contexto.ModalidadCatalogo
            .AsNoTracking()
            .Where(m => m.EsActivo)
            .OrderBy(m => m.Orden)
            .Select(m => new ModalidadOption(m.Codigo, m.Nombre))
            .ToListAsync(cancellationToken);

        var articulos = opciones.Articulos.Select(a => new ArticuloCotizacionOption(
            a.Codigo,
            a.Nombre,
            a.CostoUnitario,
            a.UnidadMedida,
            a.Clasificacion,
            a.Amortizable)).ToList();

        var respuesta = new CotizacionOpcionesResponse(
            opciones.Clientes.Select(c => new ClienteOption(
                c.Codigo,
                c.Nombre,
                nombresTiposCliente.GetValueOrDefault(c.TipoClienteId))).ToList(),
            opciones.TiposServicio.Select(t => new TipoServicioOption(t.Codigo, t.Nombre, t.Actividad)).ToList(),
            tiposCliente,
            modalidades,
            articulos,
            articulos.Where(a => a.Amortizable).ToList(),
            opciones.Tecnicos.Select(t => new TecnicoCotizacionOption(t.Codigo, t.Nombre, t.Iniciales)).ToList(),
            opciones.Unidades,
            opciones.Clasificaciones);

        return ApiResponse<CotizacionOpcionesResponse>.Ok(respuesta).AHttp();
    }

    [HttpPost("calcular")]
    public async Task<IResult> Calcular(
        [FromBody] CalcularCotizacionRequest request,
        CancellationToken cancellationToken)
    {
        var amortizables = request.Amortizables is { Count: > 0 }
            ? request.Amortizables.Select(a => (a.ActivoCodigo, a.Cantidad, a.MesesVidaUtil)).ToList()
            : null;

        var resultado = await _servicio.CalcularAsync(
            request.ClienteCodigo,
            request.TipoClienteId,
            request.Fecha,
            request.Margen,
            request.Insumos.Select(i => (i.ProductoCodigo, i.Cantidad, i.SemestreId)).ToList(),
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
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (entidad is null)
        {
            return ApiResponse<CotizacionDetalleResponse>
                .Error("COTIZACION_NO_ENCONTRADA", "La cotización no existe.")
                .AHttp();
        }

        // Las líneas van con la cotización: el detalle las muestra y el formulario de edición
        // las necesita para recargar la pantalla. Los códigos y snapshots ya viajan en la fila.
        var insumos = await _contexto.DetalleInsumo
            .AsNoTracking()
            .Where(d => d.CotizacionId == id)
            .OrderBy(d => d.Orden)
            .Select(d => new DetalleInsumoResponse(d.ProductoCodigo, d.Cantidad, d.SemestreId))
            .ToListAsync(cancellationToken);

        var amortizables = await _contexto.DetalleAmortizable
            .AsNoTracking()
            .Where(d => d.CotizacionId == id)
            .OrderBy(d => d.Orden)
            .Select(d => new DetalleAmortizableResponse(
                d.ActivoCodigo,
                d.Cantidad,
                d.MesesVidaUtil,
                d.PrecioUnitario,
                d.ValorMensual,
                d.ActivoNombre ?? string.Empty,
                d.UnidadMedidaTexto ?? string.Empty))
            .ToListAsync(cancellationToken);

        var response = new CotizacionDetalleResponse(
            entidad.Id,
            entidad.Numero,
            entidad.Anio,
            entidad.ClienteCodigo,
            entidad.ClienteNombre,
            entidad.TipoClienteId,
            entidad.TipoServicioCodigo,
            entidad.TipoServicioNombre,
            entidad.Fecha,
            entidad.ModalidadCodigo ?? string.Empty,
            entidad.MargenAplicado,
            entidad.EstadoCodigo,
            entidad.EstadoNombre,
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
            insumos,
            amortizables,
            entidad.TecnicoCodigo,
            entidad.FechaEstimada,
            entidad.DireccionServicio);

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
        var numero = await _consecutivo.SiguienteAsync(
            "COTIZACION", request.Fecha.Year, cancellationToken: cancellationToken);

        // O cotiza para un cliente del ERP, o para alguien nuevo que todavía no existe: no hay
        // tercera opción. La ficha del ERP decide el segmento; el nombre se congela en la fila.
        string? clienteCodigo = Texto(request.ClienteCodigo);
        byte tipoClienteId;
        string clienteNombre;

        if (clienteCodigo is not null)
        {
            var cliente = await _catalogoErp.ClienteAsync(clienteCodigo, cancellationToken);
            tipoClienteId = cliente.TipoClienteId;
            clienteNombre = cliente.Nombre;
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
            clienteNombre = request.ClienteNombre.Trim();
        }

        var tipoServicio = await _catalogoErp.TipoServicioAsync(
            request.TipoServicioCodigo, cancellationToken);

        var tecnico = await _catalogoErp.TecnicoAsync(request.TecnicoCodigo, cancellationToken);

        var (modalidadId, modalidadCodigo, modalidadNombre) = await ResolverModalidadAsync(
            request.ModalidadCodigo, cancellationToken);

        var estadoBorrador = await _contexto.EstadoCotizacionCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Codigo == "BORRADOR", cancellationToken)
            ?? throw new InvalidOperationException("El estado BORRADOR no está configurado.");

        var tipoConsecutivo = await _contexto.TipoConsecutivoCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Codigo == "COTIZACION", cancellationToken);

        var amortizables = request.Amortizables is { Count: > 0 }
            ? request.Amortizables.Select(a => (a.ActivoCodigo, a.Cantidad, a.MesesVidaUtil)).ToList()
            : null;

        var resultado = await _servicio.CalcularAsync(
            clienteCodigo,
            request.TipoClienteId,
            request.Fecha,
            request.Margen,
            request.Insumos.Select(i => (i.ProductoCodigo, i.Cantidad, i.SemestreId)).ToList(),
            amortizables,
            cancellationToken);

        var tc = await _tipoCambio.ObtenerVigenteAsync(request.Fecha, cancellationToken);

        var cotizacion = new Cotizacion
        {
            Id = Guid.NewGuid(),
            Numero = numero,
            Anio = (short)request.Fecha.Year,
            ClienteCodigo = clienteCodigo,
            ClienteNombre = clienteNombre,
            TipoClienteId = tipoClienteId,
            TipoServicioCodigo = tipoServicio.Codigo,
            TipoServicioNombre = tipoServicio.Nombre,
            Fecha = request.Fecha,
            ModalidadId = modalidadId,
            ModalidadCodigo = modalidadCodigo,
            ModalidadNombre = modalidadNombre,
            TecnicoCodigo = tecnico?.Codigo,
            TecnicoNombre = tecnico?.Nombre,
            FechaEstimada = request.FechaEstimada,
            DireccionServicio = request.DireccionServicio,
            MargenAplicado = request.Margen,
            EstadoId = estadoBorrador.Id,
            EstadoCodigo = estadoBorrador.Codigo,
            EstadoNombre = estadoBorrador.Nombre,
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
        await GuardarDetallesAsync(cotizacion.Id, request.Insumos, request.Amortizables, cancellationToken);
        await _contexto.SaveChangesAsync(cancellationToken);

        return ApiResponse<CotizacionResponse>.Ok(Responder(cotizacion)).AHttp();
    }

    [HttpPut("{id:guid}")]
    [Authorize("COTIZACIONES.EDITAR")]
    public async Task<IResult> Actualizar(
        Guid id,
        [FromBody] ActualizarCotizacionRequest request,
        CancellationToken cancellationToken)
    {
        var cotizacion = await _contexto.Cotizacion
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("La cotización no existe.");

        if (cotizacion.EstadoCodigo is not ("BORRADOR" or "RECHAZADA"))
        {
            return ApiResponse<CotizacionResponse>
                .Error("COTIZACION_NO_EDITABLE", "Solo se pueden editar cotizaciones en BORRADOR o RECHAZADA.")
                .AHttp();
        }

        var tipoServicio = await _catalogoErp.TipoServicioAsync(
            request.TipoServicioCodigo, cancellationToken);

        var tecnico = await _catalogoErp.TecnicoAsync(request.TecnicoCodigo, cancellationToken);

        var (modalidadId, modalidadCodigo, modalidadNombre) = await ResolverModalidadAsync(
            request.ModalidadCodigo, cancellationToken);

        // Recalcular. Para un cliente del ERP el tipo sale de su ficha; para un cliente nuevo
        // sale del snapshot con el que se cotizó, porque no hay ficha de donde sacarlo.
        var amortizables = request.Amortizables is { Count: > 0 }
            ? request.Amortizables.Select(a => (a.ActivoCodigo, a.Cantidad, a.MesesVidaUtil)).ToList()
            : null;

        var resultado = await _servicio.CalcularAsync(
            cotizacion.ClienteCodigo,
            cotizacion.ClienteCodigo is null ? cotizacion.TipoClienteId : null,
            request.Fecha,
            request.Margen,
            request.Insumos.Select(i => (i.ProductoCodigo, i.Cantidad, i.SemestreId)).ToList(),
            amortizables,
            cancellationToken);

        var tc = await _tipoCambio.ObtenerVigenteAsync(request.Fecha, cancellationToken);

        cotizacion.TipoServicioCodigo = tipoServicio.Codigo;
        cotizacion.TipoServicioNombre = tipoServicio.Nombre;
        cotizacion.Fecha = request.Fecha;
        cotizacion.ModalidadId = modalidadId;
        cotizacion.ModalidadCodigo = modalidadCodigo;
        cotizacion.ModalidadNombre = modalidadNombre;
        cotizacion.TecnicoCodigo = tecnico?.Codigo;
        cotizacion.TecnicoNombre = tecnico?.Nombre;
        cotizacion.FechaEstimada = request.FechaEstimada;
        cotizacion.DireccionServicio = request.DireccionServicio;
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
        cotizacion.HashCalculo = resultado.HashCalculo;
        cotizacion.ParametrosCalculo = resultado.ParametrosCalculo;
        cotizacion.UpdatedAt = DateTime.UtcNow;

        // La actualización es de reemplazo: lo que no se manda se borra. Las líneas se rearmen
        // con los códigos ERP y sus snapshots, con el mismo costo que usó el motor.
        _contexto.DetalleInsumo.RemoveRange(_contexto.DetalleInsumo.Where(d => d.CotizacionId == id));
        _contexto.DetalleAmortizable.RemoveRange(_contexto.DetalleAmortizable.Where(d => d.CotizacionId == id));
        await GuardarDetallesAsync(id, request.Insumos, request.Amortizables, cancellationToken);

        await _contexto.SaveChangesAsync(cancellationToken);

        return ApiResponse<CotizacionResponse>.Ok(Responder(cotizacion)).AHttp();
    }

    /// <summary>
    /// Escribe las líneas de insumo y de activo con el <b>mismo costo que usó el motor</b>:
    /// ambas consultas pasan por <see cref="CatalogoErpService.ArticulosAsync"/>. Si este lado
    /// resolviera por su cuenta, la fila guardada podría decir un precio distinto al del total y
    /// el PDF —que lee las filas— no cuadraría con lo que el cliente pagó.
    /// <para>La vida útil NO viene del ERP: la trae cada línea y es &gt; 0 (H-04).</para>
    /// </summary>
    private async Task GuardarDetallesAsync(
        Guid cotizacionId,
        IReadOnlyList<CotizacionInsumoRequest> insumos,
        IReadOnlyList<CotizacionAmortizableRequest>? amortizables,
        CancellationToken cancellationToken)
    {
        var articulosInsumo = await _catalogoErp.ArticulosAsync(
            insumos.Select(i => i.ProductoCodigo), soloAmortizables: false, cancellationToken);

        short orden = 1;
        foreach (var linea in insumos)
        {
            var codigo = Texto(linea.ProductoCodigo)
                ?? throw new InvalidOperationException("Insumo: el código es obligatorio.");

            if (!articulosInsumo.TryGetValue(codigo, out var articulo))
            {
                throw new InvalidOperationException(
                    $"Producto: el código '{codigo}' no existe o está inactivo en el catálogo ERP.");
            }

            _contexto.DetalleInsumo.Add(new DetalleInsumo
            {
                Id = Guid.NewGuid(),
                CotizacionId = cotizacionId,
                ProductoCodigo = articulo.Codigo,
                ProductoNombre = articulo.Nombre,
                UnidadMedidaTexto = articulo.UnidadMedida,
                Cantidad = linea.Cantidad,
                PrecioUnitario = articulo.CostoUnitario,
                Total = articulo.CostoUnitario * linea.Cantidad,
                SemestreId = linea.SemestreId,
                Orden = orden++
            });
        }

        if (amortizables is not { Count: > 0 })
        {
            return;
        }

        var articulosActivo = await _catalogoErp.ArticulosAsync(
            amortizables.Select(a => a.ActivoCodigo), soloAmortizables: true, cancellationToken);

        orden = 1;
        foreach (var linea in amortizables)
        {
            var codigo = Texto(linea.ActivoCodigo)
                ?? throw new InvalidOperationException("Activo amortizable: el código es obligatorio.");

            if (!articulosActivo.TryGetValue(codigo, out var articulo))
            {
                throw new InvalidOperationException(
                    $"Activo amortizable: el código '{codigo}' no existe, está inactivo o no es amortizable en el ERP.");
            }

            if (linea.MesesVidaUtil is not > 0)
            {
                throw new InvalidOperationException(
                    $"El activo \"{articulo.Nombre}\" necesita meses de vida útil mayores a cero. " +
                    "El ERP no trae ese dato: ingréselos en la línea del activo.");
            }

            var total = articulo.CostoUnitario * linea.Cantidad;
            _contexto.DetalleAmortizable.Add(new DetalleAmortizable
            {
                Id = Guid.NewGuid(),
                CotizacionId = cotizacionId,
                ActivoCodigo = articulo.Codigo,
                ActivoNombre = articulo.Nombre,
                UnidadMedidaTexto = articulo.UnidadMedida,
                Cantidad = linea.Cantidad,
                PrecioUnitario = articulo.CostoUnitario,
                MesesVidaUtil = linea.MesesVidaUtil.Value,
                Total = total,
                ValorMensual = total / linea.MesesVidaUtil.Value,
                Orden = orden++
            });
        }
    }

    private async Task<(byte? Id, string? Codigo, string? Nombre)> ResolverModalidadAsync(
        string? codigo,
        CancellationToken cancellationToken)
    {
        var texto = Texto(codigo);
        if (texto is null)
        {
            return (null, null, null);
        }

        var modalidad = await _contexto.ModalidadCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Codigo == texto && m.EsActivo, cancellationToken)
            ?? throw new InvalidOperationException("La modalidad no existe o está inactiva.");

        return (modalidad.Id, modalidad.Codigo, modalidad.Nombre);
    }

    /// <summary>Anula una cotización. Estado terminal: la fila y su historial se conservan.</summary>
    [HttpPut("{id:guid}/anular")]
    [Authorize("COTIZACIONES.ANULAR")]
    public async Task<IResult> Anular(
        Guid id,
        [FromBody] AnularCotizacionRequest request,
        CancellationToken cancellationToken)
    {
        var cotizacion = await _contexto.Cotizacion
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("La cotización no existe.");

        if (cotizacion.EstadoCodigo == "ANULADA")
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
        cotizacion.EstadoCodigo = estadoAnulada.Codigo;
        cotizacion.EstadoNombre = estadoAnulada.Nombre;
        cotizacion.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return ApiResponse<CotizacionResponse>.Ok(Responder(cotizacion)).AHttp();
    }

    private static CotizacionResponse Responder(Cotizacion c) => new(
        c.Id,
        c.Numero,
        c.Anio,
        c.ClienteCodigo,
        c.ClienteNombre,
        c.TipoServicioCodigo,
        c.TipoServicioNombre,
        c.Fecha,
        c.ModalidadCodigo ?? string.Empty,
        c.MargenAplicado,
        c.EstadoCodigo,
        c.EstadoNombre,
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
        c.CreatedAt);

    private static string? Texto(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>Solicitud para anular una cotización. El motivo es obligatorio.</summary>
public sealed record AnularCotizacionRequest(string Motivo);

/// <summary>Línea de insumo de una cotización existente, para el detalle y la edición.</summary>
public sealed record DetalleInsumoResponse(
    string ProductoCodigo,
    decimal Cantidad,
    byte? SemestreId);

/// <summary>
/// Línea de activo amortizable ya guardada. <c>MesesVidaUtil</c> es la vida útil efectivamente
/// usada: en el ERP no existe ese dato, siempre la escribió quien cotizó. Se devuelve para que el
/// formulario recargue el mismo número y el PDF no difiera del total.
/// </summary>
public sealed record DetalleAmortizableResponse(
    string ActivoCodigo,
    decimal Cantidad,
    int MesesVidaUtil,
    decimal PrecioUnitario,
    decimal ValorMensual,
    string ActivoNombre,
    string UnidadMedidaTexto);

/// <summary>
/// Una cotización con sus líneas. El listado usa <c>CotizacionResponse</c>; el detalle y la
/// edición usan esta, que trae las líneas para que editar sea recargar y no rearmar de memoria.
/// </summary>
public sealed record CotizacionDetalleResponse(
    Guid Id,
    string Numero,
    int Anio,
    string? ClienteCodigo,
    string ClienteNombre,
    byte TipoClienteId,
    string TipoServicioCodigo,
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
    IReadOnlyList<DetalleInsumoResponse> Insumos,
    IReadOnlyList<DetalleAmortizableResponse> Amortizables,
    string? TecnicoCodigo,
    DateOnly? FechaEstimada,
    string? DireccionServicio);

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
