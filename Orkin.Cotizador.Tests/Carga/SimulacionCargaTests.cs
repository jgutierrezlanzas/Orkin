using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Carga;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Carga;

/// <summary>
/// Pruebas de la simulación de cargas (<c>§12.10</c>).
/// <para>Lo que se comprueba aquí es una promesa, no una función: <b>simular no escribe</b> y
/// <b>simular dice lo mismo que la carga real haría</b>. La segunda es la razón de que la
/// primera sirva. Una simulación que predijera otra cosa sería peor que no tenerla, porque
/// alguien le creería y ejecutaría la carga real esperando un resultado que no va a llegar.</para>
/// <para>Usa el proveedor InMemory de EF Core. No es una base de datos: no valida SQL, ni
/// restricciones, ni <c>rowversion</c>. Para estas pruebas sobra, porque lo que se necesita
/// comprobar es una propiedad de la lógica del cargador —qué llama a <c>Add</c> y qué no—, y eso
/// se lee igual en un almacén en memoria. Las pruebas de extremo a extremo contra SQL Server son
/// de otra cosa.</para>
/// </summary>
public sealed class SimulacionCargaTests
{
    private static readonly Guid ConexionErp = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UnidadId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid CategoriaId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    /// <summary>
    /// Una base en memoria con su contexto y una fábrica para abrir otro contexto sobre ella.
    /// <para>La fábrica no es un detalle: para afirmar que una simulación <b>no escribió</b> hay
    /// que releer la base desde fuera, no mirar el mismo contexto. Un <c>SaveChanges</c> pendiente
    /// en el rastreador es invisible para las consultas de EF, así que una prueba que solo
    /// consulta el contexto que ejecutó la carga pasaría aunque la carga hubiera escrito.</para>
    /// <para>Cada laboratorio tiene su base propia porque algunas pruebas crean dos (una para
    /// simular y otra para cargar de verdad) y comparan. Con una base compartida, la segunda
    /// encontraría los productos de la primera y las dos correrían sobre el mismo catálogo.</para>
    /// </summary>
    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"sim-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = Crear();

        public CotizadorDbContext Contexto { get; }

        public CotizadorDbContext Relectura() => Crear();

        private CotizadorDbContext Crear() =>
            new(new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public async ValueTask DisposeAsync() => await Contexto.DisposeAsync();
    }

    // ── Armado del contexto ───────────────────────────────────────────────────

    /// <summary>
    /// Base en memoria con lo mínimo que el cargador necesita: una unidad, una categoría y el
    /// catálogo de origen de precio. Sin el origen de precio el cargador se niega a correr, y esa
    /// negativa es correcta —en producción ese catálogo está sembrado— pero estorbaría para
    /// probar lo que nos interesa.
    /// </summary>
    private static async Task<Laboratorio> CrearLaboratorioAsync(params Producto[] productos)
    {
        var laboratorio = new Laboratorio();
        var contexto = laboratorio.Contexto;

        contexto.UnidadMedida.Add(new UnidadMedida
        {
            Id = UnidadId,
            Codigo = "ML",
            Nombre = "Mililitro",
            Abreviatura = "ml",
            IsActive = true
        });

        contexto.CategoriaProducto.Add(new CategoriaProducto
        {
            Id = CategoriaId,
            Codigo = "INSECTICIDA",
            Nombre = "Insecticida",
            IsActive = true
        });

        var idOrigen = 0;
        foreach (var codigo in new[] { "ERP", "EXCEL" })
        {
            // CatalogoDominioBase.Id es init-only a propósito: lo asigna el IDENTITY de la base
            // y el código de producción nunca lo fija. El InMemory no genera claves, así que la
            // prueba tiene que fijarla, y para una propiedad init-only el único camino es
            // escribir el campo de respaldo que el compilador genera detrás.
            var fila = new OrigenPrecioCatalogo { Codigo = codigo, Nombre = codigo, EsActivo = true };
            AsignarIdCatalogo(fila, (byte)(++idOrigen));
            contexto.OrigenPrecioCatalogo.Add(fila);
        }

        foreach (var p in productos)
        {
            contexto.Producto.Add(p);
        }

        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        return laboratorio;
    }

    /// <summary>
    /// Fija el <c>Id</c> de una fila de catálogo de dominio. Es <c>init</c>, así que no hay
    /// setter que invocar; se escribe el campo de respaldo. Solo para el InMemory, que no genera
    /// claves como sí hace el IDENTITY de SQL Server.
    /// </summary>
    private static void AsignarIdCatalogo<T>(T fila, byte id) where T : CatalogoDominioBase
    {
        // El campo de respaldo lo declara CatalogoDominioBase, no el tipo concreto, y
        // GetField no sube por la jerarquía: hay que preguntar directamente a la base.
        typeof(CatalogoDominioBase)
            .GetField($"<{nameof(CatalogoDominioBase.Id)}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fila, id);
    }

    /// <summary>
    /// Configuración con el mapeo por identidad que usa el servicio cuando el flujo no trae
    /// <c>MapeoJson</c>. Se escribe explícito porque <c>AplicarMapeo</c> es una proyección
    /// estricta: sin entradas en el diccionario, sale vacío. La identidad la arma
    /// <c>IntegracionService.LerMapeo</c> cuando el flujo no define mapeo, y eso ya se probó
    /// con el flujo real; aquí interesa el cargador, no esa resolución.
    /// </summary>
    private static ConfiguracionCarga Config(bool esSimulacion) => new()
    {
        Catalogo = "PRODUCTOS",
        TipoOrigen = "SQL_SERVER",
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codigo"] = "Codigo",
            ["Nombre"] = "Nombre",
            ["CostoActual"] = "CostoActual",
            ["UnidadMedidaCodigo"] = "UnidadMedidaCodigo",
            ["CategoriaCodigo"] = "CategoriaCodigo"
        },
        ErpConexionId = ConexionErp,
        EsSimulacion = esSimulacion
    };

    private static Dictionary<string, object?> Fila(
        string codigo,
        string nombre = "Insumo",
        string costo = "100.00",
        string unidad = "ML",
        string? categoria = "INSECTICIDA") => new()
    {
        ["Codigo"] = codigo,
        ["Nombre"] = nombre,
        ["CostoActual"] = costo,
        ["UnidadMedidaCodigo"] = unidad,
        ["CategoriaCodigo"] = categoria
    };

        private static Producto ProductoExistente(string codigo, string nombre, decimal costo, Guid? conexion = null) => new()
    {
        Codigo = codigo,
        Nombre = nombre,
        CostoActual = costo,
        UnidadMedidaId = UnidadId,
        CategoriaId = CategoriaId,
        OrigenPrecioId = 1,
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
        var cargador = new CargadorProductos(contexto.Contexto);

        var resultado = await cargador.CargarAsync(
            [Fila("P-001"), Fila("P-002"), Fila("P-003")],
            Config(esSimulacion: true));

        Assert.Equal(3, resultado.Insertados);
        Assert.Empty(resultado.Rechazos);

        // Esto es lo que hace que la palabra "simular" sea cierta. Si alguien decide que simular
        // "también puede escribir un poquito", esta es la prueba que se cae.
        Assert.Equal(0, await contexto.Contexto.Producto.CountAsync());
    }

    [Fact]
    public async Task Simulacion_NoModificaProductosExistentes()
    {
        var existente = ProductoExistente("P-001", "Nombre viejo", 100m, ConexionErp);
        await using var contexto = await CrearLaboratorioAsync(existente);
        var cargador = new CargadorProductos(contexto.Contexto);

        var resultado = await cargador.CargarAsync(
            [Fila("P-001", nombre: "Nombre nuevo", costo: "250.00")],
            Config(esSimulacion: true));

        Assert.Equal(1, resultado.Actualizados);

        // Contexto nuevo: lo que importa es lo que quedó en la base, no lo que el rastreador
        // de cambios tenga en memoria. Un "no modificó" que solo se comprueba sobre el
        // rastreador pasaría la prueba con un SaveChanges de más.
        using var verificacion = contexto.Relectura();

        var guardada = await verificacion.Producto.SingleAsync();

        Assert.Equal("Nombre viejo", guardada.Nombre);
        Assert.Equal(100m, guardada.CostoActual);
    }

    [Fact]
    public async Task Simulacion_NoAdoptaHuerfanas()
    {
        // Producto cargado a mano (sin conexión) con el mismo código que trae el ERP. Es el caso
        // de la adopción: el más delicado, porque "no hace nada" aquí significa que la fila sigue
        // sin conexión asignada.
        var huerfana = ProductoExistente("P-001", "Del Excel", 100m, conexion: null);
        await using var contexto = await CrearLaboratorioAsync(huerfana);
        var cargador = new CargadorProductos(contexto.Contexto);

        var resultado = await cargador.CargarAsync(
            [Fila("P-001", nombre: "Del ERP", costo: "150.00")],
            Config(esSimulacion: true));

        var cambio = Assert.Single(resultado.Cambios);
        Assert.Equal(AccionSimulada.Adoptar, cambio.Accion);

        using var verificacion = contexto.Relectura();

        var guardada = await verificacion.Producto.SingleAsync();

        Assert.Null(guardada.ErpConexionId);
        Assert.Equal("Del Excel", guardada.Nombre);
    }

    // ── La otra promesa: decir la verdad ─────────────────────────────────────

    [Fact]
    public async Task Simulacion_CuentaLoMismoQueLaCargaReal()
    {
        var existentes = new[]
        {
            ProductoExistente("P-001", "Igual", 100m, ConexionErp),
            ProductoExistente("P-002", "Viejo", 100m, ConexionErp)
        };

        var filas = new List<Dictionary<string, object?>>
        {
            Fila("P-001", nombre: "Igual"),              // sin cambios
            Fila("P-002", nombre: "Nuevo", costo: "300"), // cambia
            Fila("P-003")                                  // nuevo
        };

        await using var contextoSimulacion = await CrearLaboratorioAsync(existentes);
        var simulado = await new CargadorProductos(contextoSimulacion.Contexto)
            .CargarAsync(filas, Config(esSimulacion: true));

        await using var contextoReal = await CrearLaboratorioAsync(
            ProductoExistente("P-001", "Igual", 100m, ConexionErp),
            ProductoExistente("P-002", "Viejo", 100m, ConexionErp));

        var real = await new CargadorProductos(contextoReal.Contexto)
            .CargarAsync(filas, Config(esSimulacion: false));

        Assert.Equal(real.Insertados, simulado.Insertados);
        Assert.Equal(real.Actualizados, simulado.Actualizados);
        Assert.Equal(real.Omitidos, simulado.Omitidos);
        Assert.Equal(real.Rechazados, simulado.Rechazados);
    }

    [Fact]
    public async Task Simulacion_YCargaReal_CuentanIgualConCodigosRepetidos()
    {
        // Un ERP con una vista mal armada puede traer el mismo artículo dos veces. La carga real
        // lo trata como un alta y una omisión, porque la segunda fila encuentra la que la
        // primera acaba de insertar. Si la simulación lo contara como dos altas, prometería un
        // número de productos nuevos que la carga real no va a producir.
        var filas = new List<Dictionary<string, object?>>
        {
            Fila("P-001", nombre: "Primera vez"),
            Fila("P-001", nombre: "Primera vez"),
            Fila("P-001", nombre: "Primera vez")
        };

        await using var contextoSimulacion = await CrearLaboratorioAsync();
        var simulado = await new CargadorProductos(contextoSimulacion.Contexto)
            .CargarAsync(filas, Config(esSimulacion: true));

        await using var contextoReal = await CrearLaboratorioAsync();
        var real = await new CargadorProductos(contextoReal.Contexto)
            .CargarAsync(filas, Config(esSimulacion: false));

        Assert.Equal(1, simulado.Insertados);
        Assert.Equal(2, simulado.Omitidos);

        Assert.Equal(real.Insertados, simulado.Insertados);
        Assert.Equal(real.Omitidos, simulado.Omitidos);
        Assert.Equal(1, await contextoReal.Contexto.Producto.CountAsync());
    }

    // ── El detalle del diff ──────────────────────────────────────────────────

    [Fact]
    public async Task Simulacion_DiceQueCampoCambiaYASuValorNuevo()
    {
        await using var contexto = await CrearLaboratorioAsync(
            ProductoExistente("P-001", "Nombre viejo", 100m, ConexionErp));

        var resultado = await new CargadorProductos(contexto.Contexto).CargarAsync(
            [Fila("P-001", nombre: "Nombre nuevo", costo: "250.50")],
            Config(esSimulacion: true));

        var cambio = Assert.Single(resultado.Cambios);
        Assert.Equal(AccionSimulada.Actualizar, cambio.Accion);

        var nombre = Assert.Single(cambio.Campos, c => c.Campo == "Nombre");
        Assert.Equal("Nombre viejo", nombre.Anterior);
        Assert.Equal("Nombre nuevo", nombre.Nuevo);

        var costo = Assert.Single(cambio.Campos, c => c.Campo == "Costo");
        Assert.Equal("100.00", costo.Anterior);
        Assert.Equal("250.50", costo.Nuevo);
    }

    [Fact]
    public async Task Simulacion_NoListaCamposQueNoCambian()
    {
        await using var contexto = await CrearLaboratorioAsync(
            ProductoExistente("P-001", "Igual", 100m, ConexionErp));

        var resultado = await new CargadorProductos(contexto.Contexto).CargarAsync(
            [Fila("P-001", nombre: "Distinto")],
            Config(esSimulacion: true));

        var cambio = Assert.Single(resultado.Cambios);

        // Solo el nombre. Si el diff listara los ocho campos siempre, la mitad serían ruido y lo
        // importante —el precio que cambió— se escondería entre ellos.
        Assert.Single(cambio.Campos);
        Assert.Equal("Nombre", cambio.Campos[0].Campo);
    }

    [Fact]
    public async Task Simulacion_DejaCambiosVacioEnCargaReal()
    {
        await using var contexto = await CrearLaboratorioAsync();

        var resultado = await new CargadorProductos(contexto.Contexto)
            .CargarAsync([Fila("P-001")], Config(esSimulacion: false));

        // En una carga real lo que pasó ya está en la base y en la bitácora. Devolver además el
        // diff sería el doble de la información por el doble del tamaño, de algo que ya se
        // puede leer.
        Assert.Empty(resultado.Cambios);
        Assert.Equal(1, await contexto.Contexto.Producto.CountAsync());
    }

    // ── Rechazos ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Simulacion_ReportaRechazosSinEscribir()
    {
        await using var contexto = await CrearLaboratorioAsync();
        var cargador = new CargadorProductos(contexto.Contexto);

        var resultado = await cargador.CargarAsync(
            [Fila("P-001"), Fila("P-002", unidad: "GIGABYTE")],
            Config(esSimulacion: true));

        // La unidad que no existe es el rechazo más común y más útil de ver antes de ejecutar:
        // significa que falta cargar el catálogo de unidades del cliente.
        Assert.Equal(1, resultado.Rechazados);
        Assert.Contains("GIGABYTE", Assert.Single(resultado.Rechazos).Motivo);
        Assert.Equal(1, resultado.Insertados);
        Assert.Equal(0, await contexto.Contexto.Producto.CountAsync());
    }
}
