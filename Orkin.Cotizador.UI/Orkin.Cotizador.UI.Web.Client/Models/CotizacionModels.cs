namespace Orkin.Cotizador.UI.Web.Client.Models;

public sealed class LineaInsumoRequest
{
    /// <summary>Código de <c>vvArticulos</c>.</summary>
    public string ProductoCodigo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; } = 1;
    public byte? SemestreId { get; set; }
}

public sealed class CotizacionCalculadaResponse
{
    public decimal TotalInsumosSemestre1 { get; set; }
    public decimal TotalInsumosSemestre2 { get; set; }
    public decimal TotalInsumos { get; set; }
    public decimal TotalAmortizablesMensual { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal PrecioCordobas { get; set; }
    public decimal PrecioUsd { get; set; }
    public decimal MargenAplicado { get; set; }
    public decimal TipoCambioValor { get; set; }
    public DateOnly TipoCambioFecha { get; set; }
    public Guid? TipoCambioId { get; set; }
    public byte MetodoCosteoId { get; set; }
    public decimal? PorcentajePlantilla { get; set; }
}

public sealed class CotizacionListItem
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int Anio { get; set; }
    /// <summary>Null cuando la cotización es para un cliente nuevo que no tiene ficha.</summary>
    public string? ClienteCodigo { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public byte TipoClienteId { get; set; }
    public string TipoServicioCodigo { get; set; } = string.Empty;
    public string TipoServicioNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string ModalidadCodigo { get; set; } = string.Empty;
    public decimal Margen { get; set; }
    public string EstadoCodigo { get; set; } = string.Empty;
    public string EstadoNombre { get; set; } = string.Empty;
    public decimal TotalInsumosSemestre1 { get; set; }
    public decimal TotalInsumosSemestre2 { get; set; }
    public decimal TotalInsumos { get; set; }
    public decimal TotalAmortizablesMensual { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal PrecioCordobas { get; set; }
    public decimal PrecioUsd { get; set; }
    public decimal TipoCambioValor { get; set; }
    public DateOnly TipoCambioFecha { get; set; }
    public Guid? TipoCambioId { get; set; }
    public byte MetodoCosteoId { get; set; }
    public decimal? PorcentajePlantilla { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Una cotización con sus dos clases de líneas. Trae <b>todo</b> lo que el formulario muestra,
/// sin excepción: un campo que la pantalla muestra y la respuesta no trae aparece vacío al abrir
/// para editar, y quien guarde sin mirar se lleva la sorpresa de que ese campo se borró.
/// </summary>
public sealed class CotizacionDetalleModel
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int Anio { get; set; }
    public string? ClienteCodigo { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public byte TipoClienteId { get; set; }
    public string TipoServicioCodigo { get; set; } = string.Empty;
    public string TipoServicioNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string ModalidadCodigo { get; set; } = string.Empty;
    public decimal Margen { get; set; }
    public string EstadoCodigo { get; set; } = string.Empty;
    public string EstadoNombre { get; set; } = string.Empty;

    /// <summary>Código de <c>vvTecnicos</c>. Null = sin técnico asignado.</summary>
    public string? TecnicoCodigo { get; set; }

    public DateOnly? FechaEstimada { get; set; }
    public string? DireccionServicio { get; set; }

    public decimal TotalInsumosSemestre1 { get; set; }
    public decimal TotalInsumosSemestre2 { get; set; }
    public decimal TotalInsumos { get; set; }
    public decimal TotalAmortizablesMensual { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal PrecioCordobas { get; set; }
    public decimal PrecioUsd { get; set; }
    public decimal TipoCambioValor { get; set; }
    public DateOnly TipoCambioFecha { get; set; }
    public Guid? TipoCambioId { get; set; }
    public byte MetodoCosteoId { get; set; }
    public decimal? PorcentajePlantilla { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<DetalleInsumoModel> Insumos { get; set; } = [];
    public List<DetalleAmortizableModel> Amortizables { get; set; } = [];
}

public sealed class DetalleInsumoModel
{
    public string ProductoCodigo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public byte? SemestreId { get; set; }
}

/// <summary>Línea de activo amortizable ya guardada, con la vida útil efectivamente usada.</summary>
public sealed class DetalleAmortizableModel
{
    public string ActivoCodigo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public int MesesVidaUtil { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal ValorMensual { get; set; }
    public string ActivoNombre { get; set; } = string.Empty;
    public string UnidadMedidaTexto { get; set; } = string.Empty;
}

/// <summary>
/// Todo lo que el formulario entrega al guardar, sea alta o edición. La pantalla decide qué
/// llamada hacer; el componente no lo sabe ni lo necesita.
/// </summary>
public sealed class CotizacionFormularioDatos
{
    public string? ClienteCodigo { get; set; }
    public string? ClienteNombre { get; set; }
    public byte? TipoClienteId { get; set; }
    public string TipoServicioCodigo { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string? ModalidadCodigo { get; set; }
    public string? TecnicoCodigo { get; set; }
    public DateOnly? FechaEstimada { get; set; }
    public string? DireccionServicio { get; set; }

    /// <summary>Margen como fracción (0.30 = 30 %). La pantalla lo muestra en porcentaje.</summary>
    public decimal Margen { get; set; }

    public List<LineaInsumoRequest> Insumos { get; set; } = [];
    public List<LineaAmortizableRequest> Amortizables { get; set; } = [];
}

public sealed class PaginatedCotizaciones
{
    public List<CotizacionListItem> Items { get; set; } = [];
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanioPagina { get; set; }
    public int TotalPaginas { get; set; }
    public bool TienePaginaAnterior { get; set; }
    public bool TienePaginaSiguiente { get; set; }
}

/// <summary>Combos del cotizador: códigos del ERP, nombres para mostrar.</summary>
public sealed class CotizacionOpciones
{
    public List<ClienteOpcion> Clientes { get; set; } = [];
    public List<TipoServicioOpcion> TiposServicio { get; set; } = [];
    public List<TipoClienteOpcionModel> TiposCliente { get; set; } = [];
    public List<ModalidadOpcionModel> Modalidades { get; set; } = [];
    public List<ArticuloOpcionModel> Articulos { get; set; } = [];
    public List<ArticuloOpcionModel> ActivosAmortizables { get; set; } = [];
    public List<TecnicoOpcionModel> Tecnicos { get; set; } = [];
    public List<string> Unidades { get; set; } = [];
    public List<string> Clasificaciones { get; set; } = [];
}

public sealed class ModalidadOpcionModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>Artículo de <c>vvArticulos</c>: insumo o activo amortizable.</summary>
public sealed class ArticuloOpcionModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public string? Clasificacion { get; set; }
    public bool Amortizable { get; set; }

    /// <summary>Siempre vacío: el ERP no trae vida útil, la pone quien cotiza.</summary>
    public int? MesesVidaUtil { get; set; }
}

public sealed class TecnicoOpcionModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Iniciales { get; set; }
}

/// <summary>Línea de activo amortizable en la cotización.</summary>
public sealed class LineaAmortizableRequest
{
    public string ActivoCodigo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; } = 1;

    /// <summary>
    /// Vida útil en meses PARA ESTA COTIZACIÓN. Es obligatoria: <c>vvArticulos</c> no trae ese
    /// dato. La misma trampa a la intemperie dura 6 meses y bajo techo 24; quien cotiza lo sabe.
    /// </summary>
    public int? MesesVidaUtil { get; set; }
}

public sealed class ClienteOpcion
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? TipoClienteNombre { get; set; }
}

/// <summary>
/// Tipo de cliente para el caso "cliente nuevo": lo elige quien cotiza y decide el costeo.
/// </summary>
public sealed class TipoClienteOpcionModel
{
    public byte Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class TipoServicioOpcion
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? TipoActividad { get; set; }
}

public sealed class ModalidadOpcion
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
