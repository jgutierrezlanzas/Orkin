using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Plantillas;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Plantillas;

/// <summary>
/// D-22: la gestión de plantillas trabaja sobre las tablas legacy. Estas pruebas verifican que
/// el servicio respete sus claves (<c>PLANTILLA</c>, <c>CONSECUTIVO</c>), sus códigos (<c>SI/NO</c>)
/// y la auditoría <c>FECHA_CREACION</c>/<c>USUARIO_CREACION</c>, sin crear tablas paralelas.
/// </summary>
public sealed class PlantillaServiceTests
{
    private sealed class UsuarioPrueba : IContextoUsuario
    {
        public Guid? UsuarioId => Guid.NewGuid();
        public string? NombreUsuario => "AZAPATA";
        public string? Ip => null;
        public string? Agente => null;
        public string? TraceId => null;
    }

    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"pl-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = Crear();

        public CotizadorDbContext Contexto { get; }

        public CotizadorDbContext Relectura() => Crear();

        private CotizadorDbContext Crear() =>
            new(new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public async ValueTask DisposeAsync() => await Contexto.DisposeAsync();
    }

    private static PlantillaService Servicio(Laboratorio laboratorio)
        => new(laboratorio.Contexto, new UsuarioPrueba());

    private static PlantillaGuardar DatosBase(string descripcion = "Fumigación básica") => new(
        "000081", "L0070", "Calle 1", null, "Contacto", "555", null, descripcion, "Notas", "SI",
        [
            new PlantillaOrdenTrabajoDetalle
            {
                CodigoArticulo = "PQ001", DescripArticulo = "Producto A", TipoPlaga = "P01",
                Cantidad = 2m, CostoUnitario = 10m, MaximoPermitido = 99m
            },
            new PlantillaOrdenTrabajoDetalle
            {
                CodigoArticulo = "MT002", DescripArticulo = "Producto B", Cantidad = 1m,
                CostoUnitario = 5m, MaximoPermitido = 50m
            }
        ],
        [new PlantillaOrdenTrabajoServicio { CodigoServicio = "S01", CodigoFrecuencia = "MENS" }],
        [new PlantillaOrdenTrabajoEquipos { CodigoEquipo = "E01", Utilizado = "SI" }]);

    [Fact]
    public async Task Crear_AsignaSiguientePlantillaYAduitoriaLegacy()
    {
        await using var laboratorio = new Laboratorio();
        var plantilla = await Servicio(laboratorio).CrearAsync(DatosBase());

        Assert.Equal(1, plantilla.Cabecera.Plantilla);
        Assert.Equal("000081", plantilla.Cabecera.Cliente);
        Assert.Equal("SI", plantilla.Cabecera.Activo);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), plantilla.Cabecera.FechaCreacion);
        Assert.Equal("AZAPATA", plantilla.Cabecera.UsuarioCreacion);
        // FrmCrudPlantillas dejaba TECNICO y VEHICULO vacíos en el alta.
        Assert.Equal(string.Empty, plantilla.Cabecera.Tecnico);
        Assert.Equal(string.Empty, plantilla.Cabecera.Vehiculo);
    }

    [Fact]
    public async Task Crear_RenumerizaDesdeUnoYCalculaCostoTotal()
    {
        await using var laboratorio = new Laboratorio();
        var plantilla = await Servicio(laboratorio).CrearAsync(DatosBase());

        Assert.Equal([1, 2], plantilla.Detalles.Select(d => d.Consecutivo).ToArray());
        Assert.Equal(20m, plantilla.Detalles[0].CostoTotal);
        Assert.Equal(5m, plantilla.Detalles[1].CostoTotal);
        Assert.Equal("SI", Assert.Single(plantilla.Equipos).Utilizado);
    }

    [Fact]
    public async Task Crear_SinHijosObligatorios_LanzaError()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        var datos = DatosBase() with { Equipos = [] };

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CrearAsync(datos));
        Assert.Empty(await laboratorio.Contexto.PlantillaOrdenTrabajo.ToListAsync());
    }

    [Fact]
    public async Task Copiar_ConservaMaximoPermitidoYUsaNuevoNumero()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        var original = await servicio.CrearAsync(DatosBase());

        var copia = await servicio.CopiarAsync(original.Cabecera.Plantilla);

        Assert.Equal(2, copia.Cabecera.Plantilla);
        Assert.Equal("SI", copia.Cabecera.Activo);
        Assert.Equal([1, 2], copia.Detalles.Select(d => d.Consecutivo).ToArray());
        // El copiador del WinForms perdía este valor; el de la API lo conserva.
        Assert.Equal([99m, 50m], copia.Detalles.Select(d => d.MaximoPermitido).ToArray());
        Assert.Equal(original.Detalles.Select(d => d.CodigoArticulo), copia.Detalles.Select(d => d.CodigoArticulo));
        Assert.Single(copia.Servicios);
        Assert.Single(copia.Equipos);
    }

    [Fact]
    public async Task DarDeBaja_PoneActivoNoConservaLineas()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        var plantilla = await servicio.CrearAsync(DatosBase());

        await servicio.DarDeBajaAsync(plantilla.Cabecera.Plantilla);

        var guardada = await servicio.ObtenerAsync(plantilla.Cabecera.Plantilla);
        Assert.Equal("NO", guardada!.Cabecera.Activo);
        Assert.Equal(2, guardada.Detalles.Count);
        Assert.Equal(1, await laboratorio.Contexto.PlantillaOrdenTrabajo.CountAsync());
    }

    [Fact]
    public async Task Actualizar_ReconciliaLineasPorConsecutivo()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        var plantilla = await servicio.CrearAsync(DatosBase());

        await using var contextoNuevo = laboratorio.Relectura();
        await new PlantillaService(contextoNuevo, new UsuarioPrueba()).ActualizarAsync(
            plantilla.Cabecera.Plantilla,
            DatosBase("Fumigación editada") with
            {
                Detalles =
                [
                    new PlantillaOrdenTrabajoDetalle
                    {
                        CodigoArticulo = "PQ001", DescripArticulo = "Producto A editado",
                        Cantidad = 3m, CostoUnitario = 10m, MaximoPermitido = 77m
                    }
                ],
                Servicios = [],
                Equipos = []
            },
            activo: true);

        var actualizada = await servicio.ObtenerAsync(plantilla.Cabecera.Plantilla);
        Assert.Equal("Fumigación editada", actualizada!.Cabecera.DescripTrabajo);
        Assert.Single(actualizada.Detalles);
        Assert.Equal(77m, actualizada.Detalles[0].MaximoPermitido);
        Assert.Equal(30m, actualizada.Detalles[0].CostoTotal);
        Assert.Empty(actualizada.Servicios);
        Assert.Empty(actualizada.Equipos);
    }

    [Fact]
    public async Task Listar_FiltraPorActivoClienteYTexto()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        await servicio.CrearAsync(DatosBase("APLICACION NOCTURNA"));
        await servicio.CrearAsync(DatosBase("AREAS COMUNES") with { Cliente = "000092" });

        var todas = await servicio.ListarAsync(soloActivas: false);
        Assert.Equal(2, todas.Count);

        var activas = await servicio.ListarAsync(soloActivas: true);
        Assert.Equal(2, activas.Count);

        await servicio.DarDeBajaAsync(todas[0].Cabecera.Plantilla);
        Assert.Single(await servicio.ListarAsync(soloActivas: true));

        Assert.Single(await servicio.ListarAsync(cliente: "000092"));
        Assert.Single(await servicio.ListarAsync(texto: "NOCTURNA", soloActivas: false));
        Assert.Single(await servicio.ListarAsync(texto: "2"));
    }
}
