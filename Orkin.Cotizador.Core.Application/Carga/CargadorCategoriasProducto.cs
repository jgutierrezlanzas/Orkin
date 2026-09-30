using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Carga las familias de producto (las "categorías" nuestras) desde el ERP del cliente — que en
/// el demo son sus grupos. La descripción es opcional: el nombre es lo que se elige en pantalla.
/// </summary>
public sealed class CargadorCategoriasProducto : CargadorCatalogoMaestro<CategoriaProducto>
{
    public CargadorCategoriasProducto(CotizadorDbContext contexto) : base(contexto)
    {
    }

    public override string Catalogo => "CATEGORIAS_PRODUCTO";

    protected override DbSet<CategoriaProducto> Set() => Contexto.CategoriaProducto;

    protected override IReadOnlyList<CampoEsperado> CamposExtras() =>
    [
        new("Descripcion", "texto", false, "Descripción de la categoría. Opcional."),
    ];

    protected override CategoriaProducto CrearEntidad() => new();

    protected override Task AplicarExtrasAsync(
        CategoriaProducto entidad, Dictionary<string, object?> mapeada, CancellationToken ct)
    {
        entidad.Descripcion = Normalizacion.AString(mapeada.GetValueOrDefault("Descripcion"));
        return Task.CompletedTask;
    }

    protected override Task<List<CampoSimulado>> DiferenciaExtrasAsync(
        CategoriaProducto existente, Dictionary<string, object?> mapeada, CancellationToken ct)
    {
        var cambios = new List<CampoSimulado>();

        var descripcion = Normalizacion.AString(mapeada.GetValueOrDefault("Descripcion"));
        if (existente.Descripcion != descripcion)
        {
            cambios.Add(new CampoSimulado("Descripción", existente.Descripcion, descripcion));
        }

        return Task.FromResult(cambios);
    }
}
