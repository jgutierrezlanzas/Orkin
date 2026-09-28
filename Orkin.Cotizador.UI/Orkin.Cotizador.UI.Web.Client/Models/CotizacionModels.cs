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
    public Guid ClienteId { get; set; }
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
    public List<ModalidadOpcion> Modalidades { get; set; } = [];
}

public sealed class ClienteOpcion
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? TipoClienteNombre { get; set; }
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
