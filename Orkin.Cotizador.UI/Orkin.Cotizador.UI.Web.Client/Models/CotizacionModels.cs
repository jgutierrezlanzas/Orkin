namespace Orkin.Cotizador.UI.Web.Client.Models;

public sealed class LineaInsumoRequest
{
    public Guid ProductoId { get; set; }
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
    public Guid? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public byte TipoClienteId { get; set; }
    public Guid TipoServicioId { get; set; }
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
/// Una cotización con sus dos clases de líneas. El listado usa <see cref="CotizacionListItem"/>
/// (sin líneas, que es todo lo que hace falta para la tabla); el detalle y la edición usan esta,
/// que trae las líneas para que editar sea recargar la pantalla y no rearmar de memoria.
/// <para>Trae <b>todo</b> lo que el formulario necesita, sin excepción. Un campo que el
/// formulario muestra pero la respuesta no trae aparece vacío al abrir para editar, y quien
/// guarde sin mirar se lleva la sorpresa de que ese campo se borró: por eso el detalle es un
/// espejo completo de la cabecera, no un resumen.</para>
/// </summary>
public sealed class CotizacionDetalleModel
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int Anio { get; set; }
    public Guid? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public byte TipoClienteId { get; set; }
    public Guid TipoServicioId { get; set; }
    public string TipoServicioNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string ModalidadCodigo { get; set; } = string.Empty;
    public decimal Margen { get; set; }
    public string EstadoCodigo { get; set; } = string.Empty;
    public string EstadoNombre { get; set; } = string.Empty;

    /// <summary>Técnico al que se asignó el trabajo. Null = sin técnico asignado.</summary>
    public Guid? TecnicoAsignadoId { get; set; }

    /// <summary>Cuándo se proyecta ejecutar el trabajo. Null = no se agendó.</summary>
    public DateOnly? FechaEstimada { get; set; }

    /// <summary>Dónde se hace el trabajo. Null = no se indicó.</summary>
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
    public Guid ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public byte? SemestreId { get; set; }
}

/// <summary>
/// Línea de activo amortizable ya guardada. <see cref="MesesVidaUtil"/> es el valor con el que se
/// cotizó: si vino del catálogo y no se editó a mano, es el mismo número, y al recargar el
/// formulario se muestra igual — que es lo que importa, que la pantalla diga lo que se usó.
/// </summary>
public sealed class DetalleAmortizableModel
{
    public Guid ActivoAmortizableId { get; set; }
    public decimal Cantidad { get; set; }
    public int? MesesVidaUtil { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal ValorMensual { get; set; }
    public string ActivoNombre { get; set; } = string.Empty;
    public string UnidadMedidaTexto { get; set; } = string.Empty;
}

/// <summary>
/// Todo lo que el formulario de cotización entrega al guardar, sin importar si la pantalla es la
/// de alta o la de edición. El componente es uno solo y por eso el resultado es uno solo: quien
/// lo recibe decide si llama al alta o a la actualización, y no tiene que adivinar qué campos
/// existen en cada caso.
/// </summary>
public sealed class CotizacionFormularioDatos
{
    public Guid? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public byte? TipoClienteId { get; set; }
    public Guid TipoServicioId { get; set; }
    public DateOnly Fecha { get; set; }
    public string? ModalidadCodigo { get; set; }
    public Guid? TecnicoAsignadoId { get; set; }
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

public sealed class CotizacionOpciones
{
    public List<ClienteOpcion> Clientes { get; set; } = [];
    public List<TipoServicioOpcion> TiposServicio { get; set; } = [];
    public List<TipoClienteOpcionModel> TiposCliente { get; set; } = [];
    public List<ModalidadOpcionModel> Modalidades { get; set; } = [];
    public List<ActivoAmortizableOpcionModel> ActivosAmortizables { get; set; } = [];
    public List<TecnicoOpcionModel> Tecnicos { get; set; } = [];
}

/// <summary>Una modalidad de cotización: servicio único, recurrente por semestre…</summary>
public sealed class ModalidadOpcionModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>
/// Un activo amortizable elegible para la cotización: trampa, lámpara UV, cebadero.
/// A diferencia del insumo, se descuenta por meses dentro de la cotización.
/// </summary>
public sealed class ActivoAmortizableOpcionModel
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal CostoActual { get; set; }
    public int MesesVidaUtil { get; set; }
    public string UnidadAbreviatura { get; set; } = string.Empty;
}

/// <summary>Técnico al que se le asigna el trabajo. Por ahora salen de los usuarios activos.</summary>
public sealed class TecnicoOpcionModel
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>Línea de activo amortizable en la cotización.</summary>
public sealed class LineaAmortizableRequest
{
    public Guid ActivoId { get; set; }
    public decimal Cantidad { get; set; } = 1;

    /// <summary>
    /// Vida útil en meses PARA ESTA COTIZACIÓN. Si no viene, se usa la del catálogo. Es
    /// editable porque el valor del catálogo es el típico, no el real: una trampa a la
    /// intemperie dura 6 meses y la misma trampa bajo techo dura 24 — quien está cotizando
    /// lo sabe, el catálogo no.
    /// </summary>
    public int? MesesVidaUtil { get; set; }
}

public sealed class ClienteOpcion
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? TipoClienteNombre { get; set; }
}

/// <summary>
/// Tipo de cliente para el caso "cliente nuevo": quien cotiza lo elige y ese tipo decide el
/// método de costeo del precio.
/// </summary>
public sealed class TipoClienteOpcionModel
{
    public byte Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class TipoServicioOpcion
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsResidencialPorDefecto { get; set; }
    public bool EsEmpresarialPorDefecto { get; set; }
}

public sealed class ModalidadOpcion
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
