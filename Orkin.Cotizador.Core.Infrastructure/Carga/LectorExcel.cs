using System.Data;

using ClosedXML.Excel;

using Orkin.Cotizador.Core.Domain.Carga;

namespace Orkin.Cotizador.Core.Infrastructure.Carga;

/// <summary>
/// Lee filas de un archivo Excel (`.xlsx`). Cada hoja se lee como una tabla: la primera fila
/// son los nombres de columna, y las demás son datos. Devuelve diccionarios clave-valor para
/// que el proceso de carga no sepa que el origen fue Excel.
/// </summary>
public sealed class LectorExcel : ILectorOrigen
{
    private readonly Stream _archivo;
    private readonly string _hoja;

    /// <param name="archivo">El contenido del archivo .xlsx (no la ruta: el archivo viene de un upload).</param>
    /// <param name="hoja">Nombre de la hoja a leer. Si es null, se lee la primera hoja.</param>
    public LectorExcel(Stream archivo, string? hoja = null)
    {
        _archivo = archivo;
        _hoja = hoja ?? string.Empty;
    }

    public string TipoOrigen => "EXCEL";

    public Task<IReadOnlyList<Dictionary<string, object?>>> LeerAsync(CancellationToken cancellationToken = default)
    {
        using var libro = new XLWorkbook(_archivo);

        var hoja = string.IsNullOrWhiteSpace(_hoja)
            ? libro.Worksheets.First()
            : libro.Worksheets.FirstOrDefault(w =>
                string.Equals(w.Name, _hoja, StringComparison.OrdinalIgnoreCase))
              ?? throw new InvalidOperationException($"La hoja '{_hoja}' no existe en el archivo.");

        var primeraFila = hoja.FirstRowUsed()
            ?? throw new InvalidOperationException("La hoja está vacía: no hay filas que leer.");

        var columnas = new List<(int Indice, string Nombre)>();
        var celda = primeraFila.FirstCellUsed();

        while (celda is not null && !string.IsNullOrWhiteSpace(celda.GetString()))
        {
            columnas.Add((celda.Address.ColumnNumber, celda.GetString().Trim()));
            celda = celda.CellRight();
        }

        if (columnas.Count == 0)
        {
            throw new InvalidOperationException("La primera fila no tiene nombres de columna.");
        }

        var filas = new List<Dictionary<string, object?>>();
        var filaActual = primeraFila.RowBelow();

        while (filaActual is not null && !filaActual.IsEmpty())
        {
            var fila = new Dictionary<string, object?>(columnas.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var (indice, nombre) in columnas)
            {
                var valorCelda = filaActual.Cell(indice);

                object? valor = valorCelda.DataType switch
                {
                    XLDataType.Text => valorCelda.GetString(),
                    XLDataType.Number => valorCelda.GetValue<double>(),
                    XLDataType.DateTime => valorCelda.GetDateTime(),
                    XLDataType.Boolean => valorCelda.GetBoolean(),
                    XLDataType.TimeSpan => valorCelda.GetValue<TimeSpan>(),
                    _ => valorCelda.GetString()
                };

                fila[nombre] = valor is string s && string.IsNullOrWhiteSpace(s) ? null : valor;
            }

            if (fila.Values.Any(v => v is not null))
            {
                filas.Add(fila);
            }

            filaActual = filaActual.RowBelow();
        }

        return Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>(filas);
    }
}
