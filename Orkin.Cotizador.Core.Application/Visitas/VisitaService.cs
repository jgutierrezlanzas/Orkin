using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Visitas;

/// <summary>
/// Envuelve el stored procedure legacy <c>dbo.SINCO_GENERA_ORDENE_DE_TRABAJO</c> (D-22: se
/// reutiliza, no se reimplementa) y devuelve el número de orden generado.
/// </summary>
public interface IGeneradorOrdenTrabajo
{
    Task<string?> GenerarDesdeVisitaAsync(long idVisita, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class GeneradorOrdenTrabajoService : IGeneradorOrdenTrabajo
{
    private readonly CotizadorDbContext _contexto;

    public GeneradorOrdenTrabajoService(CotizadorDbContext contexto) => _contexto = contexto;

    public async Task<string?> GenerarDesdeVisitaAsync(
        long idVisita,
        CancellationToken cancellationToken = default)
    {
        // El SP valida la visita, copia la plantilla y los técnicos y asigna el consecutivo de OT.
        await _contexto.Database.ExecuteSqlRawAsync(
            "EXEC dbo.SINCO_GENERA_ORDENE_DE_TRABAJO @p0",
            new object[] { idVisita },
            cancellationToken);

        return await _contexto.OrdenTrabajoVisita
            .AsNoTracking()
            .Where(o => o.IdVisita == idVisita)
            .OrderByDescending(o => o.OrdenNumero)
            .Select(o => (string?)o.OrdenNumero)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>
/// Resumen de una visita para la grilla/calendario. Los nombres se resuelven desde los catálogos
/// compartidos y los códigos se conservan tal como los guarda el ERP (D-20).
/// </summary>
public sealed record VisitaResumen(
    ProgramacionVisitaConfirmada Visita,
    int Tecnicos,
    IReadOnlyList<string> CodigosTecnico,
    string? ClienteNombre,
    long? Plantilla,
    string? DescripTrabajo,
    string? OrdenNumero);

/// <summary>Visita con sus técnicos, su plantilla y el número de orden si ya se generó.</summary>
public sealed record VisitaCompleta(
    ProgramacionVisitaConfirmada Visita,
    IReadOnlyList<ProgramacionVisitaDetalleConfirmada> Tecnicos,
    IReadOnlyList<PlantillaCalendario> Calendarios,
    string? ClienteNombre,
    long? Plantilla,
    string? DescripTrabajo,
    string? OrdenNumero);

/// <summary>Datos de alta/edición. El servicio asigna <c>ID_VISITA</c> y el evento cuando aplica.</summary>
public sealed record VisitaGuardar(
    string? IdCalendario,
    string? Cliente,
    string? Locacion,
    string? TipoServicio,
    DateOnly? FechaInicio,
    TimeSpan? HoraInicio,
    DateOnly? FechaFinal,
    TimeSpan? HoraFinal,
    string? CodigoVehiculo,
    string? Nota,
    long? Plantilla,
    IReadOnlyList<VisitaTecnicoGuardar> Tecnicos);

public sealed record VisitaTecnicoGuardar(string? CodigoTecnico, string? Iniciales);

/// <summary>
/// Gestión de la agenda propia sobre las tablas legacy existentes (D-22):
/// <c>ProgramacionVisitaConfirmadas</c>, <c>ProgramacionVisitaDetalleConfirmada</c> y
/// <c>Plantilla_Calendario</c>. No se crean tablas ni se importan datos a tablas paralelas.
/// </summary>
public sealed class VisitaService
{
    private readonly CotizadorDbContext _contexto;
    private readonly IContextoUsuario _usuario;
    private readonly IGeneradorOrdenTrabajo _generador;

    public VisitaService(
        CotizadorDbContext contexto,
        IContextoUsuario? usuario = null,
        IGeneradorOrdenTrabajo? generador = null)
    {
        _contexto = contexto;
        _usuario = usuario ?? new ContextoUsuarioSistema();
        _generador = generador ?? new GeneradorOrdenTrabajoService(contexto);
    }

    /// <summary>Lista visitas en un rango de fechas con sus técnicos y su orden, en una sola pasada.</summary>
    public async Task<IReadOnlyList<VisitaResumen>> ListarAsync(
        DateOnly? desde = null,
        DateOnly? hasta = null,
        string? texto = null,
        string? cliente = null,
        long? plantilla = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.ProgramacionVisitaConfirmada.AsNoTracking().AsQueryable();

        if (desde is { } d)
        {
            consulta = consulta.Where(v => v.FechaInicio >= d);
        }

        if (hasta is { } h)
        {
            consulta = consulta.Where(v => v.FechaInicio <= h);
        }

        if (!string.IsNullOrWhiteSpace(cliente))
        {
            consulta = consulta.Where(v => v.Cliente == cliente.Trim());
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            consulta = consulta.Where(v =>
                (v.IdCalendario != null && v.IdCalendario.Contains(t)) ||
                (v.Cliente != null && v.Cliente.Contains(t)) ||
                (v.Nota != null && v.Nota.Contains(t)));
        }

        var visitas = await consulta
            .OrderBy(v => v.FechaInicio).ThenBy(v => v.HoraInicio).ThenBy(v => v.IdVisita)
            .ToListAsync(cancellationToken);

        if (visitas.Count == 0)
        {
            return [];
        }

        var ids = visitas.Select(v => v.IdVisita).Distinct().ToList();
        var idsSet = ids.ToHashSet();
        var (minId, maxId) = (ids.Min(), ids.Max());
        var calendarioIds = visitas.Select(v => v.IdCalendario)
            .Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).Distinct().ToList();
        var calendarioSet = calendarioIds.ToHashSet();

        // Ojo con `lista.Contains(columna)`: EF lo traduce a OPENJSON(... '$'), que el SQL Server
        // del ERP rechaza (error 102). Se filtra por rango en SQL y se acota el conjunto en memoria.
        // La agrupación también va en memoria: proyectar una colección dentro de un GroupBy genera
        // la misma sintaxis JSON.
        var filasTecnico = await _contexto.ProgramacionVisitaDetalleConfirmada.AsNoTracking()
            .Where(t => t.IdVisita >= minId && t.IdVisita <= maxId)
            .Select(t => new { t.IdVisita, t.CodigoTecnico })
            .ToListAsync(cancellationToken);
        var tecnicos = filasTecnico
            .Where(t => idsSet.Contains(t.IdVisita))
            .GroupBy(t => t.IdVisita)
            .ToDictionary(g => g.Key, g => new
            {
                Total = g.Count(),
                Codigos = g.Select(t => t.CodigoTecnico)
                    .Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).ToList()
            });

        var plantillaPorEvento = new Dictionary<string, long>();
        if (calendarioIds.Count > 0)
        {
            var minCalendario = calendarioIds.Min();
            var maxCalendario = calendarioIds.Max();
            var calendarios = await _contexto.PlantillaCalendario.AsNoTracking()
                .Where(c => string.Compare(c.IdCalendario, minCalendario) >= 0 &&
                            string.Compare(c.IdCalendario, maxCalendario) <= 0)
                .ToListAsync(cancellationToken);
            // La clave de Plantilla_Calendario es (PLANTILLA, ID_CALENDARIO): un mismo evento
            // puede estar en varias plantillas. Se toma la menor para mostrar una sola ficha.
            plantillaPorEvento = calendarios
                .Where(c => calendarioSet.Contains(c.IdCalendario))
                .OrderBy(c => c.Plantilla)
                .GroupBy(c => c.IdCalendario)
                .ToDictionary(g => g.Key, g => g.First().Plantilla);
        }

        var ordenes = await _contexto.OrdenTrabajoVisita.AsNoTracking()
            .Where(o => o.IdVisita >= minId && o.IdVisita <= maxId)
            .ToListAsync(cancellationToken);
        var ordenesPorVisita = ordenes
            .Where(o => idsSet.Contains(o.IdVisita))
            .GroupBy(o => o.IdVisita) // una visita puede tener varias órdenes (una por plantilla)
            .ToDictionary(g => g.Key, g => g.Min(o => o.OrdenNumero));

        var nombres = await NombresClienteAsync(visitas.Select(v => v.Cliente), cancellationToken);
        var plantillas = await DescripcionesPlantillaAsync(
            plantillaPorEvento.Values.Distinct(), cancellationToken);

        var resultado = visitas.Select(v =>
        {
            plantillaPorEvento.TryGetValue(v.IdCalendario, out var numeroPlantilla);
            plantillas.TryGetValue(numeroPlantilla, out var descripcion);
            ordenesPorVisita.TryGetValue(v.IdVisita, out var orden);
            tecnicos.TryGetValue(v.IdVisita, out var detalle);
            return new VisitaResumen(
                v,
                detalle?.Total ?? 0,
                detalle?.Codigos ?? [],
                nombres.GetValueOrDefault(v.Cliente ?? string.Empty),
                numeroPlantilla == 0 ? null : numeroPlantilla,
                descripcion,
                orden);
        });

        if (plantilla is { } filtroPlantilla)
        {
            resultado = resultado.Where(v => v.Plantilla == filtroPlantilla);
        }

        return resultado.ToList();
    }

    /// <summary>Detalle completo de una visita, o <c>null</c> si no existe.</summary>
    public async Task<VisitaCompleta?> ObtenerAsync(
        long idVisita,
        CancellationToken cancellationToken = default)
    {
        var visita = await _contexto.ProgramacionVisitaConfirmada.AsNoTracking()
            .FirstOrDefaultAsync(v => v.IdVisita == idVisita, cancellationToken);
        if (visita is null)
        {
            return null;
        }

        var tecnicos = await _contexto.ProgramacionVisitaDetalleConfirmada.AsNoTracking()
            .Where(t => t.IdVisita == idVisita).OrderBy(t => t.Linea)
            .ToListAsync(cancellationToken);
        var calendarios = await _contexto.PlantillaCalendario.AsNoTracking()
            .Where(c => c.IdCalendario == visita.IdCalendario)
            .OrderBy(c => c.Plantilla).ToListAsync(cancellationToken);
        var orden = await _contexto.OrdenTrabajoVisita.AsNoTracking()
            .Where(o => o.IdVisita == idVisita)
            .Select(o => (string?)o.OrdenNumero).FirstOrDefaultAsync(cancellationToken);
        var nombres = await NombresClienteAsync([visita.Cliente], cancellationToken);
        var plantillas = await DescripcionesPlantillaAsync(
            calendarios.Select(c => c.Plantilla), cancellationToken);

        return new VisitaCompleta(
            visita,
            tecnicos,
            calendarios,
            nombres.GetValueOrDefault(visita.Cliente ?? string.Empty),
            calendarios.FirstOrDefault()?.Plantilla,
            plantillas.GetValueOrDefault(calendarios.FirstOrDefault()?.Plantilla ?? 0),
            orden);
    }

    /// <summary>
    /// Confirma una visita: consecutivo <c>MAX+1</c>, un evento propio si no se suministra uno,
    /// al menos un técnico y exactamente una plantilla (mismas reglas del WinForms).
    /// </summary>
    public async Task<VisitaCompleta> CrearAsync(
        VisitaGuardar datos,
        CancellationToken cancellationToken = default)
    {
        Validar(datos);

        var idCalendario = string.IsNullOrWhiteSpace(datos.IdCalendario)
            ? $"ORKIN-{Guid.NewGuid():N}"
            : Limite(datos.IdCalendario, 100)!;

        if (await _contexto.ProgramacionVisitaConfirmada.AnyAsync(
                v => v.IdCalendario == idCalendario, cancellationToken))
        {
            throw new InvalidOperationException("Ese evento ya está confirmado como visita.");
        }

        var visita = new ProgramacionVisitaConfirmada
        {
            IdVisita = await SiguienteVisitaAsync(cancellationToken),
            IdCalendario = idCalendario,
            Cliente = Limite(datos.Cliente, 20),
            Locacion = Limite(datos.Locacion, 5),
            TipoServicio = Limite(datos.TipoServicio, 4),
            FechaInicio = datos.FechaInicio,
            HoraInicio = datos.HoraInicio,
            FechaFinal = datos.FechaFinal ?? datos.FechaInicio,
            HoraFinal = datos.HoraFinal ?? datos.HoraInicio,
            CodigoVehiculo = Limite(datos.CodigoVehiculo, 4),
            CantidadTecnicos = datos.Tecnicos.Count,
            Nota = Limite(datos.Nota, 250)
        };
        _contexto.ProgramacionVisitaConfirmada.Add(visita);

        GuardarTecnicos(visita.IdVisita, datos.Tecnicos);
        _contexto.PlantillaCalendario.Add(NuevoCalendario(datos.Plantilla!.Value, idCalendario));

        await _contexto.SaveChangesAsync(cancellationToken);
        return await ObtenerAsync(visita.IdVisita, cancellationToken)
            ?? throw new InvalidOperationException("No se pudo releer la visita creada.");
    }

    /// <summary>Edita la visita y reconcilia técnicos y plantilla por sus claves legacy.</summary>
    public async Task<VisitaCompleta?> ActualizarAsync(
        long idVisita,
        VisitaGuardar datos,
        CancellationToken cancellationToken = default)
    {
        Validar(datos);

        var visita = await _contexto.ProgramacionVisitaConfirmada
            .FirstOrDefaultAsync(v => v.IdVisita == idVisita, cancellationToken);
        if (visita is null)
        {
            return null;
        }

        // El evento de Google no se cambia desde la edición: es la identidad de la visita.
        var idCalendario = visita.IdCalendario;
        visita.Cliente = Limite(datos.Cliente, 20);
        visita.Locacion = Limite(datos.Locacion, 5);
        visita.TipoServicio = Limite(datos.TipoServicio, 4);
        visita.FechaInicio = datos.FechaInicio;
        visita.HoraInicio = datos.HoraInicio;
        visita.FechaFinal = datos.FechaFinal ?? datos.FechaInicio;
        visita.HoraFinal = datos.HoraFinal ?? datos.HoraInicio;
        visita.CodigoVehiculo = Limite(datos.CodigoVehiculo, 4);
        visita.Nota = Limite(datos.Nota, 250);
        visita.CantidadTecnicos = datos.Tecnicos.Count;

        var tecnicosActuales = await _contexto.ProgramacionVisitaDetalleConfirmada
            .Where(t => t.IdVisita == idVisita).OrderBy(t => t.Linea)
            .ToListAsync(cancellationToken);
        var tecnicosNuevos = datos.Tecnicos.OrderBy(t => t.CodigoTecnico).ToList();
        for (var i = 0; i < tecnicosNuevos.Count; i++)
        {
            var linea = i + 1;
            var destino = tecnicosActuales.ElementAtOrDefault(i);
            if (destino is null)
            {
                destino = new ProgramacionVisitaDetalleConfirmada { IdVisita = idVisita, Linea = linea };
                _contexto.ProgramacionVisitaDetalleConfirmada.Add(destino);
            }

            destino.Linea = linea;
            destino.CodigoTecnico = Limite(tecnicosNuevos[i].CodigoTecnico, 4);
            destino.Iniciales = Limite(tecnicosNuevos[i].Iniciales, 2);
        }

        _contexto.ProgramacionVisitaDetalleConfirmada.RemoveRange(
            tecnicosActuales.Skip(tecnicosNuevos.Count));

        var calendariosActuales = await _contexto.PlantillaCalendario
            .Where(c => c.IdCalendario == idCalendario).ToListAsync(cancellationToken);
        _contexto.PlantillaCalendario.RemoveRange(calendariosActuales);
        _contexto.PlantillaCalendario.Add(NuevoCalendario(datos.Plantilla!.Value, idCalendario));

        await _contexto.SaveChangesAsync(cancellationToken);
        return await ObtenerAsync(idVisita, cancellationToken);
    }

    /// <summary>
    /// Revierte una confirmación con borrado físico en el orden del legacy: técnicos → visita →
    /// plantilla/calendario. No se puede reversar una visita cuya orden de trabajo ya fue generada.
    /// </summary>
    public async Task ReversarAsync(
        long idVisita,
        CancellationToken cancellationToken = default)
    {
        var visita = await _contexto.ProgramacionVisitaConfirmada
            .FirstOrDefaultAsync(v => v.IdVisita == idVisita, cancellationToken)
            ?? throw new InvalidOperationException($"No existe la visita {idVisita}.");

        if (await _contexto.OrdenTrabajoVisita.AnyAsync(o => o.IdVisita == idVisita, cancellationToken))
        {
            throw new InvalidOperationException(
                "No se puede reversar una visita que ya tiene orden de trabajo generada.");
        }

        var tecnicos = await _contexto.ProgramacionVisitaDetalleConfirmada
            .Where(t => t.IdVisita == idVisita).ToListAsync(cancellationToken);
        _contexto.ProgramacionVisitaDetalleConfirmada.RemoveRange(tecnicos);

        var calendarios = await _contexto.PlantillaCalendario
            .Where(c => c.IdCalendario == visita.IdCalendario).ToListAsync(cancellationToken);
        _contexto.PlantillaCalendario.RemoveRange(calendarios);

        _contexto.ProgramacionVisitaConfirmada.Remove(visita);
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Genera la orden de trabajo desde la visita invocando el SP legacy.</summary>
    public async Task<string?> GenerarOrdenTrabajoAsync(
        long idVisita,
        CancellationToken cancellationToken = default)
    {
        if (!await _contexto.ProgramacionVisitaConfirmada
                .AnyAsync(v => v.IdVisita == idVisita, cancellationToken))
        {
            throw new InvalidOperationException($"No existe la visita {idVisita}.");
        }

        return await _generador.GenerarDesdeVisitaAsync(idVisita, cancellationToken);
    }

    // ── Internos ──────────────────────────────────────────────────────────────────

    private async Task<long> SiguienteVisitaAsync(CancellationToken cancellationToken)
    {
        var maximo = await _contexto.ProgramacionVisitaConfirmada
            .MaxAsync(v => (long?)v.IdVisita, cancellationToken);
        return (maximo ?? 0) + 1;
    }

    private void GuardarTecnicos(
        long idVisita,
        IReadOnlyList<VisitaTecnicoGuardar> tecnicos)
    {
        for (var i = 0; i < tecnicos.Count; i++)
        {
            _contexto.ProgramacionVisitaDetalleConfirmada.Add(new ProgramacionVisitaDetalleConfirmada
            {
                IdVisita = idVisita,
                Linea = i + 1,
                CodigoTecnico = Limite(tecnicos[i].CodigoTecnico, 4),
                Iniciales = Limite(tecnicos[i].Iniciales, 2)
            });
        }
    }

    private PlantillaCalendario NuevoCalendario(long plantilla, string idCalendario) => new()
    {
        Plantilla = plantilla,
        IdCalendario = idCalendario,
        FechaServicio = DateOnly.FromDateTime(DateTime.Today),
        HoraServicio = TimeSpan.Zero,
        FechaCreacion = DateOnly.FromDateTime(DateTime.Today),
        UsuarioCreacion = UsuarioCreacion()
    };

    private string UsuarioCreacion()
    {
        var nombre = string.IsNullOrWhiteSpace(_usuario.NombreUsuario)
            ? "SISTEMA"
            : _usuario.NombreUsuario.Trim();
        return Limite(nombre, 25)!;
    }

    private async Task<IReadOnlyDictionary<string, string>> NombresClienteAsync(
        IEnumerable<string?> codigos,
        CancellationToken cancellationToken)
    {
        var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        if (lista.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        // Rango en SQL + conjuntos en memoria: evita el OPENJSON('$') que falla en el ERP.
        var min = lista.Min();
        var max = lista.Max();
        var clientes = await _contexto.Cliente.AsNoTracking()
            .Where(c => string.Compare(c.Codigo, min) >= 0 && string.Compare(c.Codigo, max) <= 0)
            .Select(c => new { c.Codigo, c.Nombre })
            .ToListAsync(cancellationToken);
        return clientes
            .Where(c => lista.Contains(c.Codigo))
            .GroupBy(c => c.Codigo)
            .ToDictionary(g => g.Key, g => g.First().Nombre);
    }

    private async Task<IReadOnlyDictionary<long, string>> DescripcionesPlantillaAsync(
        IEnumerable<long> numeros,
        CancellationToken cancellationToken)
    {
        var lista = numeros.Where(n => n > 0).Distinct().ToList();
        if (lista.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var min = lista.Min();
        var max = lista.Max();
        var plantillas = await _contexto.PlantillaOrdenTrabajo.AsNoTracking()
            .Where(p => p.Plantilla >= min && p.Plantilla <= max)
            .Select(p => new { p.Plantilla, p.DescripTrabajo })
            .ToListAsync(cancellationToken);
        return plantillas
            .Where(p => lista.Contains(p.Plantilla))
            .GroupBy(p => p.Plantilla)
            .ToDictionary(
                g => g.Key,
                g => g.First().DescripTrabajo ?? string.Empty);
    }

    private static void Validar(VisitaGuardar datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Cliente))
        {
            throw new InvalidOperationException("La visita requiere un cliente.");
        }

        if (string.IsNullOrWhiteSpace(datos.Locacion))
        {
            throw new InvalidOperationException("La visita requiere una locación.");
        }

        if (datos.FechaInicio is null)
        {
            throw new InvalidOperationException("La visita requiere fecha de inicio.");
        }

        if (datos.Plantilla is null || datos.Plantilla <= 0)
        {
            throw new InvalidOperationException("La visita requiere una plantilla de orden de trabajo.");
        }

        if (datos.Tecnicos.Count == 0)
        {
            throw new InvalidOperationException("La visita requiere al menos un técnico.");
        }
    }

    private static string? Limite(string? valor, int maximo)
        => valor is null ? null : (valor.Trim() is { Length: > 0 } t ? t[..Math.Min(t.Length, maximo)] : null);
}
