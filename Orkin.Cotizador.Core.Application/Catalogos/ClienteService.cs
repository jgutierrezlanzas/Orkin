using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Catalogos;

/// <summary>
/// CRUD de clientes. Todo cliente que recibe una cotización vive acá.
/// </summary>
public sealed class ClienteService
{
    private readonly CotizadorDbContext _contexto;

    public ClienteService(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<Cliente>> ListarAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Cliente> query = _contexto.Cliente
            .AsNoTracking()
            .Include(c => c.TipoCliente);

        if (!incluirInactivos)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _contexto.Cliente
            .AsNoTracking()
            .Include(c => c.TipoCliente)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Cliente> CrearAsync(
        string codigo,
        string nombre,
        string? razonSocial,
        byte tipoClienteId,
        string? cedulaORuc,
        string? identificacionFiscal,
        string? direccion,
        string? ciudad,
        string? departamento,
        string? telefono,
        string? correo,
        string? contactoNombre,
        string? contactoCargo,
        string? sitioWeb,
        string? notas,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        codigo = codigo.Trim().ToUpperInvariant();

        if (await _contexto.Cliente.AnyAsync(c => c.Codigo == codigo, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe un cliente con el código '{codigo}'.");
        }

        if (!await _contexto.TipoClienteCatalogo.AnyAsync(t => t.Id == tipoClienteId, cancellationToken))
        {
            throw new InvalidOperationException("El tipo de cliente no existe.");
        }

        var entidad = new Cliente
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Nombre = nombre.Trim(),
            RazonSocial = razonSocial?.Trim(),
            TipoClienteId = tipoClienteId,
            CedulaORuc = cedulaORuc?.Trim(),
            IdentificacionFiscal = identificacionFiscal?.Trim(),
            Direccion = direccion?.Trim(),
            Ciudad = ciudad?.Trim(),
            Departamento = departamento?.Trim(),
            Telefono = telefono?.Trim(),
            Correo = correo?.Trim(),
            ContactoNombre = contactoNombre?.Trim(),
            ContactoCargo = contactoCargo?.Trim(),
            SitioWeb = sitioWeb?.Trim(),
            Notas = notas?.Trim(),
            OrigenDatoId = (byte)OrigenDatoCatalogoSeed.Manual,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _contexto.Cliente.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    public async Task<Cliente> ActualizarAsync(
        Guid id,
        string nombre,
        string? razonSocial,
        byte tipoClienteId,
        string? cedulaORuc,
        string? identificacionFiscal,
        string? direccion,
        string? ciudad,
        string? departamento,
        string? telefono,
        string? correo,
        string? contactoNombre,
        string? contactoCargo,
        string? sitioWeb,
        string? notas,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var entidad = await _contexto.Cliente
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe el cliente '{id}'.");

        if (!await _contexto.TipoClienteCatalogo.AnyAsync(t => t.Id == tipoClienteId, cancellationToken))
        {
            throw new InvalidOperationException("El tipo de cliente no existe.");
        }

        entidad.Nombre = nombre.Trim();
        entidad.RazonSocial = razonSocial?.Trim();
        entidad.TipoClienteId = tipoClienteId;
        entidad.CedulaORuc = cedulaORuc?.Trim();
        entidad.IdentificacionFiscal = identificacionFiscal?.Trim();
        entidad.Direccion = direccion?.Trim();
        entidad.Ciudad = ciudad?.Trim();
        entidad.Departamento = departamento?.Trim();
        entidad.Telefono = telefono?.Trim();
        entidad.Correo = correo?.Trim();
        entidad.ContactoNombre = contactoNombre?.Trim();
        entidad.ContactoCargo = contactoCargo?.Trim();
        entidad.SitioWeb = sitioWeb?.Trim();
        entidad.Notas = notas?.Trim();
        entidad.IsActive = esActivo;
        entidad.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }
}
