// Fase 6: las pruebas corren sobre una base InMemory. Los SPs de Exactus no se ejecutan: se
// sustituyen por IExactusService falso para comprobar la delegación, los mensajes exactos y las
// descripciones reales que se resolvieron desde las vistas vv*.
#pragma warning disable xUnit1051

using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Inventario;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Inventario;

public sealed class InventarioServiceTests
{
    private const string MensajeGenerado = "Documento de inventario generado satisfactoriamente.";
    private const string YaTransferizado = "1. EL DOCUMENTO DE INVENTARIO YA FUE TRANSFERIDO A EXACTUS";

    private sealed class ExactusFalso : IExactusService
    {
        public string? Requisa { get; private set; }
        public string? Documento { get; private set; }
        public string? Error { get; set; }

        public Task<ExactusResultado> GenerarDesdeRequisaAsync(
            string requisaNumero,
            CancellationToken cancellationToken = default)
        {
            Requisa = requisaNumero;
            return Task.FromResult(new ExactusResultado(true, MensajeGenerado, "CN-0000001"));
        }

        public Task<ExactusResultado> TransferirAsync(
            string documentoInventario,
            CancellationToken cancellationToken = default)
        {
            Documento = documentoInventario;
            return Error is null
                ? Task.FromResult(new ExactusResultado(
                    true,
                    "Documento de inventario trasladado a Exactus satisfactoriamente.",
                    "enviado correctamente"))
                : throw new InvalidOperationException(Error);
        }
    }

    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"docinv-{Guid.NewGuid()}";

        public Laboratorio()
        {
            Contexto = new CotizadorDbContext(
                new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);
            Exactus = new ExactusFalso();
            Sembrar();
        }

        public CotizadorDbContext Contexto { get; }
        public ExactusFalso Exactus { get; }

        public InventarioService Servicio => new(Contexto, Exactus);

        public ValueTask DisposeAsync() => Contexto.DisposeAsync();

        private void Sembrar()
        {
            Contexto.VistaArticuloErp.AddRange(
                new VistaArticuloErp
                {
                    CodigoArticulo = "PQ0067", Descripcion = "AVES",
                    DescripArticulo = "RODENTICIDA FINAL BLOX PARA TODO TIEMPO 18",
                    UnidadVenta = "UND", Activo = "S"
                },
                new VistaArticuloErp
                {
                    CodigoArticulo = "MT0040", Descripcion = "AVES",
                    DescripArticulo = "TRAMPA GOMOSA GRANDE (VICTOR) PARA RATONES",
                    UnidadVenta = "UND", Activo = "S"
                });
            Contexto.VistaBodegaErp.AddRange(
                new VistaBodegaErp { Codigo = "B01", Descripcion = "BODEGA CENTRAL", Activo = "SI" },
                new VistaBodegaErp { Codigo = "B02", Descripcion = "BODEGA VEHICULO", Activo = "SI" });

            Contexto.DocumentoInventario.AddRange(
                Cabecera("CN-0000001", "0026433", "NO", new DateTime(2023, 3, 17)),
                Cabecera("CN-0000002", "0026434", "SI", new DateTime(2023, 3, 18)));
            Contexto.DocumentoInventarioLinea.AddRange(
                Linea("CN-0000001", 1, "PQ0067", "B01", 21m, 76.61m),
                Linea("CN-0000001", 2, "MT0040", "B02", 47m, 423m),
                Linea("CN-0000002", 1, "PQ0067", "B01", 10m, 36.48m));
            Contexto.SaveChanges();
        }
    }

    private static DocumentoInventario Cabecera(
        string documento, string requisa, string transferido, DateTime fecha) => new()
    {
        PaqueteInventario = "CN",
        DocumentoInv = documento,
        Consecutivo = "CONSUMOS",
        OrdenNumero = requisa,
        RequisaNumero = requisa,
        Referencia = $"CLIENTE REAL / ORDEN NO: {requisa}",
        FechaHorCreacion = fecha,
        FechaDocumento = fecha,
        Seleccionado = "N",
        Usuario = "SINCOERP",
        Transferido = transferido
    };

    private static DocumentoInventarioLinea Linea(
        string documento, int linea, string articulo, string bodega, decimal cantidad, decimal total) => new()
    {
        PaqueteInventario = "CN",
        DocumentoInv = documento,
        LineaDocInv = linea,
        AjusteConfig = "~CC~",
        Articulo = articulo,
        Bodega = bodega,
        Tipo = "C",
        Subtipo = "D",
        Subsubtipo = "N",
        Cantidad = cantidad,
        CostoUnitario = total / cantidad,
        CostoTotalLocal = total,
        CostoTotalDolar = 0m,
        PrecioTotalLocal = 0m,
        PrecioTotalDolar = 0m,
        CentroCosto = "10-00-00",
        CuentaContable = "5-1-02-00-00",
        NoteExistsFlag = 0,
        RecordDate = new DateTime(2023, 3, 17)
    };

    [Fact]
    public async Task Listar_FiltraPorTransferidoYTraeBodegaConDescripcion()
    {
        await using var laboratorio = new Laboratorio();

        var pendientes = await laboratorio.Servicio.ListarAsync(transferido: "NO");
        var transferidos = await laboratorio.Servicio.ListarAsync(transferido: "SI");
        var todos = await laboratorio.Servicio.ListarAsync(transferido: "Todos");

        Assert.Single(pendientes);
        Assert.Equal("CN-0000001", pendientes[0].Cabecera.DocumentoInv);
        Assert.Equal("BODEGA CENTRAL (B01)", $"{pendientes[0].NombreBodega} ({pendientes[0].Bodega})");
        Assert.Equal(2, pendientes[0].Lineas);
        Assert.Single(transferidos);
        Assert.Equal(2, todos.Count);
    }

    [Fact]
    public async Task Listar_FiltraPorTextoDelLegacy()
    {
        await using var laboratorio = new Laboratorio();

        var porRequisa = await laboratorio.Servicio.ListarAsync(texto: "0026433");
        var porReferencia = await laboratorio.Servicio.ListarAsync(texto: "CLIENTE REAL");

        Assert.Single(porRequisa);
        Assert.Equal("CN-0000001", porRequisa[0].Cabecera.DocumentoInv);
        Assert.Equal(2, porReferencia.Count);
    }

    [Fact]
    public async Task Obtener_DetallaArticulosConDescripcionRealYUnitarioCalculado()
    {
        await using var laboratorio = new Laboratorio();

        var documento = await laboratorio.Servicio.ObtenerAsync("CN", "CN-0000001");

        Assert.NotNull(documento);
        Assert.Equal(2, documento.Lineas.Count);
        // El WinForms ordena por DESCRIP_ARTICULO: RODENTICIDA precede a TRAMPA.
        Assert.Equal("RODENTICIDA FINAL BLOX PARA TODO TIEMPO 18", documento.Lineas[0].DescripcionArticulo);
        Assert.Equal("PQ0067", documento.Lineas[0].Articulo);
        Assert.Equal("UND", documento.Lineas[0].Unidad);
        Assert.Equal("BODEGA CENTRAL (B01)", $"{documento.Lineas[0].NombreBodega} ({documento.Lineas[0].Bodega})");
        Assert.Equal(76.61m / 21m, documento.Lineas[0].CostoUnitario);
        Assert.Equal("TRAMPA GOMOSA GRANDE (VICTOR) PARA RATONES", documento.Lineas[1].DescripcionArticulo);
        Assert.Equal("MT0040", documento.Lineas[1].Articulo);
        Assert.Equal("BODEGA VEHICULO (B02)", $"{documento.Lineas[1].NombreBodega} ({documento.Lineas[1].Bodega})");
        Assert.Equal(9m, documento.Lineas[1].CostoUnitario);
    }

    [Fact]
    public async Task Obtener_ConClaveInexistenteDevuelveNull()
    {
        await using var laboratorio = new Laboratorio();

        Assert.Null(await laboratorio.Servicio.ObtenerAsync("CN", "CN-9999999"));
    }

    [Fact]
    public async Task Generar_DelegaEnElSpConLaRequisa()
    {
        await using var laboratorio = new Laboratorio();

        var resultado = await laboratorio.Servicio.GenerarDesdeRequisaAsync("0026433");

        Assert.True(resultado.Exitoso);
        Assert.Equal(MensajeGenerado, resultado.Mensaje);
        Assert.Equal("CN-0000001", resultado.ResultadoSp);
        Assert.Equal("0026433", laboratorio.Exactus.Requisa);
    }

    [Fact]
    public async Task Transferir_UsaElNumeroDeDocumentoYPropagaElMensajeDelSp()
    {
        await using var laboratorio = new Laboratorio();
        laboratorio.Exactus.Error = YaTransferizado;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => laboratorio.Servicio.TransferirAsync("CN", "CN-0000001"));

        Assert.Equal(YaTransferizado, ex.Message);
        Assert.Equal("CN-0000001", laboratorio.Exactus.Documento);
    }

    [Fact]
    public async Task Transferir_DocumentoInexistenteNoLlamaAlSp()
    {
        await using var laboratorio = new Laboratorio();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => laboratorio.Servicio.TransferirAsync("CN", "CN-9999999"));

        Assert.Null(laboratorio.Exactus.Documento);
    }
}
