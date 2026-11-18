using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Reportes;

/// <summary>Parámetro de un reporte: la clave pública y el parámetro real del SP legacy.</summary>
/// <param name="Clave">Clave que usa la UI y la API (query string).</param>
/// <param name="Etiqueta">Texto que ve el usuario.</param>
/// <param name="Tipo"><c>fecha</c>, <c>entero</c>, <c>booleano</c> o <c>texto</c>.</param>
/// <param name="SqlParametro">Nombre del parámetro del SP, con <c>@</c>.</param>
/// <param name="Requerido">Si falta, la petición se rechaza antes de tocar la base.</param>
/// <param name="ValorPorDefecto">Valor opcional cuando el parámetro no es requerido.</param>
public sealed record ReporteParametro(
    string Clave,
    string Etiqueta,
    string Tipo,
    string SqlParametro,
    bool Requerido = true,
    string? ValorPorDefecto = null);

/// <summary>Columna que se muestra del dataset del SP.</summary>
public sealed record ReporteColumna(string Clave, string Titulo);

/// <summary>
/// Catálogo único de reportes (D-21): una definición por SP, con sus parámetros y columnas.
/// El nombre del procedimiento es una constante de este catálogo; nunca viene del cliente.
/// </summary>
public sealed record ReporteDefinicion(
    string Clave,
    string Nombre,
    string Descripcion,
    string Procedimiento,
    IReadOnlyList<ReporteParametro> Parametros,
    IReadOnlyList<ReporteColumna> Columnas,
    bool Landscape = false);

/// <summary>Firma del bloque <c>CODIGO = '0003'</c> de <c>vvTipodeDocumentosFirma</c>.</summary>
public sealed record FirmaReporte(string Nombre, string Cargo, int Orden);

/// <summary>Resultado listo para el visor, el PDF y el CSV.</summary>
public sealed record ReporteResultado(
    ReporteDefinicion Definicion,
    IReadOnlyList<Dictionary<string, object?>> Filas,
    IReadOnlyList<FirmaReporte> Firmas,
    string Subtitulo);

/// <summary>
/// Servicio de reportes de la Fase 7. Invoca los SPs <c>SINCO_IMPRIMIR_*</c> y
/// <c>SINCO_CONSULTAR_*</c> con parámetros, sin reimplementarlos ni concatenar datos del usuario
/// en SQL (D-22). Los mensajes <c>RAISERROR</c> se reenvían sin traducir.
/// </summary>
public interface IReporteService
{
    IReadOnlyList<ReporteDefinicion> Catalogo { get; }

    ReporteDefinicion? Buscar(string clave);

    Task<ReporteResultado> ObtenerAsync(
        string clave,
        IReadOnlyDictionary<string, object?> parametros,
        int? limite = null,
        CancellationToken cancellationToken = default);

    Task<List<FirmaReporte>> ObtenerFirmasAsync(CancellationToken cancellationToken = default);

    byte[] GenerarCsv(ReporteResultado resultado);
}

/// <inheritdoc />
public sealed class ServicioReportes : IReporteService
{
    private const string FirmaCodigo = "0003";
    private const string FirmaActivo = "SI";

    private static readonly ReporteParametro DesdeHasta =
        new("desde", "Desde", "fecha", "@Fecha_Inicial");
    private static readonly ReporteParametro Hasta =
        new("hasta", "Hasta", "fecha", "@Fecha_Final");

    private static readonly ReporteColumna[] ColumnasOrden =
    [
        new("ORDEN_NUMERO", "Orden"),
        new("FECHA_ORDEN", "Fecha"),
        new("HORA", "Hora"),
        new("NOMBRE_CLIENTE", "Cliente"),
        new("LOCACION_NOMBRE", "Locación"),
        new("NOMBRE_SERVICIO", "Servicio"),
        new("DESCRIP_ARTICULO", "Artículo"),
        new("CANTIDAD", "Cantidad"),
        new("COSTO_UNITARIO", "Costo unitario"),
        new("COSTO_TOTAL", "Costo total"),
        new("ESTADO", "Estado")
    ];

    private static readonly ReporteColumna[] ColumnasRequisa =
    [
        new("REQUISA_NUMERO", "Requisa"),
        new("FECHA_REQUISA", "Fecha"),
        new("ORDEN_NUMERO", "Orden"),
        new("NOMBRE_CLIENTE", "Cliente"),
        new("LOCACION_NOMBRE", "Locación"),
        new("CODIGO_ARTICULO", "Bodega · Artículo"),
        new("DESCRIP_ARTICULO", "Descripción"),
        new("CANTIDAD_ENTREGADA", "Entregada"),
        new("CANTIDAD_UTILIZADA", "Utilizada"),
        new("CANTIDAD_DEVUELTA", "Devuelta"),
        new("UMEDIDA", "Unidad")
    ];

    private static readonly ReporteColumna[] ColumnasValidacion =
    [
        new("FECHA_REQUISA", "Fecha"),
        new("REQUISA_NUMERO", "Requisa"),
        new("DOCUMENTO_INV", "Documento"),
        new("ESTADO", "Estado"),
        new("CODIGO_ARTICULO", "Artículo"),
        new("DESCRIP_ARTICULO", "Descripción"),
        new("CANTIDAD_ENTREGADA", "Entregada"),
        new("CANTIDAD_UTILIZADA", "Utilizada"),
        new("CANTIDAD_DEVUELTA", "Devuelta"),
        new("CANTIDAD_SOBFAL", "SobFal"),
        new("COSTO_UNITARIO", "Costo unitario"),
        new("COSTO_TOTAL", "Costo total"),
        new("CENTRO_COSTO", "Centro de costo"),
        new("CUENTA_CONTABLE", "Cuenta contable")
    ];

    private static readonly ReporteColumna[] ColumnasArticulo =
    [
        new("CODIGO_ARTICULO", "Artículo"),
        new("DESCRIP_ARTICULO", "Descripción"),
        new("CANTIDAD_ENTREGADA", "Entregada"),
        new("CANTIDAD_DEVUELTA", "Devuelta"),
        new("CANTIDAD_UTILIZADA", "Utilizada"),
        new("CANTIDAD_SOBFAL", "SobFal"),
        new("COSTO_TOTAL", "Costo total")
    ];

    private static readonly ReporteColumna[] ColumnasTransaccion =
    [
        new("ORDEN_NUMERO", "Orden"),
        new("REQUISA_NUMERO", "Requisa"),
        new("FECHA_ORDEN", "Fecha"),
        new("NOMBRE_CLIENTE", "Cliente"),
        new("LOCACION_NOMBRE", "Locación"),
        new("CODIGO_ARTICULO", "Artículo"),
        new("DESCRIP_ARTICULO", "Descripción"),
        new("CANTIDAD_ENTREGADA", "Entregada"),
        new("CANTIDAD_DEVUELTA", "Devuelta"),
        new("CANTIDAD_UTILIZADA", "Utilizada"),
        new("CANTIDAD_SOBFAL", "SobFal"),
        new("COSTO_UNITARIO", "Costo unitario"),
        new("COSTO_TOTAL", "Costo total"),
        new("CENTRO_COSTO", "Centro de costo"),
        new("CUENTA_CONTABLE", "Cuenta contable")
    ];

    private static readonly ReporteColumna[] ColumnasCostoAnual =
    [
        new("CODIGO_ARTICULO", "Artículo"),
        new("DESCRIP_ARTICULO", "Descripción"),
        new("ENE", "Ene"), new("FEB", "Feb"), new("MAR", "Mar"), new("ABR", "Abr"),
        new("MAY", "May"), new("JUN", "Jun"), new("JUL", "Jul"), new("AGO", "Ago"),
        new("SEP", "Sep"), new("OCT", "Oct"), new("NOV", "Nov"), new("DIC", "Dic"),
        new("COSTO_ANUAL", "Costo anual")
    ];

    private static readonly ReporteColumna[] ColumnasCostoCliente =
    [
        new("NOMBRE_CLIENTE", "Cliente"),
        new("LOCACION_NOMBRE", "Locación"),
        new("CODIGO_ARTICULO", "Artículo"),
        new("DESCRIP_ARTICULO", "Descripción"),
        new("CANTIDAD_TRIM1", "Cant. T1"), new("CANTIDAD_TRIM2", "Cant. T2"),
        new("CANTIDAD_TRIM3", "Cant. T3"), new("CANTIDAD_TRIM4", "Cant. T4"),
        new("MONTO_TRIM1", "Monto T1"), new("MONTO_TRIM2", "Monto T2"),
        new("MONTO_TRIM3", "Monto T3"), new("MONTO_TRIM4", "Monto T4"),
        new("COSTO_ANUAL", "Costo anual")
    ];

    private static readonly ReporteColumna[] ColumnasCostoSemanal =
    [
        new("NOMBRE_CLIENTE", "Cliente"),
        new("LOCACION_NOMBRE", "Locación"),
        new("DESCRIPCION", "Clasificación"),
        new("CODIGO_ARTICULO", "Artículo"),
        new("DESCRIP_ARTICULO", "Descripción"),
        new("SEMANA1_CANTIDAD", "Sem. 1 cant."), new("SEMANA2_CANTIDAD", "Sem. 2 cant."),
        new("SEMANA3_CANTIDAD", "Sem. 3 cant."), new("SEMANA4_CANTIDAD", "Sem. 4 cant."),
        new("TOTAL_CANTIDAD", "Total cant."),
        new("SEMANA1_MONTO", "Sem. 1 monto"), new("SEMANA2_MONTO", "Sem. 2 monto"),
        new("SEMANA3_MONTO", "Sem. 3 monto"), new("SEMANA4_MONTO", "Sem. 4 monto"),
        new("TOTAL_MONTO", "Total monto")
    ];

    private static readonly ReporteColumna[] ColumnasCartera =
    [
        new("CATEGORIA", "Categoría"),
        new("VENDEDOR_NOMBRE", "Vendedor"),
        new("CLIENTE_NOMBRE", "Cliente"),
        new("SALDO_ANTERIOR", "Saldo anterior"),
        new("FACTURAS", "Facturas"),
        new("NOTAS_DEBITO", "Notas débito"),
        new("RECIBOS", "Recibos"),
        new("NOTAS_CREDITO", "Notas crédito"),
        new("SALDO_ACTUAL", "Saldo actual"),
        new("A_5_DIAS", "A 5 días"),
        new("A_15_DIAS", "A 15 días"),
        new("A_30_DIAS", "A 30 días"),
        new("A_60_DIAS", "A 60 días"),
        new("MAS_60_DIAS", "Más de 60 días"),
        new("SALDO_CORRIENTE", "Saldo corriente")
    ];

    private static readonly ReporteDefinicion[] Definiciones =
    [
        new("orden", "Orden de trabajo",
            "Imprime una orden aplicada (AP). El propio SP rechaza las que todavía no están aplicadas.",
            "dbo.SINCO_IMPRIMIR_ORDENE_DE_TRABAJO",
            [new ReporteParametro("numero", "Número de orden", "texto", "@NUMERO_ORDEN")],
            ColumnasOrden),
        new("ordenes", "Órdenes generadas por fecha",
            "Órdenes en estado GE del rango; dataset del SP legacy.",
            "dbo.SINCO_IMPRIMIR_ORDENES_DE_TRABAJO",
            [DesdeHasta, Hasta], ColumnasOrden, true),
        new("requisas", "Requisas por fecha",
            "Requisas en estado GE del rango, con artículos y cantidades del SP legacy.",
            "dbo.SINCO_IMPRIMIR_REQUISAS",
            [DesdeHasta, Hasta], ColumnasRequisa, true),
        new("requisas-validacion", "Requisas para validar",
            "Entregado / utilizado / devuelto / SobFal y su documento de inventario.",
            "dbo.SINCO_IMPRIMIR_REQUISAS_PARAVALIDAR",
            [DesdeHasta, Hasta], ColumnasValidacion, true),
        new("requisas-tecnicos", "Técnicos por requisa",
            "Técnicos asignados a las requisas GE del rango.",
            "dbo.SINCO_IMPRIMIR_REQUISAS_TECNICOS",
            [DesdeHasta, Hasta],
            [
                new ReporteColumna("REQUISA_NUMERO", "Requisa"),
                new ReporteColumna("INICIALES", "Iniciales"),
                new ReporteColumna("NOMBRE_TECNICO", "Técnico")
            ]),
        new("transacciones", "Transacciones aplicadas",
            "Órdenes y requisas AP con sus líneas y costos (dataset legacy).",
            "dbo.SINCO_CONSULTAR_TRANSACCIONES",
            [DesdeHasta, Hasta], ColumnasTransaccion, true),
        new("detalle-transacciones", "Detalle de transacciones",
            "Igual que Transacciones pero solo cantidades utilizadas mayores que cero.",
            "dbo.SINCO_CONSULTAR_DETALLETRANSACCIONES",
            [DesdeHasta, Hasta], ColumnasTransaccion, true),
        new("resumen-articulos", "Resumen por artículos",
            "Cantidades y costos agrupados por artículo en el rango.",
            "dbo.SINCO_CONSULTAR_RESUMENPORARTICULOS",
            [DesdeHasta, Hasta], ColumnasArticulo),
        new("resumen-transacciones", "Resumen de transacciones",
            "Cantidades y costos agrupados por cliente, locación y artículo.",
            "dbo.SINCO_CONSULTAR_RESUMENTRANSACCIONES",
            [DesdeHasta, Hasta],
            [
                new ReporteColumna("NOMBRE_CLIENTE", "Cliente"),
                new ReporteColumna("LOCACION_NOMBRE", "Locación"),
                new ReporteColumna("CODIGO_ARTICULO", "Artículo"),
                new ReporteColumna("DESCRIP_ARTICULO", "Descripción"),
                new ReporteColumna("CANTIDAD_ENTREGADA", "Entregada"),
                new ReporteColumna("CANTIDAD_DEVUELTA", "Devuelta"),
                new ReporteColumna("CANTIDAD_UTILIZADA", "Utilizada"),
                new ReporteColumna("CANTIDAD_SOBFAL", "SobFal"),
                new ReporteColumna("COSTO_TOTAL", "Costo total")
            ], true),
        new("costos-anuales", "Costos anuales por artículo",
            "Costo mensual por artículo. El SP devuelve ENE…DIC y el total anual.",
            "dbo.SINCO_CONSULTAR_RESUMENANUALCOSTOS",
            [new ReporteParametro("anio", "Año", "entero", "@Anyo")],
            ColumnasCostoAnual, true),
        new("costos-anuales-cliente", "Costos anuales por cliente",
            "Cantidades y montos por trimestre, cliente y artículo.",
            "dbo.SINCO_CONSULTAR_RESUMENCOSTOS_ANUALPORCLIENTE",
            [new ReporteParametro("anio", "Año", "entero", "@Anyo")],
            ColumnasCostoCliente, true),
        new("costos-semanales", "Costos semanales",
            "Quirk heredado: el SP ignora los parámetros y siempre usa el año y mes actuales.",
            "dbo.SINCO_CONSULTAR_RESUMENCOSTOS_SEMANAL",
            [
                new ReporteParametro("anio", "Año", "entero", "@Anyo", false),
                new ReporteParametro("mes", "Mes", "entero", "@Mes", false)
            ], ColumnasCostoSemanal, true),
        new("cartera", "Cartera por antigüedad",
            "Saldos por cliente y antigüedad sobre FUMIPLAGA.FMP.",
            "dbo.SINCO_SP_CARTERA_ANTIGUEDAD",
            [
                new ReporteParametro("fechaCorte", "Fecha de corte", "fecha", "@FECHA_CORTE"),
                new ReporteParametro("vencida", "Solo vencida", "booleano", "@VENCIDA", false, "false")
            ], ColumnasCartera, true)
    ];

    private readonly ILectorSql _sql;
    private readonly ILogger<ServicioReportes> _logger;
    private readonly IContextoUsuario _usuario;

    public ServicioReportes(
        ILectorSql sql,
        ILogger<ServicioReportes> logger,
        IContextoUsuario usuario)
    {
        _sql = sql;
        _logger = logger;
        _usuario = usuario;
    }

    /// <inheritdoc />
    public IReadOnlyList<ReporteDefinicion> Catalogo => Definiciones;

    /// <inheritdoc />
    public ReporteDefinicion? Buscar(string clave) => Definiciones
        .FirstOrDefault(d => string.Equals(d.Clave, clave, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public async Task<ReporteResultado> ObtenerAsync(
        string clave,
        IReadOnlyDictionary<string, object?> parametros,
        int? limite = null,
        CancellationToken cancellationToken = default)
    {
        var definicion = Buscar(clave)
            ?? throw new KeyNotFoundException($"No existe el reporte '{clave}'.");

        var (sql, argumentos, subtitulo) = Preparar(definicion, parametros);

        try
        {
            var filas = await _sql.ConsultarAsync(sql, cancellationToken, argumentos.ToArray());
            if (limite.HasValue && filas.Count > limite.Value)
            {
                filas = filas.Take(limite.Value).ToList();
            }

            var firmas = await ObtenerFirmasAsync(cancellationToken);
            _logger.LogInformation(
                "Reporte {Reporte} ejecutado por {Usuario}: {Parametros}. Filas: {Filas}.",
                definicion.Clave,
                _usuario.NombreUsuario ?? "sistema",
                subtitulo,
                filas.Count);

            return new ReporteResultado(definicion, filas, firmas, subtitulo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Igual que en el resto de los envoltorios legacy: el RAISERROR del SP es contrato
            // observable del WinForms. Se registra y se reenvía sin traducirlo.
            _logger.LogError(
                ex,
                "Reporte {Reporte} falló para {Usuario}. {Parametros}",
                definicion.Clave,
                _usuario.NombreUsuario ?? "sistema",
                subtitulo);
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<List<FirmaReporte>> ObtenerFirmasAsync(CancellationToken cancellationToken = default)
    {
        var filas = await _sql.ConsultarAsync(
            "SELECT DESCRIPCION, FIRMAS, ORDEN FROM dbo.vvTipodeDocumentosFirma " +
            "WHERE CODIGO = @Codigo AND ACTIVO = @Activo ORDER BY ORDEN",
            cancellationToken,
            ("@Codigo", FirmaCodigo),
            ("@Activo", FirmaActivo));

        return filas.Select(f => new FirmaReporte(
            Texto(f, "DESCRIPCION"),
            Texto(f, "FIRMAS"),
            int.TryParse(Texto(f, "ORDEN"), out var orden) ? orden : 0))
            .ToList();
    }

    /// <inheritdoc />
    public byte[] GenerarCsv(ReporteResultado resultado)
    {
        var texto = new StringBuilder();
        texto.Append('\uFEFF'); // Excel en español abre el CSV con acentos si lleva BOM.
        texto.AppendLine(string.Join(';', resultado.Definicion.Columnas.Select(c => Celda(c.Titulo))));

        foreach (var fila in resultado.Filas)
        {
            texto.AppendLine(string.Join(';', resultado.Definicion.Columnas
                .Select(c => Celda(Formato(fila.TryGetValue(c.Clave, out var valor) ? valor : null)))));
        }

        return Encoding.UTF8.GetBytes(texto.ToString());
    }

    private static (string Sql, List<(string, object?)> Argumentos, string Subtitulo) Preparar(
        ReporteDefinicion definicion,
        IReadOnlyDictionary<string, object?> parametros)
    {
        var argumentos = new List<(string, object?)>();
        var detalles = new List<string>();

        foreach (var parametro in definicion.Parametros)
        {
            object? valor = null;
            if (parametros.TryGetValue(parametro.Clave, out var entrante))
            {
                valor = entrante;
            }
            else if (parametro.ValorPorDefecto is not null)
            {
                valor = parametro.ValorPorDefecto;
            }

            if (valor is null || string.IsNullOrWhiteSpace(valor.ToString()))
            {
                if (parametro.Requerido)
                {
                    throw new ArgumentException($"Falta el parámetro {parametro.Etiqueta}.");
                }

                continue;
            }

            object convertido = parametro.Tipo switch
            {
                "fecha" => ConvertirFecha(valor, parametro.Etiqueta),
                "entero" => ConvertirEntero(valor, parametro.Etiqueta),
                "booleano" => ConvertirBooleano(valor, parametro.Etiqueta),
                _ => valor.ToString()!.Trim()
            };

            argumentos.Add((parametro.SqlParametro, convertido));
            detalles.Add($"{parametro.Etiqueta}: {Formato(convertido)}");
        }

        // Nombre del SP y nombres de parámetro salen del catálogo estático; los valores viajan
        // como parámetros de SqlClient. Ningún dato del usuario se concatena en la sentencia.
        var sql = argumentos.Count == 0
            ? $"EXEC {definicion.Procedimiento}"
            : $"EXEC {definicion.Procedimiento} {string.Join(", ", argumentos.Select(a => a.Item1))}";

        return (sql, argumentos, string.Join(" · ", detalles));
    }

    private static DateTime ConvertirFecha(object valor, string etiqueta)
    {
        if (valor is DateTime fecha)
        {
            return fecha.Date;
        }

        if (valor is DateOnly dateOnly)
        {
            return dateOnly.ToDateTime(TimeOnly.MinValue);
        }

        var texto = valor.ToString()!.Trim();
        var culturas = new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("es-ES") };
        foreach (var cultura in culturas)
        {
            if (DateTime.TryParse(texto, cultura, DateTimeStyles.None, out var resultado))
            {
                return resultado.Date;
            }
        }

        throw new ArgumentException($"{etiqueta} no es una fecha válida.");
    }

    private static int ConvertirEntero(object valor, string etiqueta)
    {
        if (valor is int entero)
        {
            return entero;
        }

        return int.TryParse(valor.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var resultado)
            ? resultado
            : throw new ArgumentException($"{etiqueta} debe ser un número entero.");
    }

    private static bool ConvertirBooleano(object valor, string etiqueta) => valor switch
    {
        bool booleano => booleano,
        "1" or "true" or "TRUE" or "si" or "SI" or "sí" or "Sí" => true,
        "0" or "false" or "FALSE" or "no" or "NO" => false,
        _ => throw new ArgumentException($"{etiqueta} debe ser SI o NO.")
    };

    private static string Formato(object? valor) => valor switch
    {
        null => "—",
        DateTime fecha => fecha.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("es-ES")),
        DateOnly fecha => fecha.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("es-ES")),
        decimal numero => numero.ToString("0.######", CultureInfo.InvariantCulture),
        double numero => numero.ToString("0.######", CultureInfo.InvariantCulture),
        float numero => numero.ToString("0.######", CultureInfo.InvariantCulture),
        bool booleano => booleano ? "SI" : "NO",
        _ => valor.ToString()?.Trim() ?? "—"
    };

    private static string Texto(Dictionary<string, object?> fila, string clave) =>
        fila.TryGetValue(clave, out var valor) ? valor?.ToString()?.Trim() ?? string.Empty : string.Empty;

    private static string Celda(string valor)
    {
        var necesitaComillas = valor.Contains(';') || valor.Contains('"') ||
                               valor.Contains('\r') || valor.Contains('\n');
        var escapado = valor.Replace("\"", "\"\"");
        return necesitaComillas ? $"\"{escapado}\"" : escapado;
    }
}
