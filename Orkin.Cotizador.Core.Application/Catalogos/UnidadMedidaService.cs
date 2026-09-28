using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Catalogos;

/// <summary>
/// CRUD de unidades de medida. Primer catálogo maestro con el que se prueba el patrón de
/// administración (Fase 2).
/// </summary>
public sealed class UnidadMedidaService
{
    private readonly CotizadorDbContext _contexto;

    public UnidadMedidaService(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<UnidadMedida>> ListarAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        var query = _contexto.UnidadMedida.AsNoTracking();

        if (!incluirInactivos)
        {
            query = query.Where(u => u.IsActive);
        }

        return await query
            .OrderBy(u => u.Orden)
            .ThenBy(u => u.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<UnidadMedida?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _contexto.UnidadMedida
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<UnidadMedida> CrearAsync(
        string codigo,
        string nombre,
        string abreviatura,
        bool permiteDecimales,
        int orden,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(abreviatura);

        codigo = codigo.Trim().ToUpperInvariant();

        if (await _contexto.UnidadMedida.AnyAsync(u => u.Codigo == codigo, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe una unidad de medida con el código '{codigo}'.");
        }

        var entidad = new UnidadMedida
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Nombre = nombre.Trim(),
            Abreviatura = abreviatura.Trim(),
            PermiteDecimales = permiteDecimales,
            Orden = orden,
            OrigenDatoId = (byte)OrigenDatoCatalogoSeed.Manual,
            EsSembrado = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _contexto.UnidadMedida.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    public async Task<UnidadMedida> ActualizarAsync(
        Guid id,
        string nombre,
        string abreviatura,
        bool permiteDecimales,
        int orden,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(abreviatura);

        var entidad = await _contexto.UnidadMedida
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe la unidad de medida '{id}'.");

        entidad.Nombre = nombre.Trim();
        entidad.Abreviatura = abreviatura.Trim();
        entidad.PermiteDecimales = permiteDecimales;
        entidad.Orden = orden;
        entidad.IsActive = esActivo;
        entidad.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }
}

/// <summary>
/// Valores de siembra de <c>dbo.OrigenDatoCatalogo</c> que usan los servicios cuando crean un
/// registro manual. Estos ids deben coincidir con la semilla <c>dbo.OrigenDatoCatalogo.json</c>.
/// </summary>
public static class OrigenDatoCatalogoSeed
{
    public const int Manual = 1;
}
