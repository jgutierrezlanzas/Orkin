namespace Orkin.Cotizador.Api.DTOs;

/// <summary>Línea de insumo para crear/actualizar una cotización.</summary>
/// <param name="ProductoCodigo"><c>vvArticulos.CODIGO_ARTICULO</c>.</param>
public sealed record CotizacionInsumoRequest(
    string ProductoCodigo,
    decimal Cantidad,
    byte? SemestreId);

/// <summary>Línea de activo amortizable para crear/actualizar una cotización.</summary>
/// <param name="ActivoCodigo"><c>vvArticulos.CODIGO_ARTICULO</c> con <c>AMORTIZABLE = SI</c>.</param>
/// <param name="MesesVidaUtil">
/// Vida útil de ESTA cotización. El ERP no trae ese dato (D-24), así que es obligatorio y &gt; 0.
/// </param>
public sealed record CotizacionAmortizableRequest(
    string ActivoCodigo,
    decimal Cantidad,
    int? MesesVidaUtil = null);

/// <summary>Solicitud para calcular una cotización en memoria (preview).</summary>
/// <param name="ClienteCodigo">Código ERP. Null = cliente nuevo, todavía sin ficha.</param>
/// <param name="TipoClienteId">Solo para cliente nuevo: lo elige quien cotiza y decide el método
/// de costeo. Se ignora cuando hay <c>ClienteCodigo</c>.</param>
public sealed record CalcularCotizacionRequest(
    string? ClienteCodigo,
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
/// <param name="ClienteCodigo">Código ERP. Null = cliente nuevo, todavía sin ficha.</param>
/// <param name="ClienteNombre">Nombre para el cliente nuevo, cuando no hay <c>ClienteCodigo</c>.
/// Para un cliente del ERP se ignora y se copia el nombre de la vista.</param>
/// <param name="TipoClienteId">Tipo para el cliente nuevo. Se ignora cuando hay <c>ClienteCodigo</c>.</param>
/// <param name="TipoServicioCodigo">Código de <c>vvTipoServicios.CODIGO</c>.</param>
/// <param name="TecnicoCodigo">Código de <c>vvTecnicos.CODIGO</c>. Opcional.</param>
/// <para>Técnico, fecha estimada y dirección son opcionales y van al final a propósito: alta y
/// edición los ofrecen los dos y sin esta forma compartida se perderían otra vez.</para>
public sealed record CrearCotizacionRequest(
    string? ClienteCodigo,
    string? ClienteNombre,
    byte? TipoClienteId,
    string TipoServicioCodigo,
    DateOnly Fecha,
    string? ModalidadCodigo,
    decimal Margen,
    IReadOnlyList<CotizacionInsumoRequest> Insumos,
    IReadOnlyList<CotizacionAmortizableRequest> Amortizables,
    string? TecnicoCodigo = null,
    DateOnly? FechaEstimada = null,
    string? DireccionServicio = null);

/// <summary>Respuesta de una cotización creada/actualizada.</summary>
public sealed record CotizacionResponse(
    Guid Id,
    string Numero,
    int Anio,
    string? ClienteCodigo,
    string ClienteNombre,
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
    DateTime CreatedAt);

/// <summary>
/// Solicitud para actualizar una cotización (solo en estado BORRADOR/RECHAZADA).
/// <para>Tiene <b>exactamente los mismos campos</b> que <see cref="CrearCotizacionRequest"/>,
/// menos el cliente. Esa simetría es a propósito: alta y edición son el mismo componente, y un
/// DTO con un campo menos mandaría <c>null</c> y borraría lo que ya estaba, en silencio.</para>
/// <para>El cliente no se cambia: ya viajó con ese nombre y su tipo decide el método de costeo.</para>
/// </summary>
public sealed record ActualizarCotizacionRequest(
    string TipoServicioCodigo,
    DateOnly Fecha,
    string? ModalidadCodigo,
    decimal Margen,
    IReadOnlyList<CotizacionInsumoRequest> Insumos,
    IReadOnlyList<CotizacionAmortizableRequest> Amortizables,
    string? TecnicoCodigo = null,
    DateOnly? FechaEstimada = null,
    string? DireccionServicio = null);

/// <summary>Cliente activo de <c>vvClientes</c>. El valor que se guarda es <see cref="Codigo"/>.</summary>
public sealed record ClienteOption(
    string Codigo,
    string Nombre,
    string? TipoClienteNombre);

/// <summary>Tipo de servicio activo de <c>vvTipoServicios</c>.</summary>
public sealed record TipoServicioOption(
    string Codigo,
    string Nombre,
    string? TipoActividad);

/// <summary>Tipo de cliente: catálogo propio de la plataforma, no copia del ERP.</summary>
public sealed record TipoClienteOpcion(
    byte Id,
    string Codigo,
    string Nombre);

/// <summary>Modalidad: catálogo propio de la plataforma, no copia del ERP.</summary>
public sealed record ModalidadOption(
    string Codigo,
    string Nombre);

/// <summary>Artículo activo de <c>vvArticulos</c>: insumo o activo amortizable.</summary>
/// <param name="Amortizable"><c>AMORTIZABLE = SI</c>. Decide en qué tabla de la pantalla aparece.</param>
/// <param name="MesesVidaUtil">Siempre null: el ERP no trae vida útil (D-24).</param>
public sealed record ArticuloCotizacionOption(
    string Codigo,
    string Nombre,
    decimal Costo,
    string UnidadMedida,
    string? Clasificacion,
    bool Amortizable,
    int? MesesVidaUtil = null);

/// <summary>Técnico activo de <c>vvTecnicos</c>.</summary>
public sealed record TecnicoCotizacionOption(
    string Codigo,
    string Nombre,
    string? Iniciales);

/// <summary>
/// Un solo contrato para los combos del cotizador. Los catálogos del ERP llegan por código; los
/// tipos de cliente y las modalidades siguen siendo catálogos propios de la plataforma.
/// </summary>
public sealed record CotizacionOpcionesResponse(
    IReadOnlyList<ClienteOption> Clientes,
    IReadOnlyList<TipoServicioOption> TiposServicio,
    IReadOnlyList<TipoClienteOpcion> TiposCliente,
    IReadOnlyList<ModalidadOption> Modalidades,
    IReadOnlyList<ArticuloCotizacionOption> Articulos,
    IReadOnlyList<ArticuloCotizacionOption> ActivosAmortizables,
    IReadOnlyList<TecnicoCotizacionOption> Tecnicos,
    IReadOnlyList<string> Unidades,
    IReadOnlyList<string> Clasificaciones);
