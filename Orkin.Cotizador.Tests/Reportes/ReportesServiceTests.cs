using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using Orkin.Cotizador.Core.Application.Reportes;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Reportes;

/// <summary>
/// Fase 7. El servicio de reportes es un envoltorio de SPs legacy (D-22): estas pruebas verifican
/// que el nombre del SP y de sus parámetros salgan del catálogo estático, que los valores viajen
/// como parámetros SQL (nunca concatenados), y que el <c>RAISERROR</c> del WinForms se reenvíe sin
/// traducir. No se ejecuta ningún SP real ni se toca SINCOERPORKIN.
/// </summary>
public sealed class ReportesServiceTests
{
    private sealed class UsuarioPrueba : IContextoUsuario
    {
        public Guid? UsuarioId => Guid.NewGuid();
        public string? NombreUsuario => "AZAPATA";
        public string? Ip => null;
        public string? Agente => null;
        public string? TraceId => null;
    }

    /// <summary>Doble de <see cref="ILectorSql"/> que registra cada sentencia y sus argumentos.</summary>
    private sealed class LectorPrueba : ILectorSql
    {
        public List<string> Sentencias { get; } = [];
        public List<(string Nombre, object? Valor)> Argumentos { get; } = [];
        public List<Dictionary<string, object?>> Filas { get; } = [];
        public List<Dictionary<string, object?>> Firmas { get; } =
        [
            new() { ["DESCRIPCION"] = "RUDY RUIZ", ["FIRMAS"] = "ELABORADO POR", ["ORDEN"] = "1" },
            new() { ["DESCRIPCION"] = "KARLA VICTOR", ["FIRMAS"] = "ENTREGADOR POR", ["ORDEN"] = "2" },
            new() { ["DESCRIPCION"] = "RECIBIDO POR", ["FIRMAS"] = "FIRMA CLIENTE", ["ORDEN"] = "3" }
        ];
        public Exception? Fallo { get; set; }

        public Task<List<Dictionary<string, object?>>> ConsultarAsync(
            string sql,
            CancellationToken cancellationToken = default,
            params (string Nombre, object? Valor)[] parametros)
        {
            Sentencias.Add(sql);
            Argumentos.AddRange(parametros);
            if (Fallo is not null)
            {
                throw Fallo;
            }

            return Task.FromResult(sql.Contains("vvTipodeDocumentosFirma", StringComparison.Ordinal)
                ? Firmas
                : Filas);
        }
    }

    private static ServicioReportes Crear(LectorPrueba lector) =>
        new(lector, NullLogger<ServicioReportes>.Instance, new UsuarioPrueba());

    [Fact]
    public void Catalogo_tiene_un_reporte_por_sp_y_expone_sus_parametros()
    {
        var servicio = Crear(new LectorPrueba());

        Assert.Equal(13, servicio.Catalogo.Count);
        Assert.Equal(servicio.Catalogo.Select(r => r.Clave).Count(), servicio.Catalogo.Select(r => r.Clave).Distinct().Count());

        var orden = servicio.Buscar("ORDEN");
        Assert.NotNull(orden);
        Assert.Equal("dbo.SINCO_IMPRIMIR_ORDENE_DE_TRABAJO", orden.Procedimiento);
        Assert.Equal("@NUMERO_ORDEN", Assert.Single(orden.Parametros).SqlParametro);

        var cartera = servicio.Buscar("cartera");
        Assert.NotNull(cartera);
        Assert.Equal("dbo.SINCO_SP_CARTERA_ANTIGUEDAD", cartera.Procedimiento);
        Assert.Equal("@FECHA_CORTE", cartera.Parametros[0].SqlParametro);
        Assert.Equal("@VENCIDA", cartera.Parametros[1].SqlParametro);
        Assert.False(cartera.Parametros[1].Requerido);
    }

    [Fact]
    public async Task ObtenerAsync_concatena_solo_nombres_del_catalogo_y_pasa_valores_como_parametros()
    {
        var lector = new LectorPrueba();
        var servicio = Crear(lector);

        await servicio.ObtenerAsync(
            "orden", new Dictionary<string, object?> { ["numero"] = "CN-0025666" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, lector.Sentencias.Count);
        Assert.Equal("EXEC dbo.SINCO_IMPRIMIR_ORDENE_DE_TRABAJO @NUMERO_ORDEN", lector.Sentencias[0]);
        Assert.Contains(("@NUMERO_ORDEN", (object?)"CN-0025666"), lector.Argumentos);

        // El dato del usuario nunca aparece dentro de la sentencia.
        Assert.DoesNotContain("CN-0025666", lector.Sentencias[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ObtenerAsync_convierte_fechas_enteros_y_booleanos_al_tipo_del_sp()
    {
        var lector = new LectorPrueba();
        var servicio = Crear(lector);

        await servicio.ObtenerAsync("cartera", new Dictionary<string, object?>
        {
            ["fechaCorte"] = "2026-11-18",
            ["vencida"] = "true"
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("EXEC dbo.SINCO_SP_CARTERA_ANTIGUEDAD @FECHA_CORTE, @VENCIDA", lector.Sentencias[0]);
        Assert.Equal(new DateTime(2026, 11, 18), Assert.IsType<DateTime>(
            lector.Argumentos.First(a => a.Nombre == "@FECHA_CORTE").Valor));
        Assert.True(Assert.IsType<bool>(
            lector.Argumentos.First(a => a.Nombre == "@VENCIDA").Valor));
    }

    [Fact]
    public async Task ObtenerAsync_rechaza_un_parametro_faltante_sin_tocar_la_base()
    {
        var lector = new LectorPrueba();
        var servicio = Crear(lector);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.ObtenerAsync(
                "orden", new Dictionary<string, object?>(),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Número de orden", ex.Message, StringComparison.Ordinal);
        Assert.Empty(lector.Sentencias);
    }

    [Fact]
    public async Task ObtenerAsync_propaga_el_RAISERROR_del_sp_sin_traducirlo()
    {
        const string mensajeLegacy =
            "Para poder imprimir una orden primero debe de estar aplicada, por favor apliquela para imprimirla.";
        var lector = new LectorPrueba
        {
            Fallo = new InvalidOperationException(mensajeLegacy)
        };
        var servicio = Crear(lector);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.ObtenerAsync(
                "orden", new Dictionary<string, object?> { ["numero"] = "CN-0000001" },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(mensajeLegacy, ex.Message);
    }

    [Fact]
    public async Task ObtenerFirmasAsync_lee_solo_el_codigo_0003_activo_y_respeta_el_orden()
    {
        var lector = new LectorPrueba();
        var servicio = Crear(lector);

        var firmas = await servicio.ObtenerFirmasAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, firmas.Count);
        Assert.Equal("RUDY RUIZ", firmas[0].Nombre);
        Assert.Equal("ELABORADO POR", firmas[0].Cargo);
        Assert.Equal(1, firmas[0].Orden);
        Assert.Equal("RECIBIDO POR", firmas[2].Nombre);
        Assert.Contains("CODIGO = @Codigo", lector.Sentencias[0], StringComparison.Ordinal);
        Assert.Contains("ACTIVO = @Activo", lector.Sentencias[0], StringComparison.Ordinal);
        Assert.Contains(("@Codigo", (object?)"0003"), lector.Argumentos);
        Assert.Contains(("@Activo", (object?)"SI"), lector.Argumentos);
    }

    [Fact]
    public void GenerarCsv_usa_las_columnas_del_sp_y_escapa_los_valores()
    {
        var definicion = Crear(new LectorPrueba()).Buscar("orden")!;
        var filas = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["ORDEN_NUMERO"] = "CN-0025666",
                ["NOMBRE_CLIENTE"] = "CLIENTE; S.A.",
                ["ESTADO"] = "AP"
            }
        };
        var resultado = new ReporteResultado(definicion, filas, [], "Orden: CN-0025666");

        var csv = Encoding.UTF8.GetString(Crear(new LectorPrueba()).GenerarCsv(resultado));
        var lineas = csv.Split(Environment.NewLine);

        Assert.StartsWith("\uFEFF", csv, StringComparison.Ordinal);
        Assert.Contains("Orden;Fecha;Hora;Cliente", lineas[0], StringComparison.Ordinal);
        Assert.Contains("\"CLIENTE; S.A.\"", lineas[1], StringComparison.Ordinal);
        Assert.Contains("CN-0025666", lineas[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ObtenerAsync_acota_el_resultado_al_limite_del_visor()
    {
        var lector = new LectorPrueba();
        lector.Filas.AddRange(Enumerable.Range(1, 5).Select(i => new Dictionary<string, object?>
        {
            ["ORDEN_NUMERO"] = $"CN-{i:0000000}"
        }));
        var servicio = Crear(lector);

        var resultado = await servicio.ObtenerAsync(
            "orden", new Dictionary<string, object?> { ["numero"] = "CN-0000001" }, limite: 2,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, resultado.Filas.Count);
    }
}
