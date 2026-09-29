using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;

namespace Orkin.DemoCliente.CargarDatos;

/// <summary>
/// Carga los datos del cotizador Excel de ORKIN en la base ficticia del cliente
/// (<c>OrkinClienteDemo</c>) para simular el ETL de integración (Fase 12).
/// <para>La base del "ERP" NO tiene nuestro esquema: unidades como 'ml', 'H.H', 'lb';
/// maestros repartidos entre ARTICULO / UNIDAD / GRUPO. La vista de origen
/// (<c>vw_ProductosOrkin</c>) es la que normaliza.</para>
/// <para>Idempotente: vacía y vuelve a insertar cada tabla. Correr con:
/// <c>dotnet run --project tools/Orkin.DemoCliente.CargarDatos -- "ruta\Cotizador...xlsm"</c></para>
/// </summary>
public static class Program
{
    private const string Cadena = "Server=localhost,1433;Database=OrkinClienteDemo;User Id=sa;Password=OrkinDev_2026;TrustServerCertificate=True;Encrypt=False";

    /// <summary>Unidades que el cotizador Excel usa de verdad. Una fila de la tabla izquierda
    /// cuyo valor en B no esté aquí es una fila desalineada (duplicado de la tabla derecha).</summary>
    private static readonly HashSet<string> UnidadesValidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "ml", "und", "gr", "km", "h.h", "litro", "l", "lb", "bolsa", "rollo", "yarda", "par"
    };

    public static async Task<int> Main(string[] args)
    {
        var ruta = args.Length > 0 ? args[0] : null;
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            Console.Error.WriteLine($"El archivo Excel no existe: {ruta}");
            return 1;
        }

        using var libro = new XLWorkbook(ruta);
        Console.WriteLine("Leyendo hoja 'Tabla productos'...");
        var articulos = LeerArticulos(libro.Worksheet("Tabla productos"));

        Console.WriteLine("Leyendo hojas 'Tipo de cambio' y 'Tipos de servicios'...");
        var cambios = LeerTipoCambio(libro.Worksheet("Tipo de cambio"));
        var servicios = LeerTiposServicio(libro.Worksheet("Tipos de servicios"));

        // Elfamilias se derivan de los prefijos de código, como si el ERP las agrupara.
        var familias = articulos.Values
            .Select(a => FamiliaDe(a.Codigo))
            .DistinctBy(a => a.Codigo, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();

        Console.WriteLine($"  Artículos: {articulos.Count}");
        Console.WriteLine($"  Familias:  {familias.Count} ({string.Join(", ", familias.Select(f => f.Codigo))})");
        Console.WriteLine($"  Unidades:  {articulos.Values.Select(a => a.Unidad).Distinct().Count()}");
        Console.WriteLine($"  TC:        {cambios.Count} días");
        Console.WriteLine($"  Servicios: {servicios.Count}");

        await VaciarTablaAsync(conexion, "UNIDAD");
        await VaciarTablaAsync(conexion, "GRUPO");
        await VaciarTablaAsync(conexion, "ARTICULO");
        await VaciarTablaAsync(conexion, "TIPO_CAMBIO_HIST");
        await VaciarTablaAsync(conexion, "TIPO_SERVICIO");

        await CargarUnidadesAsync(conexion, articulos.Values);
        await CargarFamiliasAsync(conexion, familias);
        await CargarArticulosAsync(conexion, articulos.Values);
        await CargarTipoCambioAsync(conexion, cambios);
        await CargarServiciosAsync(conexion, servicios);

        Console.WriteLine("Carga completada.");
        return 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Lectura del Excel
    // ─────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, Articulo> LeerArticulos(IXLWorksheet hoja)
    {
        var izquierda = new List<(string Codigo, string Unidad, string Nombre, decimal Precio)>();
        var derecha = new Dictionary<string, (string Descripcion, decimal Costo, int? VidaUtil)>(StringComparer.OrdinalIgnoreCase);

        var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 0;

        for (var f = 3; f <= ultimaFila; f++)
        {
            // Tabla izquierda: A=Código, B=UNIDAD, C=PRODUCTO, D=PRECIO
            var codigo = Texto(hoja.Cell(f, 1).GetFormattedString());
            if (codigo.Length > 0)
            {
                var unidad = Texto(hoja.Cell(f, 2).GetFormattedString());
                var nombre = Texto(hoja.Cell(f, 3).GetFormattedString());
                var precio = ParsearNumero(hoja.Cell(f, 4).GetFormattedString());

                // Solo filas alineadas (la unidad es una unidad de verdad). Las demás son
                // duplicados desalineados de la tabla derecha (H-10 en la documentación).
                if (UnidadesValidas.Contains(unidad) && nombre.Length > 0 && codigo.Length > 0)
                {
                    izquierda.Add((codigo, unidad, nombre, precio ?? 0m));
                }
            }

            // Tabla derecha: F=Código, G=Descripción, H=Costo
            var codigoDerecha = Texto(hoja.Cell(f, 6).GetFormattedString());
            if (codigoDerecha.Length > 0)
            {
                var desc = Texto(hoja.Cell(f, 7).GetFormattedString());
                var costo = ParsearNumero(hoja.Cell(f, 8).GetFormattedString());
                derecha[codigoDerecha] = (desc.Length > 0 ? desc : codigoDerecha, costo ?? 0m, null);
            }
        }

        // Maestro: las filas izquierdas son la lista con unidad; el costo lo trae la tabla derecha.
        var resultado = new Dictionary<string, Articulo>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in izquierda)
        {
            var costo = derecha.TryGetValue(fila.Codigo, out var der)
                ? der.Costo
                : fila.Precio;

            resultado[fila.Codigo] = new Articulo(
                fila.Codigo,
                UnidadEstandar(fila.Unidad),
                fila.Nombre,
                costo,
                fila.Precio > 0 ? fila.Precio : costo,
                FamiliaDe(fila.Codigo).Codigo);
        }

        return resultado;
    }

    private static List<(DateOnly Fecha, decimal Valor)> LeerTipoCambio(IXLWorksheet hoja)
    {
        var lista = new List<(DateOnly, decimal)>();
        var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 0;

        for (var f = 2; f <= ultimaFila; f++)
        {
            var textoFecha = Texto(hoja.Cell(f, 3).GetFormattedString());
            var valor = ParsearNumero(hoja.Cell(f, 4).GetFormattedString());
            var fecha = ParsearFecha(textoFecha);

            if (fecha is not null && valor is not null)
            {
                lista.Add((fecha.Value, valor.Value));
            }
        }

        return lista;
    }

    private static List<string> LeerTiposServicio(IXLWorksheet hoja)
    {
        var lista = new List<string>();
        var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 0;

        for (var f = 3; f <= ultimaFila; f++)
        {
            var nombre = Texto(hoja.Cell(f, 2).GetFormattedString());
            if (nombre.Length > 0)
            {
                lista.Add(nombre);
            }
        }

        return lista;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Escritura en la BD ficticia
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task VaciarTablaAsync(SqlConnection conexion, string tabla)
    {
        await using var comando = new SqlCommand($"DELETE FROM dbo.{tabla}", conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task CargarUnidadesAsync(SqlConnection conexion, IEnumerable<Articulo> articulos)
    {
        var unidades = articulos
            .Select(a => a.Unidad)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var unidad in unidades)
        {
            await EjecutarAsync(conexion,
                "INSERT INTO dbo.UNIDAD (UNI_CODIGO, UNI_NOMBRE) VALUES (@cod, @nom)",
                ("@cod", unidad), ("@nom", DescripcionUnidad(unidad)));
        }

        Console.WriteLine($"  UNIDAD: {unidades.Count} insertadas");
    }

    private static async Task CargarFamiliasAsync(SqlConnection conexion, List<Familia> familias)
    {
        foreach (var familia in familias)
        {
            await EjecutarAsync(conexion,
                "INSERT INTO dbo.GRUPO (GRU_CODIGO, GRU_NOMBRE) VALUES (@cod, @nom)",
                ("@cod", familia.Codigo), ("@nom", familia.Nombre));
        }

        Console.WriteLine($"  GRUPO: {familias.Count} insertadas");
    }

    private static async Task CargarArticulosAsync(SqlConnection conexion, IEnumerable<Articulo> articulos)
    {
        var contador = 0;
        foreach (var articulo in articulos)
        {
            await EjecutarAsync(conexion,
                """
                INSERT INTO dbo.ARTICULO
                    (ART_CODIGO, ART_NOMBRE, ART_DESCRIPCION, ART_UNIDAD_CODIGO,
                     ART_GRUPO_CODIGO, ART_COSTO, ART_PRECIO_VENTA, ART_VIDA_UTIL_MESES, ART_ACTIVO)
                VALUES
                    (@cod, @nom, @desc, @unidad, @grupo, @costo, @precio, NULL, 1)
                """,
                ("@cod", articulo.Codigo),
                ("@nom", articulo.Nombre),
                ("@desc", articulo.Nombre),
                ("@unidad", articulo.Unidad),
                ("@grupo", articulo.Familia),
                ("@costo", articulo.Costo),
                ("@precio", articulo.PrecioVenta));
            contador++;
        }

        Console.WriteLine($"  ARTICULO: {contador} insertados");
    }

    private static async Task CargarTipoCambioAsync(SqlConnection conexion, List<(DateOnly Fecha, decimal Valor)> cambios)
    {
        foreach (var (fecha, valor) in cambios)
        {
            await EjecutarAsync(conexion,
                "INSERT INTO dbo.TIPO_CAMBIO_HIST (TCA_FECHA, TCA_CORDOBAS_USD) VALUES (@fecha, @valor)",
                ("@fecha", fecha.ToDateTime(TimeOnly.MinValue)), ("@valor", valor));
        }

        Console.WriteLine($"  TIPO_CAMBIO_HIST: {cambios.Count} insertados");
    }

    private static async Task CargarServiciosAsync(SqlConnection conexion, List<string> servicios)
    {
        for (var i = 0; i < servicios.Count; i++)
        {
            var codigo = $"SER{i + 1:D2}";
            await EjecutarAsync(conexion,
                "INSERT INTO dbo.TIPO_SERVICIO (SVC_CODIGO, SVC_NOMBRE) VALUES (@cod, @nom)",
                ("@cod", codigo), ("@nom", servicios[i]));
        }

        Console.WriteLine($"  TIPO_SERVICIO: {servicios.Count} insertados");
    }

    private static async Task EjecutarAsync(SqlConnection conexion, string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var comando = new SqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }
        await comando.ExecuteNonQueryAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Normalizaciones
    // ─────────────────────────────────────────────────────────────────────────

    private static string UnidadEstandar(string unidad) => unidad.Trim().ToUpperInvariant() switch
    {
        "ML"     => "ml",
        "UND"    => "und",
        "GR"     => "gr",
        "KM"     => "km",
        "H.H"    => "H.H",
        "HH"     => "H.H",
        "LITRO"  => "Litro",
        "L"      => "Litro",
        "LB"     => "lb",
        "BOLSA"  => "Bolsa",
        "ROLLO"  => "Rollo",
        "YARDA"  => "Yarda",
        "PAR"    => "Par",
        _        => "und"
    };

    private static string DescripcionUnidad(string unidad) => unidad switch
    {
        "ml"    => "Mililitro",
        "und"   => "Unidad",
        "gr"    => "Gramo",
        "km"    => "Kilómetro",
        "H.H"   => "Horas hombre",
        "Litro" => "Litro",
        "lb"    => "Libra",
        "Bolsa" => "Bolsa",
        "Rollo" => "Rollo",
        "Yarda" => "Yarda",
        "Par"   => "Par",
        _       => "Unidad"
    };

    private static Familia FamiliaDe(string codigo)
    {
        var prefijo = codigo.Trim().ToUpperInvariant().StartsWith("PQ", StringComparison.Ordinal) ? "PQ"
            : codigo.StartsWith("MT", StringComparison.OrdinalIgnoreCase) ? "MT"
            : codigo.StartsWith("EP", StringComparison.OrdinalIgnoreCase) ? "EP"
            : "OT";

        return prefijo switch
        {
            "PQ" => new Familia("QUIMICO", "Productos químicos"),
            "MT" => new Familia("MATERIAL", "Materiales y trampas"),
            "EP" => new Familia("EQUIPO_PROTECCION", "Equipo de protección personal"),
            _    => new Familia("GENERAL", "General")
        };
    }

    private static string Texto(string valor) => valor.Trim();

    private static decimal? ParsearNumero(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var limpio = texto
            .Replace("C$", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "")
            .Replace(" ", "")
            .Replace(",", "");

        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }

    private static readonly Dictionary<string, int> Meses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ene"] = 1, ["feb"] = 2, ["mar"] = 3, ["abr"] = 4, ["may"] = 5, ["jun"] = 6,
        ["jul"] = 7, ["ago"] = 8, ["sep"] = 9, ["oct"] = 10, ["nov"] = 11, ["dic"] = 12
    };

    private static DateOnly? ParsearFecha(string texto)
    {
        // Formato del Excel: "1-ene-2020"
        var partes = texto.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (partes.Length != 3 || !int.TryParse(partes[0], out var dia) ||
            !Meses.TryGetValue(partes[1], out var mes) || !int.TryParse(partes[2], out var anio))
        {
            return null;
        }

        try
        {
            return new DateOnly(anio, mes, dia);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private sealed record Articulo(
        string Codigo,
        string Unidad,
        string Nombre,
        decimal Costo,
        decimal PrecioVenta,
        string Familia);

    private sealed record Familia(string Codigo, string Nombre);
}