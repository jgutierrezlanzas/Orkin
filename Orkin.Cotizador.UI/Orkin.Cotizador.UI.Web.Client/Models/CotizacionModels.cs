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

public sealed class CotizacionDetalleModel
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public Guid? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public Guid TipoServicioId { get; set; }
    public string TipoServicioNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public decimal Margen { get; set; }
    public string EstadoCodigo { get; set; } = string.Empty;
    public string EstadoNombre { get; set; } = string.Empty;
    public decimal PrecioCordobas { get; set; }
    public decimal PrecioUsd { get; set; }
    public List<DetalleInsumoModel> Insumos { get; set; } = [];
}

public sealed class DetalleInsumoModel
{
    public Guid ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public byte? SemestreId { get; set; }
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
