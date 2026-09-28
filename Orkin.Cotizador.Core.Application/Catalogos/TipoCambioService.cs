using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Catalogos;

/// <summary>
/// CRUD de tipos de cambio. El motor usa el último valor con fecha ≤ a la cotización.
/// </summary>
public sealed class TipoCambioService
{
    private readonly CotizadorDbContext _contexto;

    public TipoCambioService(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<TipoCambio>> ListarAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TipoCambio> query = _contexto.TipoCambio
            .AsNoTracking()
            .Include(t => t.Fuente);

        if (!incluirInactivos)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query
            .OrderByDescending(t => t.Fecha)
            .ToListAsync(cancellationToken);
    }

    public async Task<TipoCambio?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _contexto.TipoCambio
            .AsNoTracking()
            .Include(t => t.Fuente)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<TipoCambio> CrearAsync(
        DateOnly fecha,
        decimal valor,
        byte fuenteId,
        CancellationToken cancellationToken = default)
    {
        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "El tipo de cambio debe ser mayor que cero.");
        }

        if (!await _contexto.FuenteTipoCambioCatalogo.AnyAsync(f => f.Id == fuenteId, cancellationToken))
        {
            throw new InvalidOperationException("La fuente de tipo de cambio no existe.");
        }

        if (await _contexto.TipoCambio.AnyAsync(t => t.Fecha == fecha, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe un tipo de cambio para la fecha {fecha:yyyy-MM-dd}.");
        }

        var entidad = new TipoCambio
        {
            Id = Guid.NewGuid(),
            Fecha = fecha,
            Valor = valor,
            FuenteId = fuenteId,
            OrigenDatoId = (byte)OrigenDatoCatalogoSeed.Manual,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _contexto.TipoCambio.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    public async Task<TipoCambio> ActualizarAsync(
        Guid id,
        DateOnly fecha,
        decimal valor,
        byte fuenteId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "El tipo de cambio debe ser mayor que cero.");
        }

        var entidad = await _contexto.TipoCambio
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe el tipo de cambio '{id}'.");

        if (await _contexto.TipoCambio.AnyAsync(t => t.Fecha == fecha && t.Id != id, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe un tipo de cambio para la fecha {fecha:yyyy-MM-dd}.");
        }

        if (!await _contexto.FuenteTipoCambioCatalogo.AnyAsync(f => f.Id == fuenteId, cancellationToken))
        {
            throw new InvalidOperationException("La fuente de tipo de cambio no existe.");
        }

        entidad.Fecha = fecha;
        entidad.Valor = valor;
        entidad.FuenteId = fuenteId;
        entidad.IsActive = esActivo;
        entidad.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    /// <summary>
    /// Obtiene el último tipo de cambio con fecha ≤ a la dada (para la cotización).
    /// </summary>
    public async Task<TipoCambio?> ObtenerVigenteAsync(
        DateOnly fecha,
        CancellationToken cancellationToken = default)
    {
        return await _contexto.TipoCambio
            .AsNoTracking()
            .Include(t => t.Fuente)
            .Where(t => t.IsActive && t.Fecha <= fecha)
            .OrderByDescending(t => t.Fecha)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Obtiene el tipo de cambio vigente y devuelve sus datos esenciales como tupla.
    /// </summary>
    public async Task<(Guid Id, decimal Valor, DateOnly Fecha)> ObtenerAsync(
        DateOnly fecha,
        CancellationToken cancellationToken = default)
    {
        var tc = await ObtenerVigenteAsync(fecha, cancellationToken)
            ?? throw new InvalidOperationException($"No hay tipo de cambio vigente para {fecha:yyyy-MM-dd}.");
        return (tc.Id, tc.Valor, tc.Fecha);
    }
}