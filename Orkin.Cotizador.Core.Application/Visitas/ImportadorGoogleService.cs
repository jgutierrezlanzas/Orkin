using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Visitas;

/// <summary>Evento exportado de Google Calendar, en el formato que usa el legacy.</summary>
public sealed record EventoGoogleImportar(
    string? EventoId,
    string? CalendarId,
    string? Summary,
    string? Ubicacion,
    string? Descripcion,
    DateTime? FechaInicio,
    DateTime? FechaFin);

/// <summary>Lote a importar. El nombre es opcional: si no viene se genera con la fecha.</summary>
public sealed record ImportacionGoogleRequest(
    string? Nombre,
    string? Origen,
    IReadOnlyList<EventoGoogleImportar> Eventos);

public sealed record ImportacionGoogleResumen(
    Guid LoteId,
    string Nombre,
    int TotalEventos,
    int Listos,
    int Conflictos);

/// <summary>Corrección manual de un evento en staging (la vista previa es editable).</summary>
public sealed record CorregirEventoGoogleDatos(
    string? ClienteCodigo,
    string? LocacionCodigo,
    string? TipoServicioCodigo,
    string? VehiculoCodigo,
    string? TecnicosCodigos,
    string? Nota,
    long? Plantilla);

public sealed record MapeoEventoGoogleGuardar(
    string Campo,
    string ValorOrigen,
    string ValorDestino,
    string? Descripcion,
    bool Activo);

/// <summary>
/// Importador de Google Calendar (Fase 3). Lee eventos exportados, los parsea con las reglas del
/// WinForms, aplica la homologación configurable de <c>int.ErpMapeoEventoGoogle</c>, valida los
/// códigos contra las vistas <c>vv*</c> del ERP y deja el resultado en staging para revisión.
/// <para>
/// Solo cuando el usuario confirma un evento se escribe en <c>ProgramacionVisitaConfirmadas</c>,
/// a través de <see cref="VisitaService"/>. Así el dato operativo sigue 100 % en las tablas
/// legacy (D-22) y el importador aporta auditoría del origen, que el WinForms no tenía.
/// </para>
/// </summary>
public sealed class ImportadorGoogleService
{
    private static readonly string[] CamposValidos =
        ["CLIENTE", "LOCACION", "TIPO_SERVICIO", "VEHICULO", "TECNICO"];

    private readonly CotizadorDbContext _contexto;
    private readonly VisitaService _visitas;

    public ImportadorGoogleService(CotizadorDbContext contexto, VisitaService visitas)
    {
        _contexto = contexto;
        _visitas = visitas;
    }

    /// <summary>Crea el lote y sus eventos en staging, ya parseados, homologados y validados.</summary>
    public async Task<ImportacionGoogleResumen> ImportarAsync(
        ImportacionGoogleRequest datos,
        CancellationToken cancellationToken = default)
    {
        if (datos.Eventos.Count == 0)
        {
            throw new InvalidOperationException("No hay eventos para importar.");
        }

        var lote = new ImportacionGoogleLote
        {
            Id = Guid.NewGuid(),
            Nombre = string.IsNullOrWhiteSpace(datos.Nombre)
                ? $"Importación {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
                : datos.Nombre.Trim()[..Math.Min(datos.Nombre.Trim().Length, 150)],
            Origen = Limite(datos.Origen, 2000),
            FechaImportacionUtc = DateTime.UtcNow,
            Estado = "BORRADOR",
            TotalEventos = datos.Eventos.Count
        };
        _contexto.ImportacionGoogleLote.Add(lote);

        var mapeo = await MapeosActivosAsync(cancellationToken);
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var listos = 0;
        var conflictos = 0;

        foreach (var evento in datos.Eventos)
        {
            var id = string.IsNullOrWhiteSpace(evento.EventoId)
                ? $"ORKIN-{Guid.NewGuid():N}"
                : evento.EventoId.Trim()[..Math.Min(evento.EventoId.Trim().Length, 100)];

            // Un mismo evento no se importa dos veces en el mismo lote.
            if (!vistos.Add(id))
            {
                continue;
            }

            var entidad = new ImportacionGoogleEvento
            {
                Id = Guid.NewGuid(),
                LoteId = lote.Id,
                EventoId = id,
                CalendarId = Limite(evento.CalendarId, 100),
                Summary = Limite(evento.Summary, 500),
                Ubicacion = Limite(evento.Ubicacion, 500),
                Descripcion = Limite(evento.Descripcion, 2000),
                FechaInicio = evento.FechaInicio,
                FechaFin = evento.FechaFin
            };

            try
            {
                Parsear(evento, entidad, mapeo);
                var errores = await ValidarAsync(entidad, cancellationToken);
                if (errores.Count > 0)
                {
                    entidad.Estado = EstadoImportacionGoogle.Conflicto;
                    entidad.Error = Limite(string.Join(" | ", errores), 2000);
                    conflictos++;
                }
                else
                {
                    entidad.Estado = EstadoImportacionGoogle.Listo;
                    listos++;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or IndexOutOfRangeException)
            {
                entidad.Estado = EstadoImportacionGoogle.Conflicto;
                entidad.Error = Limite(
                    "Formato de evento no reconocido. Revise los campos en la vista previa. " + ex.Message,
                    2000);
                conflictos++;
            }

            _contexto.ImportacionGoogleEvento.Add(entidad);
        }

        lote.TotalEventos = vistos.Count;
        lote.ConError = conflictos;
        await _contexto.SaveChangesAsync(cancellationToken);

        return new ImportacionGoogleResumen(
            lote.Id, lote.Nombre, lote.TotalEventos, listos, conflictos);
    }

    /// <summary>Eventos del lote (o de todos) para la grilla de vista previa.</summary>
    public async Task<IReadOnlyList<ImportacionGoogleEvento>> ListarEventosAsync(
        Guid? loteId = null,
        string? estado = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.ImportacionGoogleEvento.AsNoTracking().AsQueryable();
        if (loteId is { } id)
        {
            consulta = consulta.Where(e => e.LoteId == id);
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            consulta = consulta.Where(e => e.Estado == estado.Trim().ToUpperInvariant());
        }

        return await consulta.OrderBy(e => e.FechaInicio).ThenBy(e => e.EventoId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ImportacionGoogleEvento?> ObtenerEventoAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await _contexto.ImportacionGoogleEvento.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ImportacionGoogleLote>> ListarLotesAsync(
        CancellationToken cancellationToken = default)
        => await _contexto.ImportacionGoogleLote.AsNoTracking()
            .OrderByDescending(l => l.FechaImportacionUtc).ToListAsync(cancellationToken);

    /// <summary>Aplica una corrección manual y vuelve a validar el evento.</summary>
    public async Task<ImportacionGoogleEvento?> CorregirAsync(
        Guid id,
        CorregirEventoGoogleDatos datos,
        CancellationToken cancellationToken = default)
    {
        var evento = await _contexto.ImportacionGoogleEvento.FindAsync([id], cancellationToken);
        if (evento is null)
        {
            return null;
        }

        if (evento.Estado is EstadoImportacionGoogle.Confirmado or EstadoImportacionGoogle.Descartado)
        {
            throw new InvalidOperationException("Ese evento ya fue confirmado o descartado.");
        }

        evento.ClienteCodigo = Limite(datos.ClienteCodigo, 20);
        evento.LocacionCodigo = Limite(datos.LocacionCodigo, 5);
        evento.TipoServicioCodigo = Limite(datos.TipoServicioCodigo, 4);
        evento.VehiculoCodigo = Limite(datos.VehiculoCodigo, 4);
        evento.TecnicosCodigos = Limite(datos.TecnicosCodigos, 250);
        evento.Nota = Limite(datos.Nota, 250);
        evento.Plantilla = datos.Plantilla;

        var errores = await ValidarAsync(evento, cancellationToken);
        evento.Estado = errores.Count == 0
            ? EstadoImportacionGoogle.Listo
            : EstadoImportacionGoogle.Conflicto;
        evento.Error = errores.Count == 0 ? null : Limite(string.Join(" | ", errores), 2000);

        await _contexto.SaveChangesAsync(cancellationToken);
        await RecalcularLoteAsync(evento.LoteId, cancellationToken);
        return evento;
    }

    /// <summary>Descarta un evento conflictivo sin crear ninguna visita.</summary>
    public async Task<bool> DescartarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evento = await _contexto.ImportacionGoogleEvento.FindAsync([id], cancellationToken);
        if (evento is null)
        {
            return false;
        }

        if (evento.Estado == EstadoImportacionGoogle.Confirmado)
        {
            throw new InvalidOperationException("Ese evento ya se confirmó como visita.");
        }

        evento.Estado = EstadoImportacionGoogle.Descartado;
        await _contexto.SaveChangesAsync(cancellationToken);
        await RecalcularLoteAsync(evento.LoteId, cancellationToken);
        return true;
    }

    /// <summary>Confirma el evento como visita real en las tablas legacy.</summary>
    public async Task<long> ConfirmarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evento = await _contexto.ImportacionGoogleEvento.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException("No existe el evento.");

        if (evento.Estado != EstadoImportacionGoogle.Listo)
        {
            throw new InvalidOperationException(
                "El evento no está listo: corrija la homologación antes de confirmarlo.");
        }

        if (await _contexto.ProgramacionVisitaConfirmada.AnyAsync(
                v => v.IdCalendario == evento.EventoId, cancellationToken))
        {
            throw new InvalidOperationException("Ese evento ya está confirmado como visita.");
        }

        var tecnicos = (evento.TecnicosCodigos ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tecnicos.Count == 0)
        {
            throw new InvalidOperationException("El evento no tiene técnicos asignados.");
        }

        var iniciales = await InicialesTecnicosAsync(tecnicos, cancellationToken);
        var visita = await _visitas.CrearAsync(new VisitaGuardar(
            evento.EventoId,
            evento.ClienteCodigo,
            evento.LocacionCodigo,
            evento.TipoServicioCodigo,
            DateOnly.FromDateTime((evento.FechaInicio ?? DateTime.Today).Date),
            TimeSpan.FromTicks((evento.FechaInicio ?? DateTime.Today).TimeOfDay.Ticks),
            DateOnly.FromDateTime((evento.FechaFin ?? evento.FechaInicio ?? DateTime.Today).Date),
            TimeSpan.FromTicks((evento.FechaFin ?? evento.FechaInicio ?? DateTime.Today).TimeOfDay.Ticks),
            evento.VehiculoCodigo,
            evento.Nota,
            evento.Plantilla,
            tecnicos.Select(c => new VisitaTecnicoGuardar(c, iniciales.GetValueOrDefault(c))).ToList()),
            cancellationToken);

        evento.Estado = EstadoImportacionGoogle.Confirmado;
        evento.IdVisita = visita.Visita.IdVisita;
        evento.ProcesadoUtc = DateTime.UtcNow;
        evento.Error = null;
        await _contexto.SaveChangesAsync(cancellationToken);
        await RecalcularLoteAsync(evento.LoteId, cancellationToken);
        return evento.IdVisita.Value;
    }

    // ── Homologación ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ErpMapeoEventoGoogle>> ListarMapeosAsync(
        bool soloActivos = false,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.ErpMapeoEventoGoogle.AsNoTracking().AsQueryable();
        if (soloActivos)
        {
            consulta = consulta.Where(m => m.Activo);
        }

        return await consulta.OrderBy(m => m.Campo).ThenBy(m => m.ValorOrigen)
            .ToListAsync(cancellationToken);
    }

    public async Task<ErpMapeoEventoGoogle> GuardarMapeoAsync(
        MapeoEventoGoogleGuardar datos,
        Guid? id = null,
        CancellationToken cancellationToken = default)
    {
        if (!CamposValidos.Contains(datos.Campo, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Campo no válido. Use CLIENTE, LOCACION, TIPO_SERVICIO, VEHICULO o TECNICO.");
        }

        var mapeo = id is { } buscado
            ? await _contexto.ErpMapeoEventoGoogle.FindAsync([buscado], cancellationToken)
                ?? throw new InvalidOperationException("No existe esa homologación.")
            : new ErpMapeoEventoGoogle { Id = Guid.NewGuid() };

        mapeo.Campo = datos.Campo.Trim().ToUpperInvariant();
        mapeo.ValorOrigen = datos.ValorOrigen.Trim();
        mapeo.ValorDestino = datos.ValorDestino.Trim();
        mapeo.Descripcion = Limite(datos.Descripcion, 200);
        mapeo.Activo = datos.Activo;

        if (id is null)
        {
            var duplicado = await _contexto.ErpMapeoEventoGoogle.AnyAsync(
                m => m.Campo == mapeo.Campo && m.ValorOrigen == mapeo.ValorOrigen,
                cancellationToken);
            if (duplicado)
            {
                throw new InvalidOperationException(
                    "Ya existe una homologación para ese campo y valor de origen.");
            }

            _contexto.ErpMapeoEventoGoogle.Add(mapeo);
        }

        await _contexto.SaveChangesAsync(cancellationToken);
        return mapeo;
    }

    public async Task<bool> EliminarMapeoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mapeo = await _contexto.ErpMapeoEventoGoogle.FindAsync([id], cancellationToken);
        if (mapeo is null)
        {
            return false;
        }

        _contexto.ErpMapeoEventoGoogle.Remove(mapeo);
        await _contexto.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Internos ─────────────────────────────────────────────────────────────────

    /// <summary>Parseo con las mismas reglas del WinForms, pero sin fallar por índices ausentes.</summary>
    private static void Parsear(
        EventoGoogleImportar evento,
        ImportacionGoogleEvento destino,
        IReadOnlyDictionary<(string Campo, string Origen), string> mapeo)
    {
        var summary = (evento.Summary ?? string.Empty).Split('/');
        var descripcion = (evento.Descripcion ?? string.Empty).Split('/');

        destino.ClienteParseado = Parte(summary, 1)?.Split('-')[0].Trim();
        destino.LocacionParseada = (evento.Ubicacion ?? string.Empty).Split('-')[0].Trim();
        destino.TipoServicioParseado = Parte(descripcion, 0)?.Split('-')[0].Trim();
        destino.VehiculoParseado = TrasDosPuntos(Parte(descripcion, 1));
        destino.TecnicosParseados = TrasDosPuntos(Parte(descripcion, 2));
        destino.Nota = Limite(Parte(descripcion, 3)?.Trim(), 250);

        destino.ClienteCodigo = Homologar(mapeo, "CLIENTE", destino.ClienteParseado);
        destino.LocacionCodigo = Homologar(mapeo, "LOCACION", destino.LocacionParseada);
        destino.TipoServicioCodigo = Homologar(mapeo, "TIPO_SERVICIO", destino.TipoServicioParseado);
        destino.VehiculoCodigo = Homologar(mapeo, "VEHICULO", destino.VehiculoParseado);

        var tecnicos = (destino.TecnicosParseados ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Homologar(mapeo, "TECNICO", t))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        destino.TecnicosCodigos = Limite(string.Join(",", tecnicos), 250);

        if (string.IsNullOrWhiteSpace(destino.ClienteCodigo) ||
            string.IsNullOrWhiteSpace(destino.LocacionCodigo) ||
            string.IsNullOrWhiteSpace(destino.TipoServicioCodigo) ||
            string.IsNullOrWhiteSpace(destino.VehiculoCodigo) ||
            string.IsNullOrWhiteSpace(destino.TecnicosCodigos))
        {
            throw new InvalidOperationException(
                "Faltan campos en el evento (cliente, locación, servicio, vehículo o técnicos).");
        }
    }

    private async Task<List<string>> ValidarAsync(
        ImportacionGoogleEvento evento,
        CancellationToken cancellationToken)
    {
        var errores = new List<string>();

        var cliente = await _contexto.VistaClienteErp.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Cliente == evento.ClienteCodigo, cancellationToken);
        if (cliente is null || !EsActivo(cliente.Activo, "S"))
        {
            errores.Add($"Cliente '{evento.ClienteCodigo}' inexistente o inactivo en el ERP.");
        }

        var locacion = await _contexto.VistaLocacionErp.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Codigo == evento.LocacionCodigo, cancellationToken);
        if (locacion is null || !EsActivo(locacion.Activo, "SI"))
        {
            errores.Add($"Locación '{evento.LocacionCodigo}' inexistente o inactiva en el ERP.");
        }

        var servicio = await _contexto.VistaTipoServicioErp.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Codigo == evento.TipoServicioCodigo, cancellationToken);
        if (servicio is null || !EsActivo(servicio.Activo, "SI"))
        {
            errores.Add($"Tipo de servicio '{evento.TipoServicioCodigo}' inexistente o inactivo en el ERP.");
        }

        var vehiculo = await _contexto.VistaVehiculoErp.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Codigo == evento.VehiculoCodigo, cancellationToken);
        if (vehiculo is null || !EsActivo(vehiculo.Activo, "SI"))
        {
            errores.Add($"Vehículo '{evento.VehiculoCodigo}' inexistente o inactivo en el ERP.");
        }

        var codigosTecnico = (evento.TecnicosCodigos ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var codigo in codigosTecnico)
        {
            var tecnico = await _contexto.VistaTecnicoErp.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Codigo == codigo || t.Iniciales == codigo, cancellationToken);
            if (tecnico is null || !EsActivo(tecnico.Activo, "SI"))
            {
                errores.Add($"Técnico '{codigo}' inexistente o inactivo en el ERP.");
            }
        }

        if (evento.Plantilla is { } plantilla)
        {
            var existe = await _contexto.PlantillaOrdenTrabajo.AsNoTracking()
                .AnyAsync(p => p.Plantilla == plantilla && p.Activo == "SI", cancellationToken);
            if (!existe)
            {
                errores.Add($"Plantilla {plantilla} inexistente o dada de baja.");
            }
        }

        return errores;
    }

    private async Task<Dictionary<string, string>> InicialesTecnicosAsync(
        IReadOnlyList<string> codigos,
        CancellationToken cancellationToken)
    {
        var lista = codigos.Select(c => c.ToUpperInvariant()).Distinct().ToList();
        var tecnicos = await _contexto.VistaTecnicoErp.AsNoTracking()
            .Where(t => lista.Contains(t.Codigo) || lista.Contains(t.Iniciales))
            .Select(t => new { t.Codigo, t.Iniciales })
            .ToListAsync(cancellationToken);
        var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var codigo in codigos)
        {
            var tecnico = tecnicos.FirstOrDefault(t =>
                string.Equals(t.Codigo, codigo, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Iniciales, codigo, StringComparison.OrdinalIgnoreCase));
            if (tecnico is not null)
            {
                resultado[codigo] = tecnico.Iniciales ?? string.Empty;
            }
        }

        return resultado;
    }

    private async Task<IReadOnlyDictionary<(string Campo, string Origen), string>> MapeosActivosAsync(
        CancellationToken cancellationToken)
        => (await _contexto.ErpMapeoEventoGoogle.AsNoTracking()
                .Where(m => m.Activo).ToListAsync(cancellationToken))
            .ToDictionary(
                m => (m.Campo.ToUpperInvariant(), m.ValorOrigen.Trim()),
                m => m.ValorDestino.Trim(),
                StringTupleComparer.Instance);

    private async Task RecalcularLoteAsync(Guid loteId, CancellationToken cancellationToken)
    {
        var estados = await _contexto.ImportacionGoogleEvento.AsNoTracking()
            .Where(e => e.LoteId == loteId)
            .Select(e => e.Estado).ToListAsync(cancellationToken);
        var lote = await _contexto.ImportacionGoogleLote.FindAsync([loteId], cancellationToken);
        if (lote is null)
        {
            return;
        }

        lote.TotalEventos = estados.Count;
        lote.Confirmados = estados.Count(e => e == EstadoImportacionGoogle.Confirmado);
        lote.Descartados = estados.Count(e => e == EstadoImportacionGoogle.Descartado);
        lote.ConError = estados.Count(e => e == EstadoImportacionGoogle.Conflicto);
        if (lote.Confirmados + lote.Descartados + lote.ConError == lote.TotalEventos)
        {
            lote.Estado = "PROCESADO";
        }

        await _contexto.SaveChangesAsync(cancellationToken);
    }

    private static string? Homologar(
        IReadOnlyDictionary<(string Campo, string Origen), string> mapeo,
        string campo,
        string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var texto = valor.Trim();
        return mapeo.TryGetValue((campo.ToUpperInvariant(), texto), out var destino)
            ? destino
            : texto;
    }

    private static string? Parte(string[] partes, int indice)
        => partes.Length > indice ? partes[indice] : null;

    private static string? TrasDosPuntos(string? texto)
    {
        if (texto is null)
        {
            return null;
        }

        var partes = texto.Split(':');
        return partes.Length > 1 ? partes[1].Trim() : null;
    }

    private static bool EsActivo(string? valor, string esperado)
        => string.Equals(valor?.Trim(), esperado, StringComparison.OrdinalIgnoreCase);

    private static string? Limite(string? valor, int maximo)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, maximo)];

    /// <summary>Comparador de tuplas sin distinción de mayúsculas para la homologación.</summary>
    private sealed class StringTupleComparer : IEqualityComparer<(string Campo, string Origen)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals((string Campo, string Origen) x, (string Campo, string Origen) y)
            => string.Equals(x.Campo, y.Campo, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.Origen, y.Origen, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Campo, string Origen) obj)
            => HashCode.Combine(
                obj.Campo.ToUpperInvariant().GetHashCode(StringComparison.Ordinal),
                obj.Origen.ToUpperInvariant().GetHashCode(StringComparison.Ordinal));
    }
}
