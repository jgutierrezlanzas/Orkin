// Las acciones que delegan en SPs (anular, requisa) no se ejecutan aquí: el proveedor InMemory
// no entiende ExecuteSqlRaw. Se fijan las reglas que corren ANTES del SP, que son las que el
// WinForms reportaba con sus mensajes exactos.
#pragma warning disable xUnit1051

using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Ordenes;
using Orkin.Cotizador.Core.Application.Visitas;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Ordenes;

public sealed class OrdenTrabajoServiceTests
{
    private const string Aprobada = "No puede guardar, ya que esta orden se encuentra aprobada.";
    private const string YaAnulada = "!!! ATENCION !!! ESTA ORDEN DE TRABAJO YA ESTA ANULADA";
    private const string AnularAprobada =
        "!!! ATENCION !!! ESTA ORDEN DE TRABAJO ESTA APROBADA NO SE PUEDE ANULAR";

    private sealed class GeneradorFalso(CotizadorDbContext contexto) : IGeneradorOrdenTrabajo
    {
        public int Llamadas { get; private set; }

        public Task<string?> GenerarDesdeVisitaAsync(
            long idVisita,
            CancellationToken cancellationToken = default)
        {
            Llamadas++;
            contexto.OrdenTrabajo.Add(new OrdenTrabajo
            {
                OrdenNumero = "0000123",
                IdVisita = idVisita,
                Plantilla = 42,
                Cliente = "000081",
                Estado = "GE",
                FechaOrden = new DateOnly(2026, 11, 20)
            });
            contexto.SaveChanges();
            return Task.FromResult<string?>("0000123");
        }
    }

    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"ot-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = Crear();

        public CotizadorDbContext Contexto { get; }

        private CotizadorDbContext Crear() =>
            new(new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public async ValueTask DisposeAsync() => await Contexto.DisposeAsync();
    }

    private static OrdenTrabajoService Servicio(Laboratorio laboratorio, GeneradorFalso? generador = null)
        => new(laboratorio.Contexto, generador ?? new GeneradorFalso(laboratorio.Contexto));

    private static OrdenTrabajo Cabecera(
        string numero, long plantilla, string cliente, string estado, DateOnly fecha) => new()
    {
        OrdenNumero = numero,
        IdVisita = long.Parse(numero.TrimStart('0')) + 1000,
        Plantilla = plantilla,
        Cliente = cliente,
        Estado = estado,
        FechaOrden = fecha,
        DescripTrabajo = $"Trabajo {numero}"
    };

    private static void Sembrar(Laboratorio laboratorio)
    {
        var contexto = laboratorio.Contexto;
        contexto.OrdenTrabajo.AddRange(
            Cabecera("0000001", 10, "000081", "GE", new DateOnly(2026, 1, 10)),
            Cabecera("0000002", 20, "000099", "AP", new DateOnly(2026, 1, 12)),
            Cabecera("0000003", 30, "000081", "GE", new DateOnly(2026, 2, 10)));
        contexto.OrdenTrabajoDetalle.AddRange(
            new OrdenTrabajoDetalle
            {
                OrdenNumero = "0000001", Plantilla = 10, Consecutivo = 1,
                CodigoArticulo = "A1", Cantidad = 2, CostoUnitario = 5, CostoTotal = 10
            },
            new OrdenTrabajoDetalle
            {
                OrdenNumero = "0000001", Plantilla = 10, Consecutivo = 2,
                CodigoArticulo = "A2", Cantidad = 1, CostoUnitario = 3, CostoTotal = 3
            });
        contexto.OrdenTrabajoServicio.Add(new OrdenTrabajoServicio
        {
            OrdenNumero = "0000001", Plantilla = 10, Consecutivo = 1,
            CodigoServicio = "S002", CodigoFrecuencia = "M001"
        });
        contexto.SaveChanges();
    }

    private static OrdenClave Clave(string numero, long plantilla) => new(numero, long.Parse(numero.TrimStart('0')) + 1000, plantilla);

    [Fact]
    public async Task Listar_FiltraPorTextoClienteEstadoYFecha_yCuentaHijas()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);
        var servicio = Servicio(laboratorio);

        Assert.Equal(3, (await servicio.ListarAsync()).Count);
        Assert.Single(await servicio.ListarAsync(texto: "0000001"));
        Assert.Equal(2, (await servicio.ListarAsync(cliente: "000081")).Count);
        Assert.Equal(2, (await servicio.ListarAsync(estado: "GE")).Count);
        Assert.Single(await servicio.ListarAsync(
            desde: new DateOnly(2026, 2, 1), hasta: new DateOnly(2026, 2, 28)));
        Assert.Empty(await servicio.ListarAsync(texto: "no-existe"));

        var primera = (await servicio.ListarAsync(texto: "0000001"))[0];
        Assert.Equal(2, primera.Detalles);
        Assert.Equal(1, primera.Servicios);
        Assert.Equal(0, primera.Equipos);
        Assert.Equal(0, primera.Tecnicos);
    }

    [Fact]
    public async Task Obtener_ConClaveQueNoExiste_DevuelveNull()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);

        Assert.Null(await servicio.ObtenerAsync(Clave("0000001", 10)));
    }

    [Fact]
    public async Task Actualizar_EnGE_ActualizaCabeceraRecalculaYQuitaSobrantes()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);
        var clave = Clave("0000001", 10);

        var actualizada = await Servicio(laboratorio).ActualizarAsync(clave, new OrdenGuardar(
            "000081", new DateOnly(2026, 1, 11), new TimeSpan(9, 30, 0), "Dirección nueva",
            "Contacto", "1234567", "V10", "sí", "Trabajo editado", "Notas", "Instrucciones",
            [new OrdenTrabajoDetalle
            {
                Consecutivo = 1, CodigoArticulo = "A1", DescripArticulo = "Artículo uno",
                Cantidad = 3, CostoUnitario = 2.5m
            }],
            [new OrdenTrabajoServicio { Consecutivo = 1, CodigoServicio = "S002", CodigoFrecuencia = "M001" }],
            [], []));

        Assert.Equal("Trabajo editado", actualizada.Cabecera.DescripTrabajo);
        Assert.Equal("SI", actualizada.Cabecera.ServicioRealizado);
        Assert.Equal("GE", actualizada.Cabecera.Estado);
        Assert.Single(actualizada.Detalles);
        Assert.Equal(7.5m, actualizada.Detalles[0].CostoTotal);
        Assert.Single(actualizada.Servicios);
        Assert.Single(await laboratorio.Contexto.OrdenTrabajoDetalle.ToListAsync());
    }

    [Fact]
    public async Task Actualizar_EnAPYAN_BloqueaConMensajesDelLegacy()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);
        var servicio = Servicio(laboratorio);
        var datos = DatosVacios();

        laboratorio.Contexto.OrdenTrabajo.First(o => o.OrdenNumero == "0000003").Estado = "AN";
        await laboratorio.Contexto.SaveChangesAsync();

        var ap = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync(Clave("0000002", 20), datos));
        Assert.Equal(Aprobada, ap.Message);

        var an = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ActualizarAsync(Clave("0000003", 30), datos));
        Assert.Equal(YaAnulada, an.Message);
    }

    [Fact]
    public async Task Anular_EnAPYAN_BloqueaConMensajesDelLegacy()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);

        laboratorio.Contexto.OrdenTrabajo.First(o => o.OrdenNumero == "0000003").Estado = "AN";
        await laboratorio.Contexto.SaveChangesAsync();

        var servicio = Servicio(laboratorio);

        var aprobada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.AnularAsync(Clave("0000002", 20)));
        Assert.Equal(AnularAprobada, aprobada.Message);

        var anulada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.AnularAsync(Clave("0000003", 30)));
        Assert.Equal(YaAnulada, anulada.Message);
    }

    [Fact]
    public async Task GenerarRequisa_EnAPYAN_Bloquea()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);

        laboratorio.Contexto.OrdenTrabajo.First(o => o.OrdenNumero == "0000003").Estado = "AN";
        await laboratorio.Contexto.SaveChangesAsync();

        var servicio = Servicio(laboratorio);

        var aprobada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.GenerarRequisaAsync(Clave("0000002", 20)));
        Assert.Equal("La orden ya tiene una requisa generada.", aprobada.Message);

        var anulada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.GenerarRequisaAsync(Clave("0000003", 30)));
        Assert.Equal(YaAnulada, anulada.Message);
    }

    [Fact]
    public async Task CrearDesdeVisita_InvocaAlGeneradorLegacyUnaSolaVez()
    {
        await using var laboratorio = new Laboratorio();
        laboratorio.Contexto.ProgramacionVisitaConfirmada.Add(new ProgramacionVisitaConfirmada
        {
            IdVisita = 55, IdCalendario = "visita-55", Cliente = "000081", Locacion = "L0070",
            TipoServicio = "S002", FechaInicio = new DateOnly(2026, 11, 20),
            HoraInicio = new TimeSpan(9, 0, 0), FechaFinal = new DateOnly(2026, 11, 20),
            HoraFinal = new TimeSpan(10, 0, 0), CodigoVehiculo = "V10", CantidadTecnicos = 1
        });
        await laboratorio.Contexto.SaveChangesAsync();

        var generador = new GeneradorFalso(laboratorio.Contexto);
        var orden = await Servicio(laboratorio, generador).CrearDesdeVisitaAsync(55);

        Assert.Equal("0000123", orden.Cabecera.OrdenNumero);
        Assert.Equal(55, orden.Cabecera.IdVisita);
        Assert.Equal(1, generador.Llamadas);
    }

    private static OrdenGuardar DatosVacios() => new(
        "000081", new DateOnly(2026, 1, 1), null, null, null, null, null, "NO",
        null, null, null, [], [], [], []);
}
