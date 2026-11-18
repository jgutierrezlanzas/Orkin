// El resto de la suite (PlantillaServiceTests, OrdenTrabajoServiceTests, ...) llama a los servicios
// sin token y usa el optional CancellationToken = default. Se mantiene el mismo patrón aquí.
#pragma warning disable xUnit1051

using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Visitas;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Visitas;

/// <summary>
/// Fase 3: la agenda se gestiona sobre las tablas legacy existentes (D-22). Estas pruebas fijan
/// el consecutivo <c>MAX+1</c>, la reversa en cascada manual y el respeto a la orden generada.
/// </summary>
public sealed class VisitaServiceTests
{
    private sealed class UsuarioPrueba : IContextoUsuario
    {
        public Guid? UsuarioId => Guid.NewGuid();
        public string? NombreUsuario => "AZAPATA";
        public string? Ip => null;
        public string? Agente => null;
        public string? TraceId => null;
    }

    private sealed class GeneradorFalso : IGeneradorOrdenTrabajo
    {
        public int Llamadas { get; private set; }

        public Task<string?> GenerarDesdeVisitaAsync(
            long idVisita,
            CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return Task.FromResult<string?>("0000123");
        }
    }

    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"vi-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = Crear();

        public CotizadorDbContext Contexto { get; }

        public CotizadorDbContext Relectura() => Crear();

        private CotizadorDbContext Crear() =>
            new(new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public async ValueTask DisposeAsync() => await Contexto.DisposeAsync();
    }

    private static VisitaService Servicio(Laboratorio laboratorio, GeneradorFalso generador)
        => new(laboratorio.Contexto, new UsuarioPrueba(), generador);

    private static VisitaGuardar DatosBase(string evento = "evento-1") => new(
        evento, "000081", "L0070", "S001",
        new DateOnly(2026, 11, 20), new TimeSpan(9, 0, 0),
        new DateOnly(2026, 11, 20), new TimeSpan(10, 30, 0),
        "V10", "Notas de prueba", 42,
        [new VisitaTecnicoGuardar("T02", "OA"), new VisitaTecnicoGuardar("T04", "SC")]);

    private static void Sembrar(Laboratorio laboratorio)
    {
        laboratorio.Contexto.ProgramacionVisitaConfirmada.Add(new ProgramacionVisitaConfirmada
        {
            IdVisita = 7, IdCalendario = "antigua", Cliente = "000001", Locacion = "L0001",
            TipoServicio = "S000", FechaInicio = new DateOnly(2026, 1, 1),
            HoraInicio = new TimeSpan(8, 0, 0), FechaFinal = new DateOnly(2026, 1, 1),
            HoraFinal = new TimeSpan(9, 0, 0), CodigoVehiculo = "V00", CantidadTecnicos = 1
        });
        laboratorio.Contexto.SaveChanges();
    }

    [Fact]
    public async Task Crear_AsignaMaximoMasUnoYGuardaDetalleYCalendario()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);
        var generador = new GeneradorFalso();

        var creada = await Servicio(laboratorio, generador).CrearAsync(DatosBase());

        Assert.Equal(8, creada.Visita.IdVisita);
        Assert.Equal("evento-1", creada.Visita.IdCalendario);
        Assert.Equal(2, creada.Tecnicos.Count);
        Assert.Contains(creada.Tecnicos, t => t.Linea == 1 && t.CodigoTecnico == "T02");
        Assert.Contains(creada.Tecnicos, t => t.Linea == 2 && t.CodigoTecnico == "T04");
        Assert.Equal(42, creada.Plantilla);
        Assert.Equal("AZAPATA", creada.Calendarios.Single().UsuarioCreacion);

        using var relectura = laboratorio.Relectura();
        Assert.Equal(2, await relectura.ProgramacionVisitaConfirmada.CountAsync());
        Assert.Equal(2, await relectura.ProgramacionVisitaDetalleConfirmada.CountAsync());
        Assert.Single(await relectura.PlantillaCalendario.ToListAsync());
    }

    [Fact]
    public async Task Crear_SinTecnicoNiPlantilla_FallaSinEscribir()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio, new GeneradorFalso());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearAsync(DatosBase() with { Tecnicos = [] }));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearAsync(DatosBase() with { Plantilla = null }));

        Assert.Empty(await laboratorio.Contexto.ProgramacionVisitaConfirmada.ToListAsync());
    }

    [Fact]
    public async Task Crear_GeneraEventoPropioCuandoNoVieneDeGoogle()
    {
        await using var laboratorio = new Laboratorio();

        var creada = await Servicio(laboratorio, new GeneradorFalso())
            .CrearAsync(DatosBase(evento: null));

        Assert.StartsWith("ORKIN-", creada.Visita.IdCalendario);
    }

    [Fact]
    public async Task Reversar_BorraDetalleVisitaYCalendario()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio, new GeneradorFalso());
        var creada = await servicio.CrearAsync(DatosBase());

        await servicio.ReversarAsync(creada.Visita.IdVisita);

        Assert.Empty(await laboratorio.Contexto.ProgramacionVisitaDetalleConfirmada.ToListAsync());
        Assert.Empty(await laboratorio.Contexto.PlantillaCalendario.ToListAsync());
        Assert.Empty(await laboratorio.Contexto.ProgramacionVisitaConfirmada.ToListAsync());
    }

    [Fact]
    public async Task Reversar_ConOrdenGenerada_BloqueaYBorraNada()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio, new GeneradorFalso());
        var creada = await servicio.CrearAsync(DatosBase());
        laboratorio.Contexto.OrdenTrabajo.Add(new OrdenTrabajo
        {
            OrdenNumero = "0000123", IdVisita = creada.Visita.IdVisita, Plantilla = 42, Estado = "GE"
        });
        await laboratorio.Contexto.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ReversarAsync(creada.Visita.IdVisita));

        Assert.Single(await laboratorio.Contexto.ProgramacionVisitaConfirmada.ToListAsync());
        Assert.Equal(2, await laboratorio.Contexto.ProgramacionVisitaDetalleConfirmada.CountAsync());
    }

    [Fact]
    public async Task GenerarOrden_InvocaAlGeneradorLegacyUnaSolaVez()
    {
        await using var laboratorio = new Laboratorio();
        var generador = new GeneradorFalso();
        var servicio = Servicio(laboratorio, generador);
        var creada = await servicio.CrearAsync(DatosBase());

        var numero = await servicio.GenerarOrdenTrabajoAsync(creada.Visita.IdVisita);

        Assert.Equal("0000123", numero);
        Assert.Equal(1, generador.Llamadas);
    }

    [Fact]
    public async Task Listar_FiltraPorRangoYCliente()
    {
        await using var laboratorio = new Laboratorio();
        Sembrar(laboratorio);
        var servicio = Servicio(laboratorio, new GeneradorFalso());
        await servicio.CrearAsync(DatosBase());

        var todas = await servicio.ListarAsync();
        var filtradas = await servicio.ListarAsync(
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), cliente: "000081");
        var sinCoincidencias = await servicio.ListarAsync(cliente: "999999");

        Assert.Equal(2, todas.Count);
        Assert.Single(filtradas);
        Assert.Equal("evento-1", filtradas[0].Visita.IdCalendario);
        Assert.Empty(sinCoincidencias);
    }
}
