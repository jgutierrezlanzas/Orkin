// Mismo patrón que el resto de la suite: servicios con CancellationToken opcional, sin token.
#pragma warning disable xUnit1051

using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Visitas;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Visitas;

/// <summary>
/// Fase 3: el importador enriquece el flujo legacy con staging y homologación, pero solo escribe
/// visitas reales al confirmar. Estas pruebas fijan el parseo, la validación contra <c>vv*</c> y
/// el paso a <c>ProgramacionVisitaConfirmadas</c>.
/// </summary>
public sealed class ImportadorGoogleServiceTests
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
        public Task<string?> GenerarDesdeVisitaAsync(
            long idVisita,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("0000123");
    }

    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"ig-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = Crear();

        public CotizadorDbContext Contexto { get; }

        private CotizadorDbContext Crear() =>
            new(new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public async ValueTask DisposeAsync() => await Contexto.DisposeAsync();
    }

    private static ImportadorGoogleService Servicio(Laboratorio laboratorio)
        => new(laboratorio.Contexto, new VisitaService(
            laboratorio.Contexto, new UsuarioPrueba(), new GeneradorFalso()));

    private static void SembrarErp(Laboratorio laboratorio)
    {
        var contexto = laboratorio.Contexto;
        contexto.VistaClienteErp.Add(new VistaClienteErp
        {
            Cliente = "000081", NombreCliente = "CLIENTE REAL", Activo = "S"
        });
        contexto.VistaLocacionErp.Add(new VistaLocacionErp
        {
            Codigo = "L0070", Cliente = "000081", Activo = "SI"
        });
        contexto.VistaTipoServicioErp.Add(new VistaTipoServicioErp
        {
            Codigo = "S001", Activo = "SI"
        });
        contexto.VistaVehiculoErp.Add(new VistaVehiculoErp
        {
            Codigo = "V10", Activo = "SI"
        });
        contexto.VistaTecnicoErp.AddRange(
            new VistaTecnicoErp { Codigo = "T02", Iniciales = "OA", Activo = "SI" },
            new VistaTecnicoErp { Codigo = "T04", Iniciales = "SC", Activo = "SI" });
        contexto.PlantillaOrdenTrabajo.Add(new PlantillaOrdenTrabajo
        {
            Plantilla = 42, Cliente = "000081", Locacion = "L0070", Activo = "SI"
        });
        contexto.SaveChanges();
    }

    private static ImportacionGoogleRequest UnEvento(
        string cliente = "000081",
        string locacion = "L0070",
        string servicio = "S001",
        string vehiculo = "V10",
        string tecnicos = "OA,SC")
        => new("Prueba", "Google Calendar",
        [
            new EventoGoogleImportar(
                "evt-1", "cal-1",
                $"Orkin - Servicio/{cliente}-Cliente/{locacion}",
                $"{locacion}-Dirección real",
                $"{servicio}-Fumigación/Vehiculo: {vehiculo}/Tecnico: {tecnicos}/Nota de prueba",
                new DateTime(2026, 11, 20, 9, 0, 0),
                new DateTime(2026, 11, 20, 10, 30, 0))
        ]);

    [Fact]
    public async Task Importar_ParseaComoLegacyYDejaElEventoListo()
    {
        await using var laboratorio = new Laboratorio();
        SembrarErp(laboratorio);

        var resumen = await Servicio(laboratorio).ImportarAsync(UnEvento());

        Assert.Equal(1, resumen.TotalEventos);
        Assert.Equal(1, resumen.Listos);
        Assert.Equal(0, resumen.Conflictos);

        var evento = Assert.Single(await laboratorio.Contexto.ImportacionGoogleEvento.ToListAsync());
        Assert.Equal("000081", evento.ClienteParseado);
        Assert.Equal("L0070", evento.LocacionParseada);
        Assert.Equal("S001", evento.TipoServicioParseado);
        Assert.Equal("V10", evento.VehiculoParseado);
        Assert.Equal("OA,SC", evento.TecnicosParseados);
        Assert.Equal("Nota de prueba", evento.Nota);
        Assert.Equal("OA,SC", evento.TecnicosCodigos);
        Assert.Equal(EstadoImportacionGoogle.Listo, evento.Estado);
    }

    [Fact]
    public async Task Importar_AplicaHomologacionConfigurable()
    {
        await using var laboratorio = new Laboratorio();
        SembrarErp(laboratorio);
        laboratorio.Contexto.ErpMapeoEventoGoogle.Add(new ErpMapeoEventoGoogle
        {
            Id = Guid.NewGuid(), Campo = "CLIENTE", ValorOrigen = "RESIDENCIAL",
            ValorDestino = "000081", Activo = true
        });
        await laboratorio.Contexto.SaveChangesAsync();

        var resumen = await Servicio(laboratorio)
            .ImportarAsync(UnEvento(cliente: "RESIDENCIAL"));

        var evento = Assert.Single(await laboratorio.Contexto.ImportacionGoogleEvento.ToListAsync());
        Assert.Equal("RESIDENCIAL", evento.ClienteParseado);
        Assert.Equal("000081", evento.ClienteCodigo);
        Assert.Equal(1, resumen.Listos);
    }

    [Fact]
    public async Task Importar_CodigoInactivo_QuedaEnConflictoConMotivo()
    {
        await using var laboratorio = new Laboratorio();
        SembrarErp(laboratorio);

        var resumen = await Servicio(laboratorio)
            .ImportarAsync(UnEvento(vehiculo: "V99"));

        var evento = Assert.Single(await laboratorio.Contexto.ImportacionGoogleEvento.ToListAsync());
        Assert.Equal(EstadoImportacionGoogle.Conflicto, evento.Estado);
        Assert.Contains("Vehículo", evento.Error);
        Assert.Equal(1, resumen.Conflictos);
    }

    [Fact]
    public async Task Confirmar_CreaVisitaLegacyYMarcaElEvento()
    {
        await using var laboratorio = new Laboratorio();
        SembrarErp(laboratorio);
        var servicio = Servicio(laboratorio);
        await servicio.ImportarAsync(UnEvento());
        var evento = Assert.Single(await laboratorio.Contexto.ImportacionGoogleEvento.ToListAsync());
        await servicio.CorregirAsync(evento.Id, new PlantillaCorreccion().Aplicar(42));

        var idVisita = await servicio.ConfirmarAsync(evento.Id);

        var visita = await laboratorio.Contexto.ProgramacionVisitaConfirmada.SingleAsync();
        Assert.Equal(idVisita, visita.IdVisita);
        Assert.Equal("evt-1", visita.IdCalendario);
        Assert.Equal("000081", visita.Cliente);
        Assert.Equal(2, visita.CantidadTecnicos);
        Assert.Equal(2, await laboratorio.Contexto.ProgramacionVisitaDetalleConfirmada.CountAsync());
        Assert.Single(await laboratorio.Contexto.PlantillaCalendario.ToListAsync());

        var corregido = await laboratorio.Contexto.ImportacionGoogleEvento.SingleAsync();
        Assert.Equal(EstadoImportacionGoogle.Confirmado, corregido.Estado);
        Assert.Equal(idVisita, corregido.IdVisita);
        Assert.NotNull(corregido.ProcesadoUtc);
    }

    [Fact]
    public async Task Descartar_NoCreaNingunaVisita()
    {
        await using var laboratorio = new Laboratorio();
        SembrarErp(laboratorio);
        var servicio = Servicio(laboratorio);
        await servicio.ImportarAsync(UnEvento());
        var evento = Assert.Single(await laboratorio.Contexto.ImportacionGoogleEvento.ToListAsync());

        Assert.True(await servicio.DescartarAsync(evento.Id));

        Assert.Empty(await laboratorio.Contexto.ProgramacionVisitaConfirmada.ToListAsync());
        Assert.Equal(EstadoImportacionGoogle.Descartado,
            (await laboratorio.Contexto.ImportacionGoogleEvento.SingleAsync()).Estado);
    }

    /// <summary>Ayuda para fijar la plantilla en la corrección manual del evento.</summary>
    private sealed class PlantillaCorreccion
    {
        public CorregirEventoGoogleDatos Aplicar(long plantilla) => new(
            "000081", "L0070", "S001", "V10", "OA,SC", "Nota de prueba", plantilla);
    }
}
