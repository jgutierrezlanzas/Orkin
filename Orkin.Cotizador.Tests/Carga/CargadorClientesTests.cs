using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Carga;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Carga;

/// <summary>
/// Pruebas del cargador de clientes y de su simulación.
/// <para>Lo que se comprueba es la misma promesa que en <see cref="SimulacionCargaTests"/>:
/// <b>simular no escribe</b> y <b>simular dice lo mismo que la carga real haría</b>, aplicada al
/// segundo catálogo que vive en el ERP del cliente. El catálogo de clientes tiene una regla que
/// el de productos no tiene: el tipo de cliente es obligatorio porque decide el método de
/// costeo, y cotizarle a alguien con el método de otro tipo es cotizar mal sin que se note.</para>
/// </summary>
public sealed class CargadorClientesTests
{
    private static readonly Guid ConexionErp = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"cli-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = Crear();

        public CotizadorDbContext Contexto { get; }

        public CotizadorDbContext Relectura() => Crear();

        private CotizadorDbContext Crear() =>
            new(new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public async ValueTask DisposeAsync() => await Contexto.DisposeAsync();
    }

    private static async Task<Laboratorio> CrearLaboratorioAsync(params Cliente[] clientes)
    {
        var laboratorio = new Laboratorio();
        var contexto = laboratorio.Contexto;

        var idTipo = 0;
        foreach (var (codigo, nombre) in new[] { ("EMPRESARIAL", "Empresarial"), ("RESIDENCIAL", "Residencial") })
        {
            var fila = new TipoClienteCatalogo { Codigo = codigo, Nombre = nombre, EsActivo = true };
            AsignarIdCatalogo(fila, (byte)(++idTipo));
            contexto.TipoClienteCatalogo.Add(fila);
        }

        var idOrigen = 0;
        foreach (var (codigo, nombre) in new[] { ("ERP", "Sincronizado desde un ERP"), ("MANUAL", "Capturado en la web") })
        {
            var fila = new OrigenDatoCatalogo { Codigo = codigo, Nombre = nombre, EsActivo = true };
            AsignarIdCatalogo(fila, (byte)(++idOrigen));
            contexto.OrigenDatoCatalogo.Add(fila);
        }

        foreach (var c in clientes)
        {
            contexto.Cliente.Add(c);
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

    private static ConfiguracionCarga Config(bool esSimulacion) => new()
    {
        Catalogo = "CLIENTES",
        TipoOrigen = "SQL_SERVER",
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codigo"] = "Codigo",
            ["Nombre"] = "Nombre",
            ["RazonSocial"] = "RazonSocial",
            ["TipoClienteCodigo"] = "TipoClienteCodigo",
            ["CedulaORuc"] = "CedulaORuc",
            ["Correo"] = "Correo",
            ["Direccion"] = "Direccion",
            ["Ciudad"] = "Ciudad"
        },
        ErpConexionId = ConexionErp,
        EsSimulacion = esSimulacion
    };

    private static Dictionary<string, object?> Fila(
        string codigo,
        string nombre = "Cliente Demo",
        string tipo = "EMPRESARIAL",
        string? correo = "compras@demo.ni") => new()
    {
        ["Codigo"] = codigo,
        ["Nombre"] = nombre,
        ["RazonSocial"] = null,
        ["TipoClienteCodigo"] = tipo,
        ["CedulaORuc"] = null,
        ["Correo"] = correo,
        ["Direccion"] = "De la fuente 2c al sur",
        ["Ciudad"] = "Managua"
    };

    private static Cliente ClienteExistente(string codigo, string nombre, byte tipoId = 1, Guid? conexion = null) => new()
    {
        Codigo = codigo,
        Nombre = nombre,
        TipoClienteId = tipoId,
        OrigenDatoId = 2,
        ErpConexionId = conexion,
        CodigoExterno = conexion is null ? null : codigo,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // ── La promesa central: no escribir ───────────────────────────────────────

    [Fact]
    public async Task Simulacion_NoInsertaNadaEnElCatalogo()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var cargador = new CargadorClientes(contexto.Contexto);

        var resultado = await cargador.CargarAsync(
            [Fila("CT-001"), Fila("CT-002"), Fila("CT-003")],
            Config(esSimulacion: true));

        Assert.Equal(3, resultado.Insertados);
        Assert.Empty(resultado.Rechazos);
        Assert.Equal(0, await contexto.Contexto.Cliente.CountAsync());
    }

    [Fact]
    public async Task Simulacion_NoModificaClientesExistentes()
    {
        var existente = ClienteExistente("CT-001", "Nombre viejo", conexion: ConexionErp);
        await using var contexto = await CrearLaboratorioAsync(existente);
        var cargador = new CargadorClientes(contexto.Contexto);

        var resultado = await cargador.CargarAsync(
            [Fila("CT-001", nombre: "Nombre nuevo")],
            Config(esSimulacion: true));

        Assert.Equal(1, resultado.Actualizados);

        using var verificacion = contexto.Relectura();
        var guardado = await verificacion.Cliente.SingleAsync();

        Assert.Equal("Nombre viejo", guardado.Nombre);
    }

    // ── La otra promesa: la simulación predice la carga ────────────────────────

    [Fact]
    public async Task SimulacionYCarga_DicenLoMismo()
    {
        var existente = ClienteExistente("CT-001", "Viejo nombre", conexion: ConexionErp);

        await using var laboratorioSimulacion = await CrearLaboratorioAsync(existente);
        await using var laboratorioReal = await CrearLaboratorioAsync(ClienteExistente("CT-001", "Viejo nombre", conexion: ConexionErp));

        var filas = new[] { Fila("CT-001", nombre: "Nombre nuevo"), Fila("CT-002") };

        var simulada = await new CargadorClientes(laboratorioSimulacion.Contexto).CargarAsync(filas, Config(esSimulacion: true));
        var real = await new CargadorClientes(laboratorioReal.Contexto).CargarAsync(filas, Config(esSimulacion: false));

        Assert.Equal(simulada.Insertados, real.Insertados);
        Assert.Equal(simulada.Actualizados, real.Actualizados);
        Assert.Equal(simulada.Omitidos, real.Omitidos);
        Assert.Equal(simulada.Rechazados, real.Rechazados);
    }

    [Fact]
    public async Task CargaSegundaVez_OmiteTodo()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var cargador = new CargadorClientes(contexto.Contexto);
        var filas = new[] { Fila("CT-001"), Fila("CT-002") };

        var primera = await cargador.CargarAsync(filas, Config(esSimulacion: false));
        var segunda = await cargador.CargarAsync(filas, Config(esSimulacion: false));

        Assert.Equal(2, primera.Insertados);
        Assert.Equal(2, segunda.Omitidos);
        Assert.Equal(0, segunda.Insertados + segunda.Actualizados);
        Assert.Equal(2, await contexto.Contexto.Cliente.CountAsync());
    }

    [Fact]
    public async Task Carga_InsertaConLosDatosDelOrigen()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var cargador = new CargadorClientes(contexto.Contexto);

        await cargador.CargarAsync([Fila("CT-001", nombre: "Hotel Demo", tipo: "EMPRESARIAL")], Config(esSimulacion: false));

        using var verificacion = contexto.Relectura();
        var guardado = await verificacion.Cliente.SingleAsync();

        Assert.Equal("Hotel Demo", guardado.Nombre);
        Assert.Equal((byte)1, guardado.TipoClienteId);
        Assert.Equal(ConexionErp, guardado.ErpConexionId);
        Assert.Equal("CT-001", guardado.CodigoExterno);
        Assert.Equal(1, guardado.OrigenDatoId); // ERP
        Assert.NotNull(guardado.HashContenido);
    }

    // ── Adopción: la ficha capturada a mano la reclama el ERP ─────────────────

    [Fact]
    public async Task Carga_AdoptaLaFichaCapturadaAManoConElMismoCodigo()
    {
        // Capturada a mano: sin conexión. El ERP trae al mismo cliente con el mismo código.
        var manual = ClienteExistente("CT-001", "Hotel Demo", tipoId: 2);
        await using var contexto = await CrearLaboratorioAsync(manual);
        var cargador = new CargadorClientes(contexto.Contexto);

        var resultado = await cargador.CargarAsync(
            [Fila("CT-001", nombre: "Hotel Demo")],
            Config(esSimulacion: false));

        // La fila ya estaba: el contador responde "¿cuántas filas nuevas hay?", no "¿cuántos
        // UPDATE ejecuté?".
        Assert.Equal(0, resultado.Insertados);
        Assert.Equal(1, resultado.Actualizados);

        using var verificacion = contexto.Relectura();
        var guardado = await verificacion.Cliente.SingleAsync();

        Assert.Equal(ConexionErp, guardado.ErpConexionId);
        Assert.Equal("CT-001", guardado.CodigoExterno);
        Assert.Equal((byte)1, guardado.TipoClienteId); // el del ERP manda
        Assert.Equal(1, await verificacion.Cliente.CountAsync());
    }

    // ── El tipo de cliente es obligatorio y tiene que existir ──────────────────

    [Fact]
    public async Task Carga_RecachaLaFilaSinTipoDeCliente()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var cargador = new CargadorClientes(contexto.Contexto);

        var filas = new List<Dictionary<string, object?>>
        {
            new(Fila("CT-001")) { ["TipoClienteCodigo"] = null }
        };

        var resultado = await cargador.CargarAsync(filas, Config(esSimulacion: false));

        Assert.Equal(0, resultado.Insertados);
        Assert.Single(resultado.Rechazos);
        Assert.Contains("TipoClienteCodigo", resultado.Rechazos[0].Motivo);
    }

    [Fact]
    public async Task Carga_RecachaLaFilaConTipoDesconocido()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var cargador = new CargadorClientes(contexto.Contexto);

        var resultado = await cargador.CargarAsync(
            [Fila("CT-001", tipo: "GOBIERNO")],
            Config(esSimulacion: false));

        Assert.Single(resultado.Rechazos);
        Assert.Equal(0, await contexto.Contexto.Cliente.CountAsync());
    }

    // ── El código repetido en el origen se comporta igual en los dos modos ─────

    [Fact]
    public async Task CodigoRepetido_EnSimulacionYCargaDaLoMismo()
    {
        await using var laboratorioSimulacion = await CrearLaboratorioAsync();
        await using var laboratorioReal = await CrearLaboratorioAsync();

        var filas = new[] { Fila("CT-001", nombre: "Uno"), Fila("CT-001", nombre: "Dos") };

        var simulada = await new CargadorClientes(laboratorioSimulacion.Contexto).CargarAsync(filas, Config(esSimulacion: true));
        var real = await new CargadorClientes(laboratorioReal.Contexto).CargarAsync(filas, Config(esSimulacion: false));

        // La primera aparición crea y la segunda actualiza; ninguna de las dos inserta dos filas.
        Assert.Equal(1, simulada.Insertados);
        Assert.Equal(1, real.Insertados);
        Assert.Equal(1, await laboratorioReal.Contexto.Cliente.CountAsync());
    }
}
