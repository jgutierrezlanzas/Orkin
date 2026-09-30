using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Orkin.Cotizador.Api.Pdf;

/// <summary>
/// Genera el PDF de una cotización — el documento que viaja al cliente.
/// <para>El PDF es un <b>snapshot</b> de la cotización: muestra lo que estaba guardado al
/// momento de generarlo, con los mismos números que la pantalla. No recálcula nada ni consulta
/// nada que no esté en la fila y sus insumos, porque un documento que puede cambiar debajo de
/// quien lo está leyendo no es un documento.</para>
/// <para>El rojo corporativo va en los bordes y en los totales; el resto en negro sobre blanco.
/// Un PDF de cotización que parece un formulario del gobierno no vende un servicio de control
/// de plagas.</para>
/// </summary>
public sealed class GeneradorPdfCotizacion
{
    private readonly CotizadorDbContext _contexto;

    private const string RojoCorporativo = "#E31837";

    public GeneradorPdfCotizacion(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>Genera el PDF de una cotización como bytes, listo para descargar o enviar.</summary>
    public async Task<byte[]> GenerarAsync(Guid cotizacionId, CancellationToken cancellationToken = default)
    {
        var cotizacion = await _contexto.Cotizacion
            .AsNoTracking()
            .Include(c => c.Estado)
            .Include(c => c.TipoServicio)
            .Include(c => c.Modalidad)
            .FirstOrDefaultAsync(c => c.Id == cotizacionId, cancellationToken)
            ?? throw new KeyNotFoundException("La cotización no existe.");

        var insumos = await _contexto.DetalleInsumo
            .AsNoTracking()
            .Where(d => d.CotizacionId == cotizacionId)
            .OrderBy(d => d.ProductoNombre)
            .ToListAsync(cancellationToken);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text("ORKIN")
                            .FontSize(28)
                            .Bold()
                            .FontColor(RojoCorporativo);

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text($"COTIZACIÓN {cotizacion.Numero}").FontSize(14).Bold();
                            col.Item().Text($"Fecha: {cotizacion.Fecha:dd/MM/yyyy}").FontSize(10);
                            col.Item().Text($"Estado: {cotizacion.Estado?.Nombre ?? ""}").FontSize(10);
                        });
                    });

                    column.Item().PaddingTop(4).Height(3).Background(RojoCorporativo);
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    // ── Cliente y servicio ──────────────────────────────────────────────
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Cliente").FontSize(9).FontColor("#888888");
                            col.Item().Text(cotizacion.ClienteNombre).FontSize(12).Bold();
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Servicio").FontSize(9).FontColor("#888888");
                            col.Item().Text(cotizacion.TipoServicio?.Nombre ?? "").FontSize(12);
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Modalidad").FontSize(9).FontColor("#888888");
                            col.Item().Text(cotizacion.Modalidad?.Nombre ?? "—").FontSize(12);
                        });
                    });

                    column.Item().PaddingTop(16);

                    // ── Insumos ────────────────────────────────────────────────────────
                    column.Item().Text("Insumos").FontSize(12).Bold().FontColor(RojoCorporativo);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#F5F5F5").Padding(4).Text("#").FontSize(9).Bold();
                            header.Cell().Background("#F5F5F5").Padding(4).Text("Producto").FontSize(9).Bold();
                            header.Cell().Background("#F5F5F5").Padding(4).AlignRight().Text("UM").FontSize(9).Bold();
                            header.Cell().Background("#F5F5F5").Padding(4).AlignRight().Text("Cant.").FontSize(9).Bold();
                            header.Cell().Background("#F5F5F5").Padding(4).AlignRight().Text("Costo unit.").FontSize(9).Bold();
                            header.Cell().Background("#F5F5F5").Padding(4).AlignRight().Text("Total").FontSize(9).Bold();
                        });

                        var fila = 0;
                        foreach (var insumo in insumos)
                        {
                            fila++;
                            var fondo = fila % 2 == 0 ? "#FAFAFA" : "#FFFFFF";

                            table.Cell().Background(fondo).Padding(4).Text(fila.ToString()).FontSize(9);
                            table.Cell().Background(fondo).Padding(4).Text(insumo.ProductoNombre).FontSize(9);
                            table.Cell().Background(fondo).Padding(4).AlignRight().Text(insumo.UnidadMedidaTexto).FontSize(9);
                            table.Cell().Background(fondo).Padding(4).AlignRight().Text(insumo.Cantidad.ToString("N2")).FontSize(9);
                            table.Cell().Background(fondo).Padding(4).AlignRight().Text(insumo.PrecioUnitario.ToString("N2")).FontSize(9);
                            table.Cell().Background(fondo).Padding(4).AlignRight().Text((insumo.PrecioUnitario * insumo.Cantidad).ToString("N2")).FontSize(9);
                        }
                    });

                    column.Item().PaddingTop(16);

                    // ── Totales ────────────────────────────────────────────────────────
                    column.Item().PaddingLeft(180).Column(totales =>
                    {
                        totales.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Total insumos").FontSize(10);
                            row.ConstantItem(80).AlignRight().Text(cotizacion.TotalInsumos.ToString("C")).FontSize(10);
                        });
                        totales.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Amortizables mensual").FontSize(10);
                            row.ConstantItem(80).AlignRight().Text(cotizacion.TotalAmortizablesMensual.ToString("C")).FontSize(10);
                        });
                        totales.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Costo total").FontSize(10).Bold();
                            row.ConstantItem(80).AlignRight().Text(cotizacion.CostoTotal.ToString("C")).FontSize(10).Bold();
                        });
                        totales.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Margen ({cotizacion.MargenAplicado:P1})").FontSize(10);
                            row.ConstantItem(80).AlignRight().Text((cotizacion.CostoTotal * cotizacion.MargenAplicado).ToString("C")).FontSize(10);
                        });
                        totales.Item().PaddingTop(6).Height(2).Background(RojoCorporativo);
                        totales.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text("PRECIO").FontSize(14).Bold().FontColor(RojoCorporativo);
                            row.ConstantItem(100).AlignRight().Text(cotizacion.PrecioCordobas.ToString("C")).FontSize(14).Bold().FontColor(RojoCorporativo);
                        });
                        totales.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Equivalente en dólares").FontSize(10);
                            row.ConstantItem(100).AlignRight().Text($"US$ {cotizacion.PrecioUsd:N2}").FontSize(10);
                        });
                        totales.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"TC aplicado: {cotizacion.TipoCambioValor:N4} ({cotizacion.TipoCambioFecha:dd/MM/yyyy})").FontSize(8).FontColor("#888888");
                            row.ConstantItem(100).AlignRight().Text($"Plantilla: {cotizacion.PorcentajePlantilla:P1}").FontSize(8).FontColor("#888888");
                        });
                    });

                    // ── Vigencia ───────────────────────────────────────────────────────
                    if (cotizacion.FechaVigencia is { } vigencia)
                    {
                        column.Item().PaddingTop(16).Text(
                            $"Esta cotización es válida hasta el {vigencia:dd/MM/yyyy} " +
                            $"({cotizacion.VigenciaDias} días desde la emisión).")
                            .FontSize(9).FontColor("#666666");
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text(
                        $"ORKIN · Control de Plagas · {cotizacion.Numero} · " +
                        $"Generado el {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC")
                        .FontSize(8)
                        .FontColor("#999999");
                    row.RelativeItem().AlignRight().Text("Página 1 de 1").FontSize(8).FontColor("#999999");
                });
            });
        }).GeneratePdf();
    }
}
