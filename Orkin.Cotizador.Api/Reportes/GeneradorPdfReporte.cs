using System.Globalization;

using Orkin.Cotizador.Core.Application.Reportes;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Orkin.Cotizador.Api.Reportes;

/// <summary>
/// Genera el PDF de un reporte del WinForms a partir del dataset que devolvió el SP legacy.
/// No recalcula ni reordena: las columnas y los valores son los del SP, con encabezados
/// legibles y el bloque de firmas de <c>vvTipodeDocumentosFirma</c> (<c>CODIGO = '0003'</c>).
/// </summary>
public sealed class GeneradorPdfReporte
{
    private const string RojoCorporativo = "#E31837";
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Genera los bytes del PDF listos para descargar.</summary>
    public byte[] Generar(ReporteResultado resultado) => Document.Create(documento =>
    {
        documento.Page(pagina =>
        {
            pagina.Size(resultado.Definicion.Landscape
                ? PageSizes.A4.Landscape()
                : PageSizes.A4);
            pagina.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);

            pagina.Header().Column(cabecera =>
            {
                cabecera.Item().Row(fila =>
                {
                    fila.RelativeItem().Text("ORKIN")
                        .FontSize(20).Bold().FontColor(RojoCorporativo);
                    fila.RelativeItem().AlignRight().Column(datos =>
                    {
                        datos.Item().Text(resultado.Definicion.Nombre.ToUpperInvariant())
                            .FontSize(11).Bold();
                        datos.Item().Text($"Emitido el {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).FontColor("#666666");
                    });
                });
                cabecera.Item().PaddingTop(4).Height(2).Background(RojoCorporativo);
            });

            pagina.Content().PaddingVertical(10).Column(contenido =>
            {
                contenido.Item().Text(resultado.Definicion.Descripcion)
                    .FontSize(9).FontColor("#555555");
                contenido.Item().PaddingTop(2).Text(resultado.Subtitulo)
                    .FontSize(9).Bold();
                contenido.Item().PaddingTop(2).Text($"{resultado.Filas.Count} filas")
                    .FontSize(8).FontColor("#888888");

                contenido.Item().PaddingTop(8).Table(tabla =>
                {
                    var columnas = resultado.Definicion.Columnas;
                    tabla.ColumnsDefinition(definicion =>
                    {
                        foreach (var columna in columnas)
                        {
                            definicion.RelativeColumn(
                                columna.Titulo.Length > 14 ? 2 : 1);
                        }
                    });

                    tabla.Header(cabecera =>
                    {
                        foreach (var columna in columnas)
                        {
                            cabecera.Cell().Background("#F5F5F5").Padding(3)
                                .Text(columna.Titulo).FontSize(7).Bold();
                        }
                    });

                    var numero = 0;
                    foreach (var fila in resultado.Filas)
                    {
                        numero++;
                        var fondo = numero % 2 == 0 ? "#FAFAFA" : "#FFFFFF";
                        foreach (var columna in columnas)
                        {
                            var valor = fila.TryGetValue(columna.Clave, out var contenidoCelda)
                                ? contenidoCelda
                                : null;
                            tabla.Cell().Background(fondo).Padding(3)
                                .Text(Formato(valor)).FontSize(7);
                        }
                    }
                });

                contenido.Item().PaddingTop(16).Column(firmas =>
                {
                    firmas.Item().Text("Firmas").FontSize(10).Bold().FontColor(RojoCorporativo);
                    var bloque = resultado.Firmas.OrderBy(f => f.Orden).ToList();
                    if (bloque.Count == 0)
                    {
                        firmas.Item().PaddingTop(4).Text("No hay firmas configuradas para el documento 0003.")
                            .FontSize(8).FontColor("#888888");
                        return;
                    }

                    firmas.Item().PaddingTop(6).Row(fila =>
                    {
                        foreach (var firma in bloque)
                        {
                            fila.RelativeItem().PaddingRight(12).Column(celda =>
                            {
                                celda.Item().Text(firma.Nombre).FontSize(9).Bold();
                                celda.Item().Text(firma.Cargo).FontSize(8).FontColor("#666666");
                                celda.Item().PaddingTop(14).Height(1).Background("#999999");
                                celda.Item().PaddingTop(2).Text("Firma:").FontSize(7).FontColor("#999999");
                            });
                        }
                    });
                });
            });

            pagina.Footer().Row(pie =>
            {
                pie.RelativeItem().Text(
                        "ORKIN · Generado desde los SPs legacy de SINCOERPORKIN · " +
                        "los códigos se muestran con su descripción del ERP")
                    .FontSize(7).FontColor("#999999");
                pie.RelativeItem().AlignRight().Text($"Reporte: {resultado.Definicion.Clave}")
                    .FontSize(7).FontColor("#999999");
            });
        });
    }).GeneratePdf();

    private static string Formato(object? valor) => valor switch
    {
        null => "—",
        DateTime fecha => fecha.ToString("dd/MM/yyyy", Cultura),
        DateOnly fecha => fecha.ToString("dd/MM/yyyy", Cultura),
        decimal numero => numero.ToString("N2", Cultura),
        double numero => numero.ToString("N2", Cultura),
        float numero => numero.ToString("N2", Cultura),
        bool booleano => booleano ? "SI" : "NO",
        _ => valor.ToString()?.Trim() switch
        {
            { Length: 0 } => "—",
            var texto => texto
        }
    };
}
