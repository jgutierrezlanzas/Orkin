using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Plantillas;

/// <summary>
/// Catálogo compuesto de plantillas de orden de trabajo (Fase 2). Una sola entidad para la
/// plataforma: la usará la orden de trabajo y la agenda de visitas sin duplicar plantillas por
/// módulo (D-21).
/// <para>
/// Las líneas se reconcilian por <c>Id</c>: las que vienen con Id se actualizan, las que no se
/// agregan y las que ya no están se quitan de la colección. El borrado lógico es solo de la
/// cabecera (<see cref="Plantilla.IsActive"/>); las líneas viven mientras viva su plantilla.
/// </para>
/// </summary>
public sealed class PlantillaService
{
    private readonly CotizadorDbContext _contexto;

    public PlantillaService(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    private IQueryable<Plantilla> ConsultaCompleta() => _contexto.Plantilla
        .Include(p => p.Cliente)
        .Include(p => p.Tecnico)
        .Include(p => p.Detalles).ThenInclude(d => d.Producto)
        .Include(p => p.Detalles).ThenInclude(d => d.UnidadMedida)
        .Include(p => p.Servicios).ThenInclude(s => s.TipoServicio)
        .Include(p => p.Equipos);

    public async Task<IReadOnlyList<Plantilla>> ListarAsync(
        string? texto = null,
        Guid? clienteId = null,
        Guid? tecnicoId = null,
        bool soloActivas = true,
        CancellationToken cancellationToken = default)
    {
        var query = ConsultaCompleta().AsNoTracking();

        if (soloActivas)
        {
            query = query.Where(p => p.IsActive);
        }

        if (clienteId is { } idCliente)
        {
            query = query.Where(p => p.ClienteId == idCliente);
        }

        if (tecnicoId is { } idTecnico)
        {
            query = query.Where(p => p.TecnicoId == idTecnico);
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            query = query.Where(p =>
                EF.Functions.Like(p.Codigo, patron) || EF.Functions.Like(p.Nombre, patron));
        }

        return await query
            .OrderBy(p => p.Nombre)
            .ThenBy(p => p.Codigo)
            .ToListAsync(cancellationToken);
    }

    public async Task<Plantilla?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await ConsultaCompleta().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Plantilla> CrearAsync(
        string codigo,
        string nombre,
        Guid? clienteId,
        string? locacionCodigo,
        string? direccion,
        Guid? tecnicoId,
        string? contacto,
        string? telefono,
        string? vehiculoCodigo,
        string? descripcionTrabajo,
        string? notas,
        IReadOnlyList<PlantillaDetalle> detalles,
        IReadOnlyList<PlantillaServicio> servicios,
        IReadOnlyList<PlantillaEquipo> equipos,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        codigo = codigo.Trim().ToUpperInvariant();

        if (await _contexto.Plantilla.AnyAsync(p => p.Codigo == codigo, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe una plantilla con el código '{codigo}'.");
        }

        var entidad = new Plantilla
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Nombre = nombre.Trim(),
            ClienteId = clienteId,
            LocacionCodigo = locacionCodigo?.Trim(),
            Direccion = direccion?.Trim(),
            TecnicoId = tecnicoId,
            Contacto = contacto?.Trim(),
            Telefono = telefono?.Trim(),
            VehiculoCodigo = vehiculoCodigo?.Trim(),
            DescripcionTrabajo = descripcionTrabajo?.Trim(),
            Notas = notas?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Renumerar(detalles, servicios, equipos);
        entidad.Detalles = detalles.ToList();
        entidad.Servicios = servicios.ToList();
        entidad.Equipos = equipos.ToList();

        _contexto.Plantilla.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    public async Task<Plantilla> ActualizarAsync(
        Guid id,
        string nombre,
        Guid? clienteId,
        string? locacionCodigo,
        string? direccion,
        Guid? tecnicoId,
        string? contacto,
        string? telefono,
        string? vehiculoCodigo,
        string? descripcionTrabajo,
        string? notas,
        bool esActivo,
        IReadOnlyList<PlantillaDetalle> detalles,
        IReadOnlyList<PlantillaServicio> servicios,
        IReadOnlyList<PlantillaEquipo> equipos,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var entidad = await ConsultaCompleta()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe la plantilla '{id}'.");

        entidad.Nombre = nombre.Trim();
        entidad.ClienteId = clienteId;
        entidad.LocacionCodigo = locacionCodigo?.Trim();
        entidad.Direccion = direccion?.Trim();
        entidad.TecnicoId = tecnicoId;
        entidad.Contacto = contacto?.Trim();
        entidad.Telefono = telefono?.Trim();
        entidad.VehiculoCodigo = vehiculoCodigo?.Trim();
        entidad.DescripcionTrabajo = descripcionTrabajo?.Trim();
        entidad.Notas = notas?.Trim();
        entidad.IsActive = esActivo;
        entidad.UpdatedAt = DateTime.UtcNow;

        Renumerar(detalles, servicios, equipos);

        Reconciliar(entidad.Detalles, detalles, d => d.Id, (destino, origen) =>
        {
            destino.Consecutivo = origen.Consecutivo;
            destino.ProductoId = origen.ProductoId;
            destino.DescripcionArticulo = origen.DescripcionArticulo;
            destino.TipoPlagaCodigo = origen.TipoPlagaCodigo;
            destino.LugarTratarCodigo = origen.LugarTratarCodigo;
            destino.Area = origen.Area;
            destino.Concentracion = origen.Concentracion;
            destino.DosificacionCodigo = origen.DosificacionCodigo;
            destino.MetodoAplicacionCodigo = origen.MetodoAplicacionCodigo;
            destino.Cantidad = origen.Cantidad;
            destino.UnidadMedidaId = origen.UnidadMedidaId;
            destino.CostoUnitario = origen.CostoUnitario;
            destino.CostoTotal = origen.CostoTotal;
            destino.MaximoPermitido = origen.MaximoPermitido;
            destino.UpdatedAt = DateTime.UtcNow;
        }, origen => new PlantillaDetalle
        {
            // Id sin asignar: la clave es ValueGeneratedOnAdd. Si se le pusiera un Guid aquí,
            // EF lo tomaría como entidad existente (Modified) e intentaría un UPDATE inexistente.
            PlantillaId = id,
            Consecutivo = origen.Consecutivo,
            ProductoId = origen.ProductoId,
            DescripcionArticulo = origen.DescripcionArticulo,
            TipoPlagaCodigo = origen.TipoPlagaCodigo,
            LugarTratarCodigo = origen.LugarTratarCodigo,
            Area = origen.Area,
            Concentracion = origen.Concentracion,
            DosificacionCodigo = origen.DosificacionCodigo,
            MetodoAplicacionCodigo = origen.MetodoAplicacionCodigo,
            Cantidad = origen.Cantidad,
            UnidadMedidaId = origen.UnidadMedidaId,
            CostoUnitario = origen.CostoUnitario,
            CostoTotal = origen.CostoTotal,
            MaximoPermitido = origen.MaximoPermitido,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        Reconciliar(entidad.Servicios, servicios, s => s.Id, (destino, origen) =>
        {
            destino.Consecutivo = origen.Consecutivo;
            destino.TipoServicioId = origen.TipoServicioId;
            destino.FrecuenciaCodigo = origen.FrecuenciaCodigo;
            destino.UpdatedAt = DateTime.UtcNow;
        }, origen => new PlantillaServicio
        {
            PlantillaId = id,
            Consecutivo = origen.Consecutivo,
            TipoServicioId = origen.TipoServicioId,
            FrecuenciaCodigo = origen.FrecuenciaCodigo,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        Reconciliar(entidad.Equipos, equipos, e => e.Id, (destino, origen) =>
        {
            destino.Consecutivo = origen.Consecutivo;
            destino.EquipoCodigo = origen.EquipoCodigo;
            destino.Utilizado = origen.Utilizado;
            destino.UpdatedAt = DateTime.UtcNow;
        }, origen => new PlantillaEquipo
        {
            PlantillaId = id,
            Consecutivo = origen.Consecutivo,
            EquipoCodigo = origen.EquipoCodigo,
            Utilizado = origen.Utilizado,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _contexto.SaveChangesAsync(cancellationToken);

        return entidad;
    }

    /// <summary>
    /// Copia una plantilla: cabecera, productos, servicios y equipos. Corrige el defecto del
    /// copiador legacy, que omitía <c>MAXIMO_PERMITIDO</c> y dejaba la copia con 0.
    /// </summary>
    public async Task<Plantilla> CopiarAsync(
        Guid id,
        string? codigo = null,
        string? nombre = null,
        CancellationToken cancellationToken = default)
    {
        var origen = await ConsultaCompleta()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe la plantilla '{id}'.");

        var codigoCopia = await CodigoCopiaDisponibleAsync(
            string.IsNullOrWhiteSpace(codigo) ? origen.Codigo : codigo, cancellationToken);

        var ahora = DateTime.UtcNow;
        var copia = new Plantilla
        {
            Id = Guid.NewGuid(),
            Codigo = codigoCopia,
            Nombre = string.IsNullOrWhiteSpace(nombre) ? $"{origen.Nombre} (copia)" : nombre.Trim(),
            ClienteId = origen.ClienteId,
            LocacionCodigo = origen.LocacionCodigo,
            Direccion = origen.Direccion,
            TecnicoId = origen.TecnicoId,
            Contacto = origen.Contacto,
            Telefono = origen.Telefono,
            VehiculoCodigo = origen.VehiculoCodigo,
            DescripcionTrabajo = origen.DescripcionTrabajo,
            Notas = origen.Notas,
            IsActive = true,
            CreatedAt = ahora,
            UpdatedAt = ahora,
            Detalles = origen.Detalles.OrderBy(d => d.Consecutivo).Select((d, i) => new PlantillaDetalle
            {
                Id = Guid.NewGuid(),
                Consecutivo = i + 1,
                ProductoId = d.ProductoId,
                DescripcionArticulo = d.DescripcionArticulo,
                TipoPlagaCodigo = d.TipoPlagaCodigo,
                LugarTratarCodigo = d.LugarTratarCodigo,
                Area = d.Area,
                Concentracion = d.Concentracion,
                DosificacionCodigo = d.DosificacionCodigo,
                MetodoAplicacionCodigo = d.MetodoAplicacionCodigo,
                Cantidad = d.Cantidad,
                UnidadMedidaId = d.UnidadMedidaId,
                CostoUnitario = d.CostoUnitario,
                CostoTotal = d.CostoTotal,
                // El legacy perdía este valor al copiar. Acá se conserva siempre.
                MaximoPermitido = d.MaximoPermitido,
                CreatedAt = ahora,
                UpdatedAt = ahora
            }).ToList(),
            Servicios = origen.Servicios.OrderBy(s => s.Consecutivo).Select((s, i) => new PlantillaServicio
            {
                Id = Guid.NewGuid(),
                Consecutivo = i + 1,
                TipoServicioId = s.TipoServicioId,
                FrecuenciaCodigo = s.FrecuenciaCodigo,
                CreatedAt = ahora,
                UpdatedAt = ahora
            }).ToList(),
            Equipos = origen.Equipos.OrderBy(e => e.Consecutivo).Select((e, i) => new PlantillaEquipo
            {
                Id = Guid.NewGuid(),
                Consecutivo = i + 1,
                EquipoCodigo = e.EquipoCodigo,
                Utilizado = e.Utilizado,
                CreatedAt = ahora,
                UpdatedAt = ahora
            }).ToList()
        };

        _contexto.Plantilla.Add(copia);
        await _contexto.SaveChangesAsync(cancellationToken);

        return copia;
    }

    /// <summary>Baja lógica: la plantilla deja de aparecer pero sus líneas se conservan.</summary>
    public async Task DarDeBajaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entidad = await _contexto.Plantilla
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"No existe la plantilla '{id}'.");

        entidad.IsActive = false;
        entidad.UpdatedAt = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> CodigoCopiaDisponibleAsync(string baseCodigo, CancellationToken ct)
    {
        var prefijo = baseCodigo.Trim().ToUpperInvariant();
        var candidato = $"{prefijo}-COPIA";
        var sufijo = 1;

        while (await _contexto.Plantilla.AnyAsync(p => p.Codigo == candidato, ct))
        {
            candidato = $"{prefijo}-COPIA-{++sufijo}";
        }

        return candidato;
    }

    private static void Renumerar(
        IReadOnlyList<PlantillaDetalle> detalles,
        IReadOnlyList<PlantillaServicio> servicios,
        IReadOnlyList<PlantillaEquipo> equipos)
    {
        for (var i = 0; i < detalles.Count; i++)
        {
            detalles[i].Consecutivo = i + 1;
        }

        for (var i = 0; i < servicios.Count; i++)
        {
            servicios[i].Consecutivo = i + 1;
        }

        for (var i = 0; i < equipos.Count; i++)
        {
            equipos[i].Consecutivo = i + 1;
        }
    }

    private void Reconciliar<T>(
        ICollection<T> destino,
        IReadOnlyList<T> origen,
        Func<T, Guid> clave,
        Action<T, T> copiar,
        Func<T, T> crear) where T : class
    {
        var actuales = destino.ToDictionary(clave);
        var deseados = new HashSet<Guid>();

        foreach (var entrada in origen)
        {
            var id = clave(entrada);
            if (id != Guid.Empty && actuales.TryGetValue(id, out var existente))
            {
                copiar(existente, entrada);
                deseados.Add(id);
            }
            else
            {
                var nuevo = crear(entrada);
                destino.Add(nuevo);
                // Las entidades nuevas se marcan explícitamente como Added. Si solo se agregan
                // a la navegación, el detector de cambios puede clasificarlas como Modified
                // (especialmente en el proveedor InMemory) y generar un UPDATE de una fila
                // inexistente en lugar del INSERT correspondiente.
                _contexto.Entry(nuevo).State = EntityState.Added;
            }
        }

        foreach (var id in actuales.Keys.Where(id => !deseados.Contains(id)).ToList())
        {
            var eliminado = actuales[id];
            destino.Remove(eliminado);
            _contexto.Entry(eliminado).State = EntityState.Deleted;
        }
    }
}
