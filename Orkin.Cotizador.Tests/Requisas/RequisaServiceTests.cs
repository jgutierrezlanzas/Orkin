// Fase 5: las pruebas corren sobre una base InMemory. Las acciones que deleguan en SPs
// (SINCO_GENERA_REQUISA y SINCO_ANULAR_ORDENES_DE_TRABAJO) no se ejecutan: se comprueban las
// validaciones que corren ANTES del SP y que devuelven los mensajes exactos del WinForms.
#pragma warning disable xUnit1051

using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Ordenes;
using Orkin.Cotizador.Core.Application.Requisas;
using Orkin.Cotizador.Core.Application.Visitas;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Requisas;

public sealed class RequisaServiceTests
{
    private const string MensajeAprobada =
        "No puede guardar, ya que esta requisa se encuentra aprobada.";
    private const string MensajeBodega = "Debe seleccionar una bodega valida.";
    private const string YaAnulada = "!!! ATENCION !!! ESTA ORDEN DE TRABAJO YA ESTA ANULADA";
    private const string AnularAprobada =
        "!!! ATENCION !!! ESTA ORDEN DE TRABAJO ESTA APROBADA NO SE PUEDE ANULAR";

    private sealed class GeneradorFalso : IGeneradorOrdenTrabajo
    {
        public Task<string?> GenerarDesdeVisitaAsync(
            long idVisita,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"requisa-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = new CotizadorDbContext(
            new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public CotizadorDbContext Contexto { get; }

        public ValueTask DisposeAsync() => Contexto.DisposeAsync();
    }

    private static RequisaService Servicio(Laboratorio laboratorio)
        => new(laboratorio.Contexto, new OrdenTrabajoService(laboratorio.Contexto, new GeneradorFalso()));

    private static Requisa Cabecera(
        string numero, string? orden, string cliente, string locacion, string estado, DateOnly fecha) => new()
    {
        RequisaNumero = numero,
        OrdenNumero = orden,
        Cliente = cliente,
        Locacion = locacion,
        Estado = estado,
        FechaRequisa = fecha,
        FechaOrden = fecha,
        RequisaPendiente = "SI",
        UsuarioCreacion = "SINCOERP"
    };

    private static RequisaDetalle Linea(string numero, int consecutivo, string bodega) => new()
    {
        RequisaNumero = numero,
        Consecutivo = consecutivo,
        Bodega = bodega,
        CodigoArticulo = $"A{consecutivo}",
        DescripArticulo = $"Artículo {consecutivo}",
        Umedida = "UND",
        CantidadEntregada = 5,
        CantidadUtilizada = 3,
        CantidadDevuelta = 1,
        CantidadSobfal = -1,
        CostoUnitario = 2,
        CostoTotal = 6
    };

    private static void Sembrar(Laboratorio laboratorio)
    {
        var contexto = laboratorio.Contexto;
        contexto.VistaClienteErp.Add(new VistaClienteErp
        {
            Cliente = "000081", NombreCliente = "CLIENTE REAL", Activo = "S"
        });
        contexto.VistaClienteErp.Add(new VistaClienteErp
        {
            Cliente = "000099", NombreCliente = "OTRO CLIENTE", Activo = "S"
        });
        contexto.VistaLocacionErp.Add(new VistaLocacionErp
        {
            Codigo = "L0001", Cliente = "000081", Nombre = "LOCACION REAL", Activo = "SI"
        });
        contexto.VistaLocacionErp.Add(new VistaLocacionErp
        {
            Codigo = "L0002", Cliente = "000099", Nombre = "OTRA LOCACION", Activo = "SI"
        });
        contexto.VistaBodegaErp.Add(new VistaBodegaErp
        {
            Codigo = "B01", Descripcion = "BODEGA CENTRAL", Activo = "SI", BodegaPrincipal = "SI"
        });
        contexto.VistaBodegaErp.Add(new VistaBodegaErp
        {
            Codigo = "B99", Descripcion = "BODEGA INACTIVA", Activo = "NO"
        });
        contexto.VistaTecnicoErp.Add(new VistaTecnicoErp
        {
            Codigo = "T001", NombreCompleto = "JUAN PEREZ", Activo = "SI"
        });

        contexto.Requisa.AddRange(
            Cabecera("0000001", "0000001", "000081", "L0001", "GE", new DateOnly(2026, 1, 10)),
            Cabecera("0000002", "0000002", "000099", "L0002", "AP", new DateOnly(2026, 1, 12)),
            Cabecera("0000003", "0000003", "000081", "L0001", "AN", new DateOnly(2026, 2, 10)));
        contexto.RequisaDetalle.AddRange(
            Linea("0000001", 1, "B01"), Linea("0000001", 2, "B01"),
            Linea("0000002", 1, "B01"), Linea("0000003", 1, "B02"));
        contexto.RequisaTecnico.AddRange(
            new RequisaTecnico
            {
                RequisaNumero = "0000001", Consecutivo = 1, CodigoTecnico = "T001", Iniciales = "JP"
            },
            new RequisaTecnico
            {
                RequisaNumero = "0000002", Consecutivo = 1, CodigoTecnico = "T001", Iniciales = "JP"
            },
            new RequisaTecnico
            {
                RequisaNumero = "0000002", Consecutivo = 2, CodigoTecnico = "T002", Iniciales = "ML"
            },
            new RequisaTecnico
            {
                RequisaNumero = "0000003", Consecutivo = 1, CodigoTecnico = "T001", Iniciales = "JP"
            });
        contexto.SaveChanges();
    }

    private static RequisaGuardar DatosValidos() => new(
        "000081", new DateOnly(2026, 1, 11), new DateOnly(2026, 1, 10), "L0001", "Instrucciones reales",
        "B01",
        [new RequisaDetalle
        {
            Consecutivo = 1, CodigoArticulo = "A1", DescripArticulo = "Artículo uno",
            CantidadEntregada = 5, CantidadUtilizada = 3, CantidadDevuelta = 1, CostoUnitario = 2
        }],
        [new RequisaTecnico
        {
            Consecutivo = 1, CodigoTecnico = "T001", Iniciales = "JP"
        }]);

    [Fact]
    public async Task Listar_FiltraPorTextoClienteEstadoYFecha_yCuentaHijas()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);
        var servicio = Servicio(laboratorio);

        Assert.Equal(3, (await servicio.ListarAsync()).Count);
        Assert.Single(await servicio.ListarAsync(texto: "0000001"));
        Assert.Equal(2, (await servicio.ListarAsync(cliente: "000081")).Count);
        Assert.Single(await servicio.ListarAsync(estado: "GE"));
        Assert.Single(await servicio.ListarAsync(
            desde: new DateOnly(2026, 2, 1), hasta: new DateOnly(2026, 2, 28)));
        Assert.Empty(await servicio.ListarAsync(texto: "no-existe"));

        var primera = (await servicio.ListarAsync(texto: "0000001"))[0];
        Assert.Equal("CLIENTE REAL", primera.ClienteNombre);
        Assert.Equal("LOCACION REAL", primera.LocacionNombre);
        Assert.Equal("B01", primera.Bodega);
        Assert.Equal(2, primera.Lineas);
        Assert.Equal(1, primera.Tecnicos);
    }

    [Fact]
    public async Task Obtener_ConNumeroQueNoExiste_DevuelveNull()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);

        Assert.Null(await Servicio(laboratorio).ObtenerAsync("9999999"));
        Assert.NotNull(await Servicio(laboratorio).ObtenerAsync("0000001"));
    }

    [Fact]
    public async Task Actualizar_EnGE_RecalculaSobfalYCosto_yQuitaSobrantes()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);

        var datos = DatosValidos() with
        {
            Lineas =
            [
                new RequisaDetalle
                {
                    Consecutivo = 1, CodigoArticulo = "A1", DescripArticulo = "Artículo editado",
                    CantidadEntregada = 10, CantidadUtilizada = 4, CantidadDevuelta = 2,
                    CostoUnitario = 2.5m
                }
            ]
        };

        var actualizada = await Servicio(laboratorio).ActualizarAsync("0000001", datos);

        Assert.Equal("GE", actualizada.Cabecera.Estado);
        Assert.Equal("0000001", actualizada.Cabecera.OrdenNumero);
        Assert.Equal("Instrucciones reales", actualizada.Cabecera.Instrucciones);
        Assert.Single(actualizada.Lineas);
        Assert.Equal(-4m, actualizada.Lineas[0].CantidadSobfal);
        Assert.Equal(10m, actualizada.Lineas[0].CostoTotal);
        Assert.Equal("B01", actualizada.Lineas[0].Bodega);
        Assert.Single(actualizada.Tecnicos);
        Assert.Equal("JUAN PEREZ", actualizada.NombresTecnico["T001"]);
        Assert.Single(await laboratorio.Contexto.RequisaDetalle
            .Where(d => d.RequisaNumero == "0000001").ToListAsync());
    }

    [Theory]
    [InlineData("AP")]
    [InlineData("AN")]
    public async Task Actualizar_EnAPYAN_BloqueaConMensajeDelLegacy(string estado)
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);
        laboratorio.Contexto.Requisa.First(r => r.RequisaNumero == "0000001").Estado = estado;
        await laboratorio.Contexto.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio(laboratorio).ActualizarAsync("0000001", DatosValidos()));

        Assert.Equal(MensajeAprobada, ex.Message);
    }

    [Fact]
    public async Task Actualizar_ValidacionesDelWinForms_conMensajesExactos()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);
        var servicio = Servicio(laboratorio);

        var sinLineas = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync("0000001", DatosValidos() with { Lineas = [] }));
        Assert.Equal("Debe establecer un producto para esta plantilla.", sinLineas.Message);

        var sinCliente = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync("0000001", DatosValidos() with { Cliente = "  " }));
        Assert.Equal("Debe seleccionar un cliente.", sinCliente.Message);

        var sinTecnicos = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync("0000001", DatosValidos() with { Tecnicos = [] }));
        Assert.Equal("Debe seleccionar al menos un técnico.", sinTecnicos.Message);

        var sinBodega = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync("0000001", DatosValidos() with { Bodega = null }));
        Assert.Equal(MensajeBodega, sinBodega.Message);

        var bodegaInactiva = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync("0000001", DatosValidos() with { Bodega = "B99" }));
        Assert.Equal(MensajeBodega, bodegaInactiva.Message);

        var locacionAjena = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync("0000001", DatosValidos() with { Locacion = "L0002" }));
        Assert.Equal(
            "La locación no existe para este cliente, por favor valide en su ERP que exista la locación L0002 para el cliente 000081.",
            locacionAjena.Message);

        var entregaNegativa = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync("0000001", DatosValidos() with
            {
                Lineas =
                [
                    new RequisaDetalle
                    {
                        Consecutivo = 1, CodigoArticulo = "A1", DescripArticulo = "Artículo uno",
                        CantidadEntregada = -1, CantidadUtilizada = 3, CantidadDevuelta = 1,
                        CostoUnitario = 2
                    }
                ]
            }));
        Assert.Equal("La cantidad entregada no puede ser menor a cero", entregaNegativa.Message);

        var utilizadaNegativa = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync("0000001", DatosValidos() with
            {
                Lineas =
                [
                    new RequisaDetalle
                    {
                        Consecutivo = 1, CodigoArticulo = "A1", DescripArticulo = "Artículo uno",
                        CantidadEntregada = 5, CantidadUtilizada = -1, CantidadDevuelta = 1,
                        CostoUnitario = 2
                    }
                ]
            }));
        Assert.Equal("La cantidad utilizada no puede ser menor a cero", utilizadaNegativa.Message);
    }

    [Fact]
    public async Task CrearDesdeOrden_yAnular_NoEjecutanSP_CuandoElLegacyBloquea()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);
        var contexto = laboratorio.Contexto;
        contexto.OrdenTrabajo.AddRange(
            new OrdenTrabajo
            {
                OrdenNumero = "0000002", IdVisita = 2002, Plantilla = 20, Cliente = "000099", Estado = "AP"
            },
            new OrdenTrabajo
            {
                OrdenNumero = "0000003", IdVisita = 3003, Plantilla = 30, Cliente = "000081", Estado = "AN"
            });
        await contexto.SaveChangesAsync();

        var servicio = Servicio(laboratorio);

        var generaAprobada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearDesdeOrdenAsync(new OrdenClave("0000002", 2002, 20)));
        Assert.Equal("La orden ya tiene una requisa generada.", generaAprobada.Message);

        var generaAnulada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearDesdeOrdenAsync(new OrdenClave("0000003", 3003, 30)));
        Assert.Equal(YaAnulada, generaAnulada.Message);

        var anulaAprobada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.AnularAsync("0000002"));
        Assert.Equal(AnularAprobada, anulaAprobada.Message);

        var anulaAnulada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.AnularAsync("0000003"));
        Assert.Equal(YaAnulada, anulaAnulada.Message);
    }
}
