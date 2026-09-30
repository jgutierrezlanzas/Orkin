using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Carga;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Carga;

/// <summary>
/// Pruebas de los cargadores de catálogos maestros (unidades, categorías, tipos de servicio) y
/// del de tipos de cambio.
/// <para>Lo que se comprueba es la misma promesa de <see cref="SimulacionCargaTests"/> y
/// <see cref="CargadorClientesTests"/> —<b>simular no escribe</b> y <b>simular dice lo mismo que
/// la carga real</b>— sobre los cargadores que comparten el núcleo
/// <see cref="CargadorCatalogoMaestro{T}"/> y el que no (tipos de cambio, por fecha). Se prueba
/// el núcleo a través de los hijos: si el núcleo respeta la promesa para unidades, la respeta
/// para categorías y tipos de servicio, que solo aportan campos.</para>
/// </summary>
public sealed class CargadoresMaestrosTests
{
    private static readonly Guid ConexionErp = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"mae-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = Crear();

        public CotizadorDbContext Contexto { get; }

        public CotizadorDbContext Relectura() => Crear();

        private CotizadorDbContext Crear() =>
            new(new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public async ValueTask DisposeAsync() => await Contexto.DisposeAsync();
    }

    private static async Task<Laboratorio> CrearLaboratorioAsync()
    {
        var laboratorio = new Laboratorio();
        var contexto = laboratorio.Contexto;

        var idOrigen = 0;
        foreach (var (codigo, nombre) in new[] { ("ERP", "Sincronizado desde un ERP"), ("MANUAL", "Capturado en la web") })
        {
            var fila = new OrigenDatoCatalogo { Codigo = codigo, Nombre = nombre, EsActivo = true };
            AsignarIdCatalogo(fila, (byte)(++idOrigen));
            contexto.OrigenDatoCatalogo.Add(fila);
        }

        var idFuente = 0;
        foreach (var (codigo, nombre) in new[] { ("BANCO_CENTRAL", "Banco Central"), ("MANUAL", "Manual") })
        {
            var fila = new FuenteTipoCambioCatalogo { Codigo = codigo, Nombre = nombre, EsActivo = true };
            AsignarIdCatalogo(fila, (byte)(++idFuente));
            contexto.FuenteTipoCambioCatalogo.Add(fila);
        }

        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        return laboratorio;
    }

    private static void AsignarIdCatalogo<T>(T fila, byte id) where T : CatalogoDominioBase =>
        typeof(CatalogoDominioBase)
            .GetField($"<{nameof(CatalogoDominioBase.Id)}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fila, id);

    private static ConfiguracionCarga Config(string catalogo, bool esSimulacion) => new()
    {
        Catalogo = catalogo,
        TipoOrigen = "SQL_SERVER",
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codigo"] = "Codigo",
            ["Nombre"] = "Nombre",
            ["Abreviatura"] = "Abreviatura",
            ["PermiteDecimales"] = "PermiteDecimales",
            ["Descripcion"] = "Descripcion",
            ["UnidadMedidaCodigo"] = "UnidadMedidaCodigo",
            ["Fecha"] = "Fecha",
            ["Valor"] = "Valor"
        },
        ErpConexionId = ConexionErp,
        EsSimulacion = esSimulacion
    };

    // ── El núcleo de los maestros, probado a través de unidades ─────────────────

    [Fact]
    public async Task Unidades_Simulacion_NoEscribeNada()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var cargador = new CargadorUnidadesMedida(contexto.Contexto);

        var resultado = await cargador.CargarAsync(
            [
                new() { ["Codigo"] = "ml", ["Nombre"] = "Mililitro", ["Abreviatura"] = "ml", ["PermiteDecimales"] = "1" },
                new() { ["Codigo"] = "und", ["Nombre"] = "Unidad", ["PermiteDecimales"] = "0" }
            ],
            Config("UNIDADES_MEDIDA", esSimulacion: true));

        Assert.Equal(2, resultado.Insertados);
        Assert.Equal(0, await contexto.Contexto.UnidadMedida.CountAsync());
    }

    [Fact]
    public async Task Unidades_SimulacionYCarga_DicenLoMismo()
    {
        await using var simulacion = await CrearLaboratorioAsync();
        await using var real = await CrearLaboratorioAsync();

        var filas = new List<Dictionary<string, object?>>
        {
            new() { ["Codigo"] = "ml", ["Nombre"] = "Mililitro", ["Abreviatura"] = "ml", ["PermiteDecimales"] = "1" },
            new() { ["Codigo"] = "und", ["Nombre"] = "Unidad", ["PermiteDecimales"] = "0" }
        };

        var simulada = await new CargadorUnidadesMedida(simulacion.Contexto).CargarAsync(filas, Config("UNIDADES_MEDIDA", esSimulacion: true));
        var cargada = await new CargadorUnidadesMedida(real.Contexto).CargarAsync(filas, Config("UNIDADES_MEDIDA", esSimulacion: false));

        Assert.Equal(simulada.Insertados, cargada.Insertados);
        Assert.Equal(simulada.Actualizados, cargada.Actualizados);
        Assert.Equal(simulada.Rechazados, cargada.Rechazados);
    }

    [Fact]
    public async Task Unidades_SegundaVez_OmiteTodo()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var cargador = new CargadorUnidadesMedida(contexto.Contexto);
        var filas = new List<Dictionary<string, object?>>
        {
            new() { ["Codigo"] = "ml", ["Nombre"] = "Mililitro" }
        };

        var primera = await cargador.CargarAsync(filas, Config("UNIDADES_MEDIDA", esSimulacion: false));
        var segunda = await cargador.CargarAsync(filas, Config("UNIDADES_MEDIDA", esSimulacion: false));

        Assert.Equal(1, primera.Insertados);
        Assert.Equal(1, segunda.Omitidos);
    }

    [Fact]
    public async Task Unidades_AdoptaLaSembradaAManoConElMismoCodigo()
    {
        await using var contexto = await CrearLaboratorioAsync();
        contexto.Contexto.UnidadMedida.Add(new UnidadMedida
        {
            Id = Guid.NewGuid(),
            Codigo = "ml",
            Nombre = "Mililitro viejo",
            Abreviatura = "ml",
            PermiteDecimales = true,
            OrigenDatoId = 2,
            EsSembrado = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await contexto.Contexto.SaveChangesAsync();
        contexto.Contexto.ChangeTracker.Clear();

        var resultado = await new CargadorUnidadesMedida(contexto.Contexto).CargarAsync(
            [new() { ["Codigo"] = "ml", ["Nombre"] = "Mililitro" }],
            Config("UNIDADES_MEDIDA", esSimulacion: false));

        Assert.Equal(0, resultado.Insertados);
        Assert.Equal(1, resultado.Actualizados);

        using var verificacion = contexto.Relectura();
        var guardada = await verificacion.UnidadMedida.SingleAsync();
        Assert.Equal("Mililitro", guardada.Nombre);
        Assert.Equal(ConexionErp, guardada.ErpConexionId);
    }

    [Fact]
    public async Task Unidades_AbreviaturaPorDefectoEsElCodigo()
    {
        await using var contexto = await CrearLaboratorioAsync();

        // Sin Abreviatura en el origen: la abreviatura es el código, porque en los maestros de
        // unidades de un ERP el código suele ser a la vez lo que se imprime.
        await new CargadorUnidadesMedida(contexto.Contexto).CargarAsync(
            [new() { ["Codigo"] = "Litro", ["Nombre"] = "Litro" }],
            Config("UNIDADES_MEDIDA", esSimulacion: false));

        using var verificacion = contexto.Relectura();
        var guardada = await verificacion.UnidadMedida.SingleAsync();
        Assert.Equal("Litro", guardada.Abreviatura);
    }

    // ── Categorías: el núcleo ya probado; su campo propio ──────────────────────

    [Fact]
    public async Task Categorias_Simulacion_NoEscribeNada()
    {
        await using var contexto = await CrearLaboratorioAsync();

        var resultado = await new CargadorCategoriasProducto(contexto.Contexto).CargarAsync(
            [new() { ["Codigo"] = "RODENTICIDA", ["Nombre"] = "Rodenticida" }],
            Config("CATEGORIAS_PRODUCTO", esSimulacion: true));

        Assert.Equal(1, resultado.Insertados);
        Assert.Equal(0, await contexto.Contexto.CategoriaProducto.CountAsync());
    }

    [Fact]
    public async Task Categorias_CargaReal_GuardaLaDescripcion()
    {
        await using var contexto = await CrearLaboratorioAsync();

        await new CargadorCategoriasProducto(contexto.Contexto).CargarAsync(
            [new() { ["Codigo"] = "RODENTICIDA", ["Nombre"] = "Rodenticida", ["Descripcion"] = "Control de roedores" }],
            Config("CATEGORIAS_PRODUCTO", esSimulacion: false));

        using var verificacion = contexto.Relectura();
        var guardada = await verificacion.CategoriaProducto.SingleAsync();
        Assert.Equal("Control de roedores", guardada.Descripcion);
    }

    // ── Tipos de servicio: la FK a unidad es lo propio de este cargador ────────

    [Fact]
    public async Task TiposServicio_Simulacion_NoEscribeNada()
    {
        await using var contexto = await CrearLaboratorioAsync();

        // Sin UnidadMedidaCodigo: la unidad de cobro es opcional en la entidad a propósito, y
        // este caso prueba la promesa central (simular no escribe) sin depender de que las
        // unidades estén cargadas. El caso con FK está en las pruebas de más abajo.
        var resultado = await new CargadorTiposServicio(contexto.Contexto).CargarAsync(
            [new() { ["Codigo"] = "SAN", ["Nombre"] = "Sanitización" }],
            Config("TIPOS_SERVICIO", esSimulacion: true));

        Assert.Equal(1, resultado.Insertados);
        Assert.Equal(0, await contexto.Contexto.TipoServicio.CountAsync());
    }

    [Fact]
    public async Task TiposServicio_UnidadDeCobroDesconocida_RecachaLaFila()
    {
        await using var contexto = await CrearLaboratorioAsync();

        var resultado = await new CargadorTiposServicio(contexto.Contexto).CargarAsync(
            [new() { ["Codigo"] = "SAN", ["Nombre"] = "Sanitización", ["UnidadMedidaCodigo"] = "M2" }],
            Config("TIPOS_SERVICIO", esSimulacion: false));

        // Sin la unidad cargada, el servicio existe pero no se puede cotizar: mejor verlo
        // rechazado acá que descubrirlo en la primera cotización.
        Assert.Single(resultado.Rechazos);
        Assert.Contains("M2", resultado.Rechazos[0].Motivo);
    }

    [Fact]
    public async Task TiposServicio_ResuelveLaUnidadPorCodigo()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var unidadId = Guid.NewGuid();
        contexto.Contexto.UnidadMedida.Add(new UnidadMedida
        {
            Id = unidadId,
            Codigo = "und",
            Nombre = "Unidad",
            Abreviatura = "und",
            PermiteDecimales = false,
            OrigenDatoId = 2,
            EsSembrado = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await contexto.Contexto.SaveChangesAsync();
        contexto.Contexto.ChangeTracker.Clear();

        await new CargadorTiposServicio(contexto.Contexto).CargarAsync(
            [new() { ["Codigo"] = "SAN", ["Nombre"] = "Sanitización", ["UnidadMedidaCodigo"] = "und" }],
            Config("TIPOS_SERVICIO", esSimulacion: false));

        using var verificacion = contexto.Relectura();
        var guardado = await verificacion.TipoServicio.SingleAsync();
        Assert.Equal(unidadId, guardado.UnidadMedidaId);
    }

    // ── Tipos de cambio: el que no comparte núcleo, por fecha ───────────────────

    [Fact]
    public async Task TiposCambio_Simulacion_NoEscribeNada()
    {
        await using var contexto = await CrearLaboratorioAsync();

        var resultado = await new CargadorTiposCambio(contexto.Contexto).CargarAsync(
            [
                new() { ["Fecha"] = "2026-09-28", ["Valor"] = "36.80" },
                new() { ["Fecha"] = "2026-09-29", ["Valor"] = "36.80" }
            ],
            Config("TIPOS_CAMBIO", esSimulacion: true));

        Assert.Equal(2, resultado.Insertados);
        Assert.Equal(0, await contexto.Contexto.TipoCambio.CountAsync());
    }

    [Fact]
    public async Task TiposCambio_FechaDuplicadaEnElLote_Recacha()
    {
        await using var contexto = await CrearLaboratorioAsync();

        var resultado = await new CargadorTiposCambio(contexto.Contexto).CargarAsync(
            [
                new() { ["Fecha"] = "2026-09-28", ["Valor"] = "36.80" },
                new() { ["Fecha"] = "2026-09-28", ["Valor"] = "36.90" }
            ],
            Config("TIPOS_CAMBIO", esSimulacion: false));

        // Dos valores para el mismo día en el mismo origen son un dato corrupto del ERP:
        // rechazarlo es la única respuesta honesta, desempatarlo en silencio sería inventar.
        Assert.Equal(1, resultado.Insertados);
        Assert.Single(resultado.Rechazos);
        Assert.Contains("dos veces", resultado.Rechazos[0].Motivo);
    }

    [Fact]
    public async Task TiposCambio_AdoptaLaFilaManualDelMismoDiaYFuente()
    {
        await using var contexto = await CrearLaboratorioAsync();
        contexto.Contexto.TipoCambio.Add(new TipoCambio
        {
            Id = Guid.NewGuid(),
            Fecha = new DateOnly(2026, 9, 28),
            Valor = 36.50m,
            FuenteId = 1, // BANCO_CENTRAL en la siembra del laboratorio
            OrigenDatoId = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await contexto.Contexto.SaveChangesAsync();
        contexto.Contexto.ChangeTracker.Clear();

        var resultado = await new CargadorTiposCambio(contexto.Contexto).CargarAsync(
            [new() { ["Fecha"] = "2026-09-28", ["Valor"] = "36.80" }],
            Config("TIPOS_CAMBIO", esSimulacion: false));

        Assert.Equal(0, resultado.Insertados);
        Assert.Equal(1, resultado.Actualizados);

        using var verificacion = contexto.Relectura();
        var guardado = await verificacion.TipoCambio.SingleAsync();
        Assert.Equal(36.80m, guardado.Valor);
        Assert.Equal(ConexionErp, guardado.ErpConexionId);
    }

    [Fact]
    public async Task TiposCambio_SegundaVez_OmiteTodo()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var cargador = new CargadorTiposCambio(contexto.Contexto);
        var filas = new List<Dictionary<string, object?>>
        {
            new() { ["Fecha"] = "2026-09-28", ["Valor"] = "36.80" }
        };

        var primera = await cargador.CargarAsync(filas, Config("TIPOS_CAMBIO", esSimulacion: false));
        var segunda = await cargador.CargarAsync(filas, Config("TIPOS_CAMBIO", esSimulacion: false));

        Assert.Equal(1, primera.Insertados);
        Assert.Equal(1, segunda.Omitidos);
    }

    [Fact]
    public async Task TiposCambio_ValorInvalido_Recacha()
    {
        await using var contexto = await CrearLaboratorioAsync();

        var resultado = await new CargadorTiposCambio(contexto.Contexto).CargarAsync(
            [new() { ["Fecha"] = "2026-09-28", ["Valor"] = "0" }],
            Config("TIPOS_CAMBIO", esSimulacion: false));

        Assert.Single(resultado.Rechazos);
        Assert.Contains("mayor que cero", resultado.Rechazos[0].Motivo);
    }
}
