using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Catalogos;

/// <summary>
/// CRUD de categorías de producto.
/// </summary>
public sealed class CategoriaProductoService
{
    private readonly CotizadorDbContext _contexto;

    public CategoriaProductoService(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<CategoriaProducto>> ListarAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        var query = _contexto.CategoriaProducto.AsNoTracking();

        if (!incluirInactivos)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoriaProducto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _contexto.CategoriaProducto
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<CategoriaProducto> CrearAsync(
        string codigo,
        string nombre,
        string? descripcion,
        int orden,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        codigo = codigo.Trim().ToUpperInvariant();

        if (await _contexto.CategoriaProducto.AnyAsync(c => c.Codigo == codigo, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe una categoría con el código '{codigo}'.");
        }

        var entidad = new CategoriaProducto
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim(),
            Orden = orden,
            OrigenDatoId = (byte)OrigenDatoCatalogoSeed.Manual,
            EsSembrado = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _contexto.CategoriaProducto.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    public async Task<CategoriaProducto> ActualizarAsync(
        Guid id,
        string nombre,
        string? descripcion,
        int orden,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var entidad = await _contexto.CategoriaProducto
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe la categoría '{id}'.");

        entidad.Nombre = nombre.Trim();
        entidad.Descripcion = descripcion?.Trim();
        entidad.Orden = orden;
        entidad.IsActive = esActivo;
        entidad.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }
}
