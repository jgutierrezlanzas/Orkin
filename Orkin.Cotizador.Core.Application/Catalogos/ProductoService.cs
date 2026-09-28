using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Catalogos;

/// <summary>
/// CRUD de productos (insumos consumibles). Es el catálogo que alimenta las líneas de cotización.
/// </summary>
public sealed class ProductoService
{
    private readonly CotizadorDbContext _contexto;

    public ProductoService(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<Producto>> ListarAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Producto> query = _contexto.Producto
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Include(p => p.UnidadMedida);

        if (!incluirInactivos)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .OrderBy(p => p.Codigo)
            .ToListAsync(cancellationToken);
    }

    public async Task<Producto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _contexto.Producto
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Include(p => p.UnidadMedida)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Producto> CrearAsync(
        string codigo,
        string nombre,
        string? descripcion,
        Guid? categoriaId,
        Guid unidadMedidaId,
        decimal costoActual,
        decimal precioVentaActual,
        int? vidaUtilMeses,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        if (costoActual < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costoActual), "El costo no puede ser negativo.");
        }

        if (precioVentaActual < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precioVentaActual), "El precio de venta no puede ser negativo.");
        }

        codigo = codigo.Trim().ToUpperInvariant();

        if (await _contexto.Producto.AnyAsync(p => p.Codigo == codigo, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe un producto con el código '{codigo}'.");
        }

        if (!await _contexto.UnidadMedida.AnyAsync(u => u.Id == unidadMedidaId, cancellationToken))
        {
            throw new InvalidOperationException("La unidad de medida no existe.");
        }

        if (categoriaId.HasValue && !await _contexto.CategoriaProducto.AnyAsync(c => c.Id == categoriaId.Value, cancellationToken))
        {
            throw new InvalidOperationException("La categoría no existe.");
        }

        var entidad = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim(),
            CategoriaId = categoriaId,
            UnidadMedidaId = unidadMedidaId,
            CostoActual = costoActual,
            PrecioVentaActual = precioVentaActual,
            VidaUtilMeses = vidaUtilMeses,
            OrigenPrecioId = (byte)OrigenPrecioCatalogoSeed.Manual,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _contexto.Producto.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    public async Task<Producto> ActualizarAsync(
        Guid id,
        string nombre,
        string? descripcion,
        Guid? categoriaId,
        Guid unidadMedidaId,
        decimal costoActual,
        decimal precioVentaActual,
        int? vidaUtilMeses,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        if (costoActual < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costoActual), "El costo no puede ser negativo.");
        }

        if (precioVentaActual < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precioVentaActual), "El precio de venta no puede ser negativo.");
        }

        var entidad = await _contexto.Producto
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe el producto '{id}'.");

        if (!await _contexto.UnidadMedida.AnyAsync(u => u.Id == unidadMedidaId, cancellationToken))
        {
            throw new InvalidOperationException("La unidad de medida no existe.");
        }

        if (categoriaId.HasValue && !await _contexto.CategoriaProducto.AnyAsync(c => c.Id == categoriaId.Value, cancellationToken))
        {
            throw new InvalidOperationException("La categoría no existe.");
        }

        entidad.Nombre = nombre.Trim();
        entidad.Descripcion = descripcion?.Trim();
        entidad.CategoriaId = categoriaId;
        entidad.UnidadMedidaId = unidadMedidaId;
        entidad.CostoActual = costoActual;
        entidad.PrecioVentaActual = precioVentaActual;
        entidad.VidaUtilMeses = vidaUtilMeses;
        entidad.IsActive = esActivo;
        entidad.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }
}

internal static class OrigenPrecioCatalogoSeed
{
    public const int Manual = 1;
}
