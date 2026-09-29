using System.Data;

using ClosedXML.Excel;

using Orkin.Cotizador.Core.Domain.Carga;

namespace Orkin.Cotizador.Core.Infrastructure.Carga;

/// <summary>
/// Lee filas de un archivo Excel (`.xlsx` / `.xlsm`). Cada hoja se lee como una tabla: una fila
/// son los nombres de columna, y las demás son datos. Devuelve diccionarios clave-valor para
/// que el proceso de carga no sepa que el origen fue Excel.
/// <para><b>Lo que este lector no hace, y conviene saber antes de confiarle un archivo.</b> Es
/// un lector de <em>una tabla por hoja</em>, con dos supuestos que no siempre se cumplen:</para>
/// <list type="number">
/// <item>Los encabezados están en la <b>primera fila usada</b>. Si el archivo tiene un título
/// arriba, hay que decir cuál es la fila de encabezados (<c>filaEncabezado</c>): el lector
/// no lo adivina, porque adivinarlo es peor que fallar. Un título de una celda se lee como
/// encabezado de una columna, y todas las filas salen rechazadas por el mismo campo.</item>
/// <item>Los encabezados son <b>contiguos</b>. El escaneo se detiene en la primera celda vacía,
/// de modo que las columnas de la derecha de una fila con huecos no se leen.</item>
/// </list>
/// <para>El archivo histórico de ORKIN no cumple ninguno de los dos: tiene un título arriba y
/// <em>dos tablas lado a lado</em> (A–D con código, unidad, producto y precio; F–H con código,
/// descripción y costo), con el costo en la segunda. Ese archivo no lo carga este lector y no
/// lo va a cargar ningún lector de una tabla por hoja, porque el costo hay que cruzarlo con el
/// código de otra tabla. Para eso está el seeder de `scripts/demo-cliente/`, que sí sabe cómo
/// es ese archivo. Este lector sirve para los archivos de una sola tabla, que son la mayoría de
/// los que llegan.</para>
/// </summary>
public sealed class LectorExcel : ILectorOrigen
{
    private readonly Stream _archivo;
    private readonly string _hoja;
    private readonly int _filaEncabezado;

    /// <param name="archivo">El contenido del archivo .xlsx (no la ruta: el archivo viene de un upload).</param>
    /// <param name="hoja">Nombre de la hoja a leer. Si es null, se lee la primera hoja.</param>
    /// <param name="filaEncabezado">
    /// Número de fila (1-based) donde están los nombres de columna. Si es 0, se toma la primera
    /// fila usada de la hoja.
    /// </param>
    public LectorExcel(Stream archivo, string? hoja = null, int filaEncabezado = 0)
    {
        _archivo = archivo;
        _hoja = hoja ?? string.Empty;
        _filaEncabezado = filaEncabezado;
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

        // El encabezado se toma de la fila indicada, o de la primera fila usada si no se
        // indicó ninguna. Se valida contra el rango real de la hoja para que un número de fila
        // equivocado diga "la fila 900 no existe" y no "la hoja está vacía", que manda a
        // mirar el archivo equivocado.
        IXLRow primeraFila;

        if (_filaEncabezado > 0)
        {
            var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 0;

            if (_filaEncabezado > ultimaFila)
            {
                throw new InvalidOperationException(
                    $"Se indicó la fila {_filaEncabezado} como encabezado, pero la hoja '{hoja.Name}' " +
                    $"termina en la fila {ultimaFila}.");
            }

            primeraFila = hoja.Row(_filaEncabezado);
        }
        else
        {
            primeraFila = hoja.FirstRowUsed()
                ?? throw new InvalidOperationException("La hoja está vacía: no hay filas que leer.");
        }

        var columnas = new List<(int Indice, string Nombre)>();
        var celda = primeraFila.FirstCellUsed();

        // El escaneo se detiene en la primera celda vacía y no salta el hueco, a propósito: una
        // hoja con dos tablas lado a lado (columnas A-D y F-H, con E de separación) se leería
        // como una sola tabla de cuatro columnas, y la segunda se perdería en silencio. Parar
        // es lo que hace visible que hay algo más a la derecha.
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
