namespace Orkin.Cotizador.Api.DTOs;

/// <summary>Línea de insumo para crear/actualizar una cotización.</summary>
public sealed record CotizacionInsumoRequest(
    Guid ProductoId,
    decimal Cantidad,
    byte? SemestreId);

/// <summary>Línea de activo amortizable para crear/actualizar una cotización.</summary>
public sealed record CotizacionAmortizableRequest(
    Guid ActivoId,
    decimal Cantidad,
    int? MesesVidaUtil = null);

/// <summary>Solicitud para calcular una cotización en memoria (preview).</summary>
/// <param name="ClienteId">Cliente del catálogo. Null = cliente nuevo, todavía sin ficha.</param>
/// <param name="TipoClienteId">Solo para cliente nuevo: lo elige quien cotiza y decide el método
/// de costeo. Se ignora cuando hay <c>ClienteId</c>.</param>
public sealed record CalcularCotizacionRequest(
    Guid? ClienteId,
    byte? TipoClienteId,
    DateOnly Fecha,
    decimal Margen,
    IReadOnlyList<CotizacionInsumoRequest> Insumos,
    IReadOnlyList<CotizacionAmortizableRequest> Amortizables);

/// <summary>Resultado del cálculo para mostrar en la UI.</summary>
public sealed record CalculoCotizacionResponse(
    decimal TotalInsumosSemestre1,
    decimal TotalInsumosSemestre2,
    decimal TotalInsumos,
    decimal TotalAmortizablesMensual,
    decimal CostoTotal,
    decimal PrecioCordobas,
    decimal PrecioUsd,
    decimal MargenAplicado,
    decimal TipoCambioValor,
    DateOnly TipoCambioFecha,
    Guid? TipoCambioId,
    byte MetodoCosteoId,
    decimal? PorcentajePlantilla);

/// <summary>Solicitud para crear una cotización.</summary>
/// <param name="ClienteId">Cliente del catálogo. Null = cliente nuevo, todavía sin ficha.</param>
/// <param name="ClienteNombre">Nombre para el cliente nuevo, cuando no hay <c>ClienteId</c>.
/// Para un cliente del catálogo se ignora y se copia el de la ficha.</param>
/// <param name="TipoClienteId">Tipo para el cliente nuevo. Se ignora cuando hay <c>ClienteId</c>.</param>
/// <param name="TecnicoAsignadoId">Técnico al que se le asigna el trabajo. Opcional.</param>
/// <param name="FechaEstimada">Cuándo se proyecta ejecutar el trabajo. Opcional.</param>
/// <param name="DireccionServicio">Dónde se hace el trabajo. Opcional.</param>
/// <para>Los tres últimos son opcionales y van al final a propósito: el formulario de creación y el
/// de edición los ofrecen los dos, y sin esta forma compartida cada unoirlos por su lado — que es
/// exactamente como se perdieron la primera vez.</para>
public sealed record CrearCotizacionRequest(
    Guid? ClienteId,
    string? ClienteNombre,
    byte? TipoClienteId,
    Guid TipoServicioId,
    DateOnly Fecha,
    string? ModalidadCodigo,
    decimal Margen,
    IReadOnlyList<CotizacionInsumoRequest> Insumos,
    IReadOnlyList<CotizacionAmortizableRequest> Amortizables,
    Guid? TecnicoAsignadoId = null,
    DateOnly? FechaEstimada = null,
    string? DireccionServicio = null);

/// <summary>Respuesta de una cotización creada/actualizada.</summary>
public sealed record CotizacionResponse(
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
    DateTime CreatedAt);

/// <summary>Solicitud para actualizar una cotización (solo en estado BORRADOR/RECHAZADA).</summary>
/// <summary>
/// Solicitud para actualizar una cotización (solo en estado BORRADOR/RECHAZADA).
/// <para>Tiene <b>exactamente los mismos campos</b> que <see cref="CrearCotizacionRequest"/>,
/// menos el cliente. Esa simetría es a propósito: el formulario de edición y el de creación son
/// el mismo componente, y si el DTO de uno tuviera un campo menos el guardado de la edición
/// mandaría <c>null</c> y borraría lo que ya estaba, en silencio.</para>
/// <para>El cliente no se cambia: ya viajó con ese nombre y su tipo decide el método de costeo,
/// así que cambiarlo obligaría a re-etiquetar una cotización ya emitida.</para>
/// </summary>
public sealed record ActualizarCotizacionRequest(
    Guid TipoServicioId,
    DateOnly Fecha,
    string? ModalidadCodigo,
    decimal Margen,
    IReadOnlyList<CotizacionInsumoRequest> Insumos,
    IReadOnlyList<CotizacionAmortizableRequest> Amortizables,
    Guid? TecnicoAsignadoId = null,
    DateOnly? FechaEstimada = null,
    string? DireccionServicio = null);

/// <summary>Opción para dropdown de clientes.</summary>
public sealed record ClienteOption(
    Guid Id,
    string Codigo,
    string Nombre,
    string? TipoClienteNombre);

/// <summary>Opción para dropdown de tipos de servicio.</summary>
public sealed record TipoServicioOption(
    Guid Id,
    string Codigo,
    string Nombre,
    bool EsResidencialPorDefecto,
    bool EsEmpresarialPorDefecto);

/// <summary>
/// Opción para dropdown de tipos de cliente. Va en las opciones de cotización para el caso
/// "cliente nuevo": quien cotiza elige el tipo, que es lo que decide el método de costeo.
/// </summary>
public sealed record TipoClienteOpcion(
    byte Id,
    string Codigo,
    string Nombre);

/// <summary>Opción para dropdown de modalidades.</summary>
public sealed record ModalidadOption(
    string Codigo,
    string Nombre);