using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Plantillas;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Plantillas;

/// <summary>
/// Fase 2. Las dos promesas que más importan de una entidad compuesta: que la copia conserve
/// TODO el detalle (el legacy perdía <c>MAXIMO_PERMITIDO</c>) y que la baja sea lógica.
/// </summary>
public sealed class PlantillaServiceTests
{
    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"pl-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = Crear();

        public CotizadorDbContext Contexto { get; }

        /// <summary>Un request HTTP real usa un scope nuevo: el test también, para no arrastrar
        /// entidades rastreadas por la creación y simular objetos desconectados del DTO.</summary>
        public CotizadorDbContext Relectura() => Crear();

        private CotizadorDbContext Crear() =>
            new(new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public async ValueTask DisposeAsync() => await Contexto.DisposeAsync();
    }

    private static PlantillaService Servicio(Laboratorio laboratorio) => new(laboratorio.Contexto);

    private static async Task<Plantilla> CrearBaseAsync(PlantillaService servicio)
        => await servicio.CrearAsync(
            "  PL-01  ", "  Fumigación básica  ", null, "LOC-1", "Calle 1", null,
            "Contacto", "555", "VEH-1", "Trabajo", "Notas",
            [
                new PlantillaDetalle
                {
                    TipoPlagaCodigo = "PLAGA-1",
                    DescripcionArticulo = "Producto A",
                    Cantidad = 2m,
                    CostoUnitario = 10m,
                    CostoTotal = 20m,
                    MaximoPermitido = 99m
                },
                new PlantillaDetalle
                {
                    TipoPlagaCodigo = "PLAGA-2",
                    DescripcionArticulo = "Producto B",
                    Cantidad = 1m,
                    CostoUnitario = 5m,
                    CostoTotal = 5m,
                    MaximoPermitido = 50m
                }
            ],
            [new PlantillaServicio { FrecuenciaCodigo = "MENSUAL" }],
            [new PlantillaEquipo { EquipoCodigo = "EQUIPO-1", Utilizado = true }]);

    [Fact]
    public async Task Crear_NormalizaCodigoYRenumerarDetalles()
    {
        await using var laboratorio = new Laboratorio();
        var plantilla = await CrearBaseAsync(Servicio(laboratorio));

        Assert.Equal("PL-01", plantilla.Codigo);
        Assert.Equal("Fumigación básica", plantilla.Nombre);
        Assert.True(plantilla.IsActive);
        Assert.Equal([1, 2], plantilla.Detalles.Select(d => d.Consecutivo).ToArray());
        Assert.Equal("MENSUAL", Assert.Single(plantilla.Servicios).FrecuenciaCodigo);
        Assert.True(Assert.Single(plantilla.Equipos).Utilizado);
    }

    [Fact]
    public async Task Crear_ConCodigoDuplicado_LanzaError()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        await CrearBaseAsync(servicio);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CrearAsync(
            "pl-01", "Otra", null, null, null, null, null, null, null, null, null, [], [], []));

        Assert.Equal(1, await laboratorio.Contexto.Plantilla.CountAsync());
    }

    [Fact]
    public async Task Copiar_ConservaMaximoPermitidoYRenumerarDesdeUno()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        var original = await CrearBaseAsync(servicio);

        var copia = await servicio.CopiarAsync(original.Id);

        Assert.NotEqual(original.Id, copia.Id);
        Assert.Equal("PL-01-COPIA", copia.Codigo);
        Assert.Equal("Fumigación básica (copia)", copia.Nombre);
        Assert.True(copia.IsActive);

        Assert.Equal(original.Detalles.Count, copia.Detalles.Count);
        Assert.Equal([1, 2], copia.Detalles.Select(d => d.Consecutivo).ToArray());
        // El defecto que se corrige respecto del copiador del legacy.
        Assert.Equal([99m, 50m], copia.Detalles.OrderBy(d => d.Consecutivo).Select(d => d.MaximoPermitido).ToArray());
        Assert.Equal(original.Detalles.OrderBy(d => d.Consecutivo).Select(d => d.TipoPlagaCodigo),
            copia.Detalles.OrderBy(d => d.Consecutivo).Select(d => d.TipoPlagaCodigo));

        Assert.Single(copia.Servicios);
        Assert.Equal("MENSUAL", copia.Servicios.Single().FrecuenciaCodigo);
        Assert.Single(copia.Equipos);
        Assert.Equal("EQUIPO-1", copia.Equipos.Single().EquipoCodigo);
        Assert.Equal(2, await laboratorio.Contexto.Plantilla.CountAsync());
    }

    [Fact]
    public async Task Copiar_DosVeces_GeneraCodigosDistintos()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        var original = await CrearBaseAsync(servicio);

        var primera = await servicio.CopiarAsync(original.Id);
        var segunda = await servicio.CopiarAsync(original.Id);

        Assert.Equal("PL-01-COPIA", primera.Codigo);
        Assert.Equal("PL-01-COPIA-2", segunda.Codigo);
    }

    [Fact]
    public async Task DarDeBaja_NoBorraLaFilaNiSusLineas()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        var plantilla = await CrearBaseAsync(servicio);

        await servicio.DarDeBajaAsync(plantilla.Id);

        var guardada = await laboratorio.Contexto.Plantilla
            .Include(p => p.Detalles)
            .SingleAsync(p => p.Id == plantilla.Id);
        Assert.False(guardada.IsActive);
        Assert.Equal(2, guardada.Detalles.Count);
    }

    [Fact]
    public async Task Actualizar_ReconciliaLineasPorId()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);
        var plantilla = await CrearBaseAsync(servicio);
        var lineas = plantilla.Detalles.OrderBy(d => d.Consecutivo).ToList();

        await using var contextoNuevo = laboratorio.Relectura();
        await new PlantillaService(contextoNuevo).ActualizarAsync(
            plantilla.Id,
            "Fumigación editada",
            null, null, null, null, null, null, null, null, null,
            esActivo: true,
            detalles:
            [
                new PlantillaDetalle
                {
                    Id = lineas[0].Id,
                    DescripcionArticulo = "Producto A editado",
                    Cantidad = 3m,
                    CostoUnitario = 10m,
                    CostoTotal = 30m,
                    MaximoPermitido = 77m
                },
                new PlantillaDetalle
                {
                    DescripcionArticulo = "Producto nuevo",
                    Cantidad = 1m,
                    MaximoPermitido = 10m
                }
            ],
            servicios: [],
            equipos: []);

        var actualizada = await servicio.ObtenerPorIdAsync(plantilla.Id);
        Assert.Equal("Fumigación editada", actualizada!.Nombre);
        Assert.Equal(2, actualizada.Detalles.Count);
        Assert.Contains(actualizada.Detalles, d => d.Id == lineas[0].Id && d.MaximoPermitido == 77m);
        Assert.DoesNotContain(actualizada.Detalles, d => d.Id == lineas[1].Id);
        Assert.Contains(actualizada.Detalles, d => d.DescripcionArticulo == "Producto nuevo");
        Assert.Empty(actualizada.Servicios);
        Assert.Empty(actualizada.Equipos);
    }
}
