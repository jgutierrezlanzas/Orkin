using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Catalogos;

/// <summary>
/// Catálogo compartido de técnicos. Lo carga el ERP y lo usan tanto el cotizador como los
/// módulos operativos que vienen del legacy (D-21): por eso hay un solo servicio y un solo Id.
/// </summary>
public sealed class TecnicoService
{
    private readonly CotizadorDbContext _contexto;

    public TecnicoService(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<Tecnico>> ListarAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        var query = _contexto.Tecnico.AsNoTracking();

        if (!incluirInactivos)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tecnico?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _contexto.Tecnico
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<Tecnico> CrearAsync(
        string codigo,
        string nombre,
        string? telefono,
        string? correo,
        string? cargo,
        int orden,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        codigo = codigo.Trim().ToUpperInvariant();

        if (await _contexto.Tecnico.AnyAsync(t => t.Codigo == codigo, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe un técnico con el código '{codigo}'.");
        }

        var entidad = new Tecnico
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Nombre = nombre.Trim(),
            Telefono = telefono?.Trim(),
            Correo = correo?.Trim(),
            Cargo = cargo?.Trim(),
            Orden = orden,
            OrigenDatoId = (byte)OrigenDatoCatalogoSeed.Manual,
            EsSembrado = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _contexto.Tecnico.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    public async Task<Tecnico> ActualizarAsync(
        Guid id,
        string nombre,
        string? telefono,
        string? correo,
        string? cargo,
        int orden,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var entidad = await _contexto.Tecnico
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe el técnico '{id}'.");

        entidad.Nombre = nombre.Trim();
        entidad.Telefono = telefono?.Trim();
        entidad.Correo = correo?.Trim();
        entidad.Cargo = cargo?.Trim();
        entidad.Orden = orden;
        entidad.IsActive = esActivo;
        entidad.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }
}
