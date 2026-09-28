using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Catalogos;

/// <summary>
/// CRUD de tipos de servicio.
/// </summary>
public sealed class TipoServicioService
{
    private readonly CotizadorDbContext _contexto;

    public TipoServicioService(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<TipoServicio>> ListarAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TipoServicio> query = _contexto.TipoServicio
            .AsNoTracking()
            .Include(t => t.UnidadMedida);

        if (!incluirInactivos)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<TipoServicio?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _contexto.TipoServicio
            .AsNoTracking()
            .Include(t => t.UnidadMedida)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<TipoServicio> CrearAsync(
        string codigo,
        string nombre,
        string? descripcion,
        Guid? unidadMedidaId,
        bool esResidencialPorDefecto,
        bool esEmpresarialPorDefecto,
        int orden,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        codigo = codigo.Trim().ToUpperInvariant();

        if (await _contexto.TipoServicio.AnyAsync(t => t.Codigo == codigo, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe un tipo de servicio con el código '{codigo}'.");
        }

        var entidad = new TipoServicio
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim(),
            UnidadMedidaId = unidadMedidaId,
            EsResidencialPorDefecto = esResidencialPorDefecto,
            EsEmpresarialPorDefecto = esEmpresarialPorDefecto,
            Orden = orden,
            OrigenDatoId = (byte)OrigenDatoCatalogoSeed.Manual,
            EsSembrado = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _contexto.TipoServicio.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    public async Task<TipoServicio> ActualizarAsync(
        Guid id,
        string nombre,
        string? descripcion,
        Guid? unidadMedidaId,
        bool esResidencialPorDefecto,
        bool esEmpresarialPorDefecto,
        int orden,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var entidad = await _contexto.TipoServicio
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe el tipo de servicio '{id}'.");

        entidad.Nombre = nombre.Trim();
        entidad.Descripcion = descripcion?.Trim();
        entidad.UnidadMedidaId = unidadMedidaId;
        entidad.EsResidencialPorDefecto = esResidencialPorDefecto;
        entidad.EsEmpresarialPorDefecto = esEmpresarialPorDefecto;
        entidad.Orden = orden;
        entidad.IsActive = esActivo;
        entidad.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }
}
