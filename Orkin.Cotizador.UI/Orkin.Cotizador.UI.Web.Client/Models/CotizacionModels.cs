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
    public decimal? PorcentajePlantilla { get; set; }
}
