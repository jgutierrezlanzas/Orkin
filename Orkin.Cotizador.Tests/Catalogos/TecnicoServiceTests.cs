using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Tests.Catalogos;

/// <summary>
/// El catálogo compartido de técnicos (D-21) es el que usa tanto el cotizador como los módulos
/// operativos. Estas pruebas cubren la promesa que hace el alta manual: un solo <c>Codigo</c>,
/// normalizado, sin duplicados —aunque el proveedor en memoria no aplica índices únicos, el
/// servicio los rechaza antes de escribir—.
/// </summary>
public sealed class TecnicoServiceTests
{
    private sealed class Laboratorio : IAsyncDisposable
    {
        private readonly string _nombre = $"tec-{Guid.NewGuid()}";

        public Laboratorio() => Contexto = Crear();

        public CotizadorDbContext Contexto { get; }

        private CotizadorDbContext Crear() =>
            new(new DbContextOptionsBuilder<CotizadorDbContext>().UseInMemoryDatabase(_nombre).Options);

        public async ValueTask DisposeAsync() => await Contexto.DisposeAsync();
    }

    private static TecnicoService Servicio(Laboratorio laboratorio) => new(laboratorio.Contexto);

    [Fact]
    public async Task Crear_NormalizaCodigoYMarcaOrigenManual()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);

        var tecnico = await servicio.CrearAsync(
            "  t-99  ", "  Técnico de prueba  ", " 555 ", " t@ejemplo.com ", " Supervisor ", 7);

        Assert.Equal("T-99", tecnico.Codigo);
        Assert.Equal("Técnico de prueba", tecnico.Nombre);
        Assert.Equal("555", tecnico.Telefono);
        Assert.Equal("t@ejemplo.com", tecnico.Correo);
        Assert.Equal("Supervisor", tecnico.Cargo);
        Assert.Equal(7, tecnico.Orden);
        Assert.True(tecnico.IsActive);
        Assert.False(tecnico.EsSembrado);
        Assert.Equal((byte)OrigenDatoCatalogoSeed.Manual, tecnico.OrigenDatoId);
        Assert.Equal(1, await laboratorio.Contexto.Tecnico.CountAsync());
    }

    [Fact]
    public async Task Crear_ConCodigoDuplicado_LanzaError()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);

        await servicio.CrearAsync("T-01", "Primero", null, null, null, 1);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearAsync("T-01", "Duplicado", null, null, null, 2));

        Assert.Contains("T-01", excepcion.Message);
        Assert.Equal(1, await laboratorio.Contexto.Tecnico.CountAsync());
    }

    [Fact]
    public async Task Crear_ConDistintaCapitalizacion_TambienEsDuplicado()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);

        await servicio.CrearAsync("t-01", "Primero", null, null, null, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearAsync("T-01", "Duplicado", null, null, null, 2));

        Assert.Equal(1, await laboratorio.Contexto.Tecnico.CountAsync());
    }

    [Fact]
    public async Task Listar_SoloActivos_PorDefecto()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);

        await servicio.CrearAsync("A", "Activo", null, null, null, 1);
        var inactivo = await servicio.CrearAsync("B", "Inactivo", null, null, null, 2);
        await servicio.ActualizarAsync(inactivo.Id, "Inactivo", null, null, null, 2, esActivo: false);

        var activos = await servicio.ListarAsync();
        var todos = await servicio.ListarAsync(incluirInactivos: true);

        Assert.Single(activos);
        Assert.Equal("A", activos[0].Codigo);
        Assert.Equal(2, todos.Count);
    }

    [Fact]
    public async Task Actualizar_PuedeDarDeBajaSinBorrarLaFila()
    {
        await using var laboratorio = new Laboratorio();
        var servicio = Servicio(laboratorio);

        var tecnico = await servicio.CrearAsync("C", "Original", "1", "a@x.com", "Técnico", 3);
        var actualizado = await servicio.ActualizarAsync(
            tecnico.Id, "Editado", "2", "b@x.com", "Supervisor", 9, esActivo: false);

        Assert.Equal(tecnico.Id, actualizado.Id);
        Assert.Equal("C", actualizado.Codigo);
        Assert.Equal("Editado", actualizado.Nombre);
        Assert.Equal("2", actualizado.Telefono);
        Assert.Equal("b@x.com", actualizado.Correo);
        Assert.Equal("Supervisor", actualizado.Cargo);
        Assert.Equal(9, actualizado.Orden);
        Assert.False(actualizado.IsActive);
        Assert.Equal(1, await laboratorio.Contexto.Tecnico.CountAsync());
    }
}
