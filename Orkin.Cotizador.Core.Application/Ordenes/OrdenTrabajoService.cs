using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Visitas;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Ordenes;

/// <summary>Clave real de <c>dbo.Orden_Trabajo</c>: las tres columnas de su PK legacy.</summary>
public sealed record OrdenClave(string OrdenNumero, long IdVisita, long Plantilla);

/// <summary>Fila de la grilla: cabecera legacy, nombre legible del cliente y conteo de hijas.</summary>
public sealed record OrdenResumen(
    OrdenTrabajo Cabecera,
    string? ClienteNombre,
    int Detalles,
    int Servicios,
    int Equipos,
    int Tecnicos);

/// <summary>Orden completa: la cabecera y sus cuatro colecciones en las tablas originales.</summary>
public sealed record OrdenCompleta(
    OrdenTrabajo Cabecera,
    IReadOnlyList<OrdenTrabajoDetalle> Detalles,
    IReadOnlyList<OrdenTrabajoServicio> Servicios,
    IReadOnlyList<OrdenTrabajoEquipo> Equipos,
    IReadOnlyList<OrdenTrabajoTecnico> Tecnicos,
    string? ClienteNombre);

/// <summary>Datos editables. Las claves y los consecutivos los asigna el servicio, igual que el
/// WinForms: el formulario nunca elige <c>CONSECUTIVO</c>.</summary>
public sealed record OrdenGuardar(
    string? Cliente,
    DateOnly? FechaOrden,
    TimeSpan? Hora,
    string? Direccion,
    string? Contacto,
    string? Telefono,
    string? Vehiculo,
    string? ServicioRealizado,
    string? DescripTrabajo,
    string? Notas,
    string? Instrucciones,
    IReadOnlyList<OrdenTrabajoDetalle> Detalles,
    IReadOnlyList<OrdenTrabajoServicio> Servicios,
    IReadOnlyList<OrdenTrabajoEquipo> Equipos,
    IReadOnlyList<OrdenTrabajoTecnico> Tecnicos);

/// <summary>
/// Gestión de órdenes de trabajo sobre las <b>cinco tablas legacy existentes</b> (D-22):
/// <c>Orden_Trabajo</c>, <c>_Detalle</c>, <c>_Servicio</c>, <c>_Equipos</c> y <c>_Tecnicos</c>.
/// <para>
/// El alta no se reimplementa: se invoca <c>SINCO_GENERA_ORDENE_DE_TRABAJO</c> desde una visita
/// confirmada, exactamente como lo hacía el WinForms. La edición solo se permite en <c>GE</c>;
/// <c>AP</c> y <c>AN</c> se bloquean con los mensajes del legacy. La anulación y la generación de
/// requisa también delegan en los SPs originales, que además actualizan <c>REQUISA</c>.
/// </para>
/// </summary>
public sealed class OrdenTrabajoService
{
    private const string ActivoCorto = "S";
    private const string Generada = "GE";
    private const string Aprobada = "AP";
    private const string Anulada = "AN";

    private readonly CotizadorDbContext _contexto;
    private readonly IGeneradorOrdenTrabajo _generador;

    public OrdenTrabajoService(CotizadorDbContext contexto, IGeneradorOrdenTrabajo generador)
    {
        _contexto = contexto;
        _generador = generador;
    }

    // ── Lectura ───────────────────────────────────────────────────────────────────

    /// <summary>Listado con los filtros de la grilla. Se acota con <paramref name="limite"/>
    /// porque el ERP tiene más de 43.000 órdenes y la pantalla no necesita cargarlas todas.</summary>
    public async Task<IReadOnlyList<OrdenResumen>> ListarAsync(
        string? texto = null,
        string? cliente = null,
        string? estado = null,
        DateOnly? desde = null,
        DateOnly? hasta = null,
        int limite = 200,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.OrdenTrabajo.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(cliente))
        {
            consulta = consulta.Where(o => o.Cliente == cliente.Trim());
        }

        var filtroEstado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (filtroEstado is not null)
        {
            consulta = consulta.Where(o => o.Estado == filtroEstado);
        }

        if (desde is { } d)
        {
            consulta = consulta.Where(o => o.FechaOrden >= d);
        }

        if (hasta is { } h)
        {
            consulta = consulta.Where(o => o.FechaOrden <= h);
        }

        var filtro = texto?.Trim();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            // Contains sobre un parámetro (no sobre una lista local) se traduce a CHARINDEX y sí
            // funciona en el SQL Server del ERP. El problema era Contains(lista) → OPENJSON('$').
            consulta = consulta.Where(o =>
                o.OrdenNumero.Contains(filtro) ||
                (o.Cliente ?? string.Empty).Contains(filtro) ||
                (o.DescripTrabajo ?? string.Empty).Contains(filtro) ||
                (o.Direccion ?? string.Empty).Contains(filtro) ||
                (o.Contacto ?? string.Empty).Contains(filtro));
        }

        var cabeceras = await consulta
            .OrderByDescending(o => o.FechaOrden)
            .ThenByDescending(o => o.OrdenNumero)
            .Take(Math.Clamp(limite, 1, 500))
            .ToListAsync(cancellationToken);

        if (cabeceras.Count == 0)
        {
            return [];
        }

        var min = cabeceras.Min(o => o.OrdenNumero);
        var max = cabeceras.Max(o => o.OrdenNumero);
        var claves = cabeceras.Select(o => (o.OrdenNumero, o.Plantilla)).ToHashSet();

        var detalles = await ConteoAsync(
            _contexto.OrdenTrabajoDetalle.AsNoTracking().Where(d =>
                string.Compare(d.OrdenNumero, min) >= 0 && string.Compare(d.OrdenNumero, max) <= 0)
                .Select(d => new ConteoFila { OrdenNumero = d.OrdenNumero, Plantilla = d.Plantilla }),
            claves, cancellationToken);
        var servicios = await ConteoAsync(
            _contexto.OrdenTrabajoServicio.AsNoTracking().Where(s =>
                string.Compare(s.OrdenNumero, min) >= 0 && string.Compare(s.OrdenNumero, max) <= 0)
                .Select(s => new ConteoFila { OrdenNumero = s.OrdenNumero, Plantilla = s.Plantilla }),
            claves, cancellationToken);
        var equipos = await ConteoAsync(
            _contexto.OrdenTrabajoEquipo.AsNoTracking().Where(e =>
                string.Compare(e.OrdenNumero, min) >= 0 && string.Compare(e.OrdenNumero, max) <= 0)
                .Select(e => new ConteoFila { OrdenNumero = e.OrdenNumero, Plantilla = e.Plantilla }),
            claves, cancellationToken);
        var tecnicos = await ConteoAsync(
            _contexto.OrdenTrabajoTecnico.AsNoTracking().Where(t =>
                string.Compare(t.OrdenNumero, min) >= 0 && string.Compare(t.OrdenNumero, max) <= 0)
                .Select(t => new ConteoFila { OrdenNumero = t.OrdenNumero, Plantilla = t.Plantilla }),
            claves, cancellationToken);

        var nombres = await NombresClienteAsync(
            cabeceras.Select(o => o.Cliente), cancellationToken);

        return cabeceras.Select(o => new OrdenResumen(
            o,
            o.Cliente is null ? null : nombres.GetValueOrDefault(o.Cliente.Trim()),
            detalles.GetValueOrDefault((o.OrdenNumero, o.Plantilla)),
            servicios.GetValueOrDefault((o.OrdenNumero, o.Plantilla)),
            equipos.GetValueOrDefault((o.OrdenNumero, o.Plantilla)),
            tecnicos.GetValueOrDefault((o.OrdenNumero, o.Plantilla)))).ToList();
    }

    /// <summary>Detalle completo, o <c>null</c> si la clave no existe en el ERP.</summary>
    public async Task<OrdenCompleta?> ObtenerAsync(
        OrdenClave clave,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.OrdenTrabajo.AsNoTracking()
            .FirstOrDefaultAsync(o =>
                o.OrdenNumero == clave.OrdenNumero &&
                o.IdVisita == clave.IdVisita &&
                o.Plantilla == clave.Plantilla,
                cancellationToken);

        if (cabecera is null)
        {
            return null;
        }

        var plantilla = (int)Math.Clamp(clave.Plantilla, int.MinValue, int.MaxValue);

        var detalles = await _contexto.OrdenTrabajoDetalle.AsNoTracking()
            .Where(d => d.OrdenNumero == clave.OrdenNumero && d.Plantilla == plantilla)
            .OrderBy(d => d.Consecutivo).ToListAsync(cancellationToken);
        var servicios = await _contexto.OrdenTrabajoServicio.AsNoTracking()
            .Where(s => s.OrdenNumero == clave.OrdenNumero && s.Plantilla == clave.Plantilla)
            .OrderBy(s => s.Consecutivo).ThenBy(s => s.CodigoServicio)
            .ToListAsync(cancellationToken);
        var equipos = await _contexto.OrdenTrabajoEquipo.AsNoTracking()
            .Where(e => e.OrdenNumero == clave.OrdenNumero && e.Plantilla == plantilla)
            .OrderBy(e => e.Consecutivo).ToListAsync(cancellationToken);
        var tecnicos = await _contexto.OrdenTrabajoTecnico.AsNoTracking()
            .Where(t => t.OrdenNumero == clave.OrdenNumero && t.Plantilla == clave.Plantilla)
            .OrderBy(t => t.Consecutivo).ThenBy(t => t.CodigoTecnico)
            .ToListAsync(cancellationToken);

        var nombres = await NombresClienteAsync([cabecera.Cliente], cancellationToken);
        return new OrdenCompleta(
            cabecera,
            detalles,
            servicios,
            equipos,
            tecnicos,
            cabecera.Cliente is null ? null : nombres.GetValueOrDefault(cabecera.Cliente.Trim()));
    }

    // ── Escritura ─────────────────────────────────────────────────────────────────

    /// <summary>Alta desde una visita confirmada: el SP legacy copia la plantilla y sus hijas,
    /// asigna el consecutivo de 7 dígitos y actualiza <c>U_TIPODOCTOS</c>.</summary>
    public async Task<OrdenCompleta> CrearDesdeVisitaAsync(
        long idVisita,
        CancellationToken cancellationToken = default)
    {
        if (!await _contexto.ProgramacionVisitaConfirmada
                .AnyAsync(v => v.IdVisita == idVisita, cancellationToken))
        {
            throw new KeyNotFoundException($"No existe la visita {idVisita}.");
        }

        var numero = await _generador.GenerarDesdeVisitaAsync(idVisita, cancellationToken)
            ?? throw new InvalidOperationException("No se pudo obtener el número de orden generado.");

        // Una visita puede tener más de una orden (una por plantilla), así que se devuelve la
        // última generada, que es la que acaba de crear el SP.
        var cabecera = await _contexto.OrdenTrabajo.AsNoTracking()
            .Where(o => o.OrdenNumero == numero && o.IdVisita == idVisita)
            .OrderByDescending(o => o.Plantilla)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No se pudo releer la orden generada.");

        return await ObtenerAsync(
            new OrdenClave(cabecera.OrdenNumero, cabecera.IdVisita, cabecera.Plantilla),
            cancellationToken)
            ?? throw new InvalidOperationException("No se pudo releer la orden generada.");
    }

    /// <summary>Edita cabecera e hijas solo si la orden sigue en <c>GE</c>.</summary>
    public async Task<OrdenCompleta> ActualizarAsync(
        OrdenClave clave,
        OrdenGuardar datos,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.OrdenTrabajo
            .FirstOrDefaultAsync(o =>
                o.OrdenNumero == clave.OrdenNumero &&
                o.IdVisita == clave.IdVisita &&
                o.Plantilla == clave.Plantilla,
                cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la orden {clave.OrdenNumero}.");

        ValidarEditable(cabecera.Estado);

        cabecera.Cliente = Limite(datos.Cliente, 20);
        cabecera.FechaOrden = datos.FechaOrden;
        cabecera.Hora = datos.Hora;
        cabecera.Direccion = Limite(datos.Direccion, 100);
        cabecera.Contacto = Limite(datos.Contacto, 35);
        cabecera.Telefono = Limite(datos.Telefono, 10);
        cabecera.Vehiculo = Limite(datos.Vehiculo, 4);
        cabecera.ServicioRealizado = string.IsNullOrWhiteSpace(datos.ServicioRealizado)
            ? "NO"
            : CodigoSiNo(datos.ServicioRealizado);
        cabecera.DescripTrabajo = Limite(datos.DescripTrabajo, 100);
        cabecera.Notas = Limite(datos.Notas, 100);
        cabecera.Instrucciones = datos.Instrucciones;
        // ESTADO, FECHA_CREACION y USUARIO_CREACION no se tocan: son auditoría y ciclo de vida.

        await GuardarHijosAsync(clave, datos, cancellationToken);
        await _contexto.SaveChangesAsync(cancellationToken);

        return await ObtenerAsync(clave, cancellationToken)
            ?? throw new InvalidOperationException("No se pudo releer la orden actualizada.");
    }

    /// <summary>Anulación lógica vía <c>SINCO_ANULAR_ORDENES_DE_TRABAJO</c>. El SP también anula la
    /// requisa asociada y bloquea las órdenes <c>AP</c> y <c>AN</c> con sus mensajes originales.</summary>
    public async Task AnularAsync(
        OrdenClave clave,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.OrdenTrabajo.AsNoTracking()
            .FirstOrDefaultAsync(o =>
                o.OrdenNumero == clave.OrdenNumero &&
                o.IdVisita == clave.IdVisita &&
                o.Plantilla == clave.Plantilla,
                cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la orden {clave.OrdenNumero}.");

        ValidarAnulable(cabecera.Estado);
        await EjecutarSpAsync("dbo.SINCO_ANULAR_ORDENES_DE_TRABAJO", clave.OrdenNumero, cancellationToken);
    }

    /// <summary>Genera la requisa desde una orden <c>GE</c> vía <c>SINCO_GENERA_REQUISA</c>. Es lo
    /// único que lleva la orden a <c>AP</c> en el legacy: no existe SP de aprobación separado.</summary>
    public async Task GenerarRequisaAsync(
        OrdenClave clave,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.OrdenTrabajo.AsNoTracking()
            .FirstOrDefaultAsync(o =>
                o.OrdenNumero == clave.OrdenNumero &&
                o.IdVisita == clave.IdVisita &&
                o.Plantilla == clave.Plantilla,
                cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la orden {clave.OrdenNumero}.");

        if (cabecera.Estado == Anulada)
        {
            throw new InvalidOperationException(MensajeYaAnulada);
        }

        if (cabecera.Estado == Aprobada)
        {
            throw new InvalidOperationException("La orden ya tiene una requisa generada.");
        }

        await EjecutarSpAsync("dbo.SINCO_GENERA_REQUISA", clave.OrdenNumero, cancellationToken);
    }

    // ── Internos ──────────────────────────────────────────────────────────────────

    private const string MensajeAprobada =
        "No puede guardar, ya que esta orden se encuentra aprobada.";
    private const string MensajeYaAnulada =
        "!!! ATENCION !!! ESTA ORDEN DE TRABAJO YA ESTA ANULADA";
    private const string MensajeAnularAprobada =
        "!!! ATENCION !!! ESTA ORDEN DE TRABAJO ESTA APROBADA NO SE PUEDE ANULAR";

    private static void ValidarEditable(string? estado)
    {
        if (estado == Aprobada)
        {
            throw new InvalidOperationException(MensajeAprobada);
        }

        if (estado == Anulada)
        {
            throw new InvalidOperationException(MensajeYaAnulada);
        }
    }

    private static void ValidarAnulable(string? estado)
    {
        if (estado == Anulada)
        {
            throw new InvalidOperationException(MensajeYaAnulada);
        }

        if (estado == Aprobada)
        {
            throw new InvalidOperationException(MensajeAnularAprobada);
        }
    }

    private async Task EjecutarSpAsync(
        string procedimiento,
        string ordenNumero,
        CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.Database.ExecuteSqlRawAsync(
                $"EXEC {procedimiento} @p0",
                new object[] { ordenNumero },
                cancellationToken);
        }
        catch (SqlException ex)
        {
            // El SP valida con RAISERROR y ese texto es contrato observable del WinForms: se
            // reenvía tal cual para que el usuario vea el mismo mensaje de siempre.
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private sealed class ConteoFila
    {
        public string OrdenNumero { get; set; } = null!;
        public long Plantilla { get; set; }
    }

    private async Task<Dictionary<(string, long), int>> ConteoAsync(
        IQueryable<ConteoFila> filas,
        HashSet<(string OrdenNumero, long Plantilla)> claves,
        CancellationToken cancellationToken)
    {
        // El group by se hace en SQL por rango de ORDEN_NUMERO y el filtro de claves en memoria:
        // un IN con cientos de números de orden dispara el OPENJSON('$') que falla en el ERP.
        var agrupados = await filas
            .GroupBy(f => new { f.OrdenNumero, f.Plantilla })
            .Select(g => new { g.Key.OrdenNumero, g.Key.Plantilla, Total = g.Count() })
            .ToListAsync(cancellationToken);

        return agrupados
            .Where(g => claves.Contains((g.OrdenNumero, g.Plantilla)))
            .ToDictionary(g => (g.OrdenNumero, g.Plantilla), g => g.Total);
    }

    private async Task<IReadOnlyDictionary<string, string>> NombresClienteAsync(
        IEnumerable<string?> codigos,
        CancellationToken cancellationToken)
    {
        var lista = codigos
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (lista.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // Rango en SQL + conjuntos en memoria: evita el OPENJSON('$') que falla en el ERP.
        var min = lista.Min();
        var max = lista.Max();
        var clientes = await _contexto.VistaClienteErp.AsNoTracking()
            .Where(v => v.Activo == ActivoCorto &&
                        string.Compare(v.Cliente, min) >= 0 &&
                        string.Compare(v.Cliente, max) <= 0)
            .Select(v => new { Codigo = v.Cliente.Trim(), Nombre = v.NombreCliente })
            .ToListAsync(cancellationToken);

        return clientes
            .Where(v => lista.Contains(v.Codigo, StringComparer.OrdinalIgnoreCase))
            .GroupBy(v => v.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(v => v.Nombre)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))?.Trim() ?? g.Key,
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task GuardarHijosAsync(
        OrdenClave clave,
        OrdenGuardar datos,
        CancellationToken cancellationToken)
    {
        var orden = clave.OrdenNumero;
        var plantillaLarga = clave.Plantilla;
        var plantilla = (int)Math.Clamp(plantillaLarga, int.MinValue, int.MaxValue);

        var detallesActuales = await _contexto.OrdenTrabajoDetalle
            .Where(d => d.OrdenNumero == orden && d.Plantilla == plantilla)
            .OrderBy(d => d.Consecutivo).ToListAsync(cancellationToken);
        var serviciosActuales = await _contexto.OrdenTrabajoServicio
            .Where(s => s.OrdenNumero == orden && s.Plantilla == plantillaLarga)
            .OrderBy(s => s.Consecutivo).ToListAsync(cancellationToken);
        var equiposActuales = await _contexto.OrdenTrabajoEquipo
            .Where(e => e.OrdenNumero == orden && e.Plantilla == plantilla)
            .OrderBy(e => e.Consecutivo).ToListAsync(cancellationToken);
        var tecnicosActuales = await _contexto.OrdenTrabajoTecnico
            .Where(t => t.OrdenNumero == orden && t.Plantilla == plantillaLarga)
            .OrderBy(t => t.Consecutivo).ToListAsync(cancellationToken);

        var detallesNuevos = datos.Detalles.OrderBy(d => d.Consecutivo).ToList();
        for (var i = 0; i < detallesNuevos.Count; i++)
        {
            var origen = detallesNuevos[i];
            var consecutivo = i + 1;
            var destino = detallesActuales.ElementAtOrDefault(i);
            if (destino is null)
            {
                destino = new OrdenTrabajoDetalle
                {
                    OrdenNumero = orden,
                    Plantilla = plantilla,
                    Consecutivo = consecutivo
                };
                _contexto.OrdenTrabajoDetalle.Add(destino);
            }

            destino.Consecutivo = consecutivo;
            destino.CodigoArticulo = Limite(origen.CodigoArticulo, 10);
            destino.DescripArticulo = Limite(origen.DescripArticulo, 50);
            destino.TipoPlaga = Limite(origen.TipoPlaga, 4);
            destino.LugarTratar = Limite(origen.LugarTratar, 100);
            destino.Area = origen.Area is { } area ? Math.Round(area, 2) : null;
            destino.Concentracion = Limite(origen.Concentracion, 20);
            destino.CodigoDosificacion = Limite(origen.CodigoDosificacion, 4);
            destino.MetodoAplicacion = Limite(origen.MetodoAplicacion, 4);
            destino.Cantidad = origen.Cantidad is { } cantidad ? Math.Round(cantidad, 0) : null;
            destino.Umedida = Limite(origen.Umedida, 6);
            destino.CostoUnitario = origen.CostoUnitario is { } unitario ? Math.Round(unitario, 6) : null;
            // El legacy calculaba el total en el formulario: CANTIDAD × COSTO_UNITARIO.
            destino.CostoTotal = Math.Round((origen.Cantidad ?? 0) * (origen.CostoUnitario ?? 0), 2);
        }

        var serviciosNuevos = datos.Servicios.OrderBy(s => s.Consecutivo).ToList();
        for (var i = 0; i < serviciosNuevos.Count; i++)
        {
            var origen = serviciosNuevos[i];
            var consecutivo = i + 1;
            var codigoServicio = Limite(origen.CodigoServicio, 4)
                ?? throw new InvalidOperationException("El servicio requiere código de servicio.");
            var codigoFrecuencia = Limite(origen.CodigoFrecuencia, 4)
                ?? throw new InvalidOperationException("El servicio requiere frecuencia.");
            var destino = serviciosActuales.ElementAtOrDefault(i);

            // Los códigos son parte de la PK: si cambian, se sustituye la fila en vez de intentar
            // modificar la clave de una entidad rastreada.
            if (destino is not null &&
                (destino.CodigoServicio != codigoServicio || destino.CodigoFrecuencia != codigoFrecuencia))
            {
                _contexto.OrdenTrabajoServicio.Remove(destino);
                destino = null;
            }

            if (destino is null)
            {
                _contexto.OrdenTrabajoServicio.Add(new OrdenTrabajoServicio
                {
                    OrdenNumero = orden,
                    Plantilla = plantillaLarga,
                    Consecutivo = consecutivo,
                    CodigoServicio = codigoServicio,
                    CodigoFrecuencia = codigoFrecuencia
                });
                continue;
            }

            destino.Consecutivo = consecutivo;
            destino.CodigoServicio = codigoServicio;
            destino.CodigoFrecuencia = codigoFrecuencia;
        }

        var equiposNuevos = datos.Equipos.OrderBy(e => e.Consecutivo).ToList();
        for (var i = 0; i < equiposNuevos.Count; i++)
        {
            var origen = equiposNuevos[i];
            var consecutivo = i + 1;
            var destino = equiposActuales.ElementAtOrDefault(i);
            if (destino is null)
            {
                destino = new OrdenTrabajoEquipo
                {
                    OrdenNumero = orden,
                    Plantilla = plantilla,
                    Consecutivo = consecutivo
                };
                _contexto.OrdenTrabajoEquipo.Add(destino);
            }

            destino.Consecutivo = consecutivo;
            destino.CodigoEquipo = Limite(origen.CodigoEquipo, 4);
            destino.Utilizado = string.IsNullOrWhiteSpace(origen.Utilizado)
                ? "NO"
                : CodigoSiNo(origen.Utilizado);
        }

        var tecnicosNuevos = datos.Tecnicos.OrderBy(t => t.Consecutivo).ToList();
        for (var i = 0; i < tecnicosNuevos.Count; i++)
        {
            var origen = tecnicosNuevos[i];
            var consecutivo = i + 1;
            var codigoTecnico = Limite(origen.CodigoTecnico, 4)
                ?? throw new InvalidOperationException("El técnico requiere código.");
            var iniciales = Limite(origen.Iniciales, 2)
                ?? throw new InvalidOperationException("El técnico requiere iniciales.");
            var destino = tecnicosActuales.ElementAtOrDefault(i);

            if (destino is not null &&
                (destino.CodigoTecnico != codigoTecnico || destino.Iniciales != iniciales))
            {
                _contexto.OrdenTrabajoTecnico.Remove(destino);
                destino = null;
            }

            if (destino is null)
            {
                _contexto.OrdenTrabajoTecnico.Add(new OrdenTrabajoTecnico
                {
                    OrdenNumero = orden,
                    Plantilla = plantillaLarga,
                    Consecutivo = consecutivo,
                    CodigoTecnico = codigoTecnico,
                    Iniciales = iniciales
                });
                continue;
            }

            destino.Consecutivo = consecutivo;
            destino.CodigoTecnico = codigoTecnico;
            destino.Iniciales = iniciales;
        }

        _contexto.OrdenTrabajoDetalle.RemoveRange(detallesActuales.Skip(detallesNuevos.Count));
        _contexto.OrdenTrabajoServicio.RemoveRange(serviciosActuales.Skip(serviciosNuevos.Count));
        _contexto.OrdenTrabajoEquipo.RemoveRange(equiposActuales.Skip(equiposNuevos.Count));
        _contexto.OrdenTrabajoTecnico.RemoveRange(tecnicosActuales.Skip(tecnicosNuevos.Count));
    }

    private static string CodigoSiNo(string valor)
        => valor.Trim().StartsWith("S", StringComparison.OrdinalIgnoreCase) ? "SI" : "NO";

    private static string? Limite(string? valor, int maximo)
        => valor is null ? null : (valor.Trim() is { Length: > 0 } t ? t[..Math.Min(t.Length, maximo)] : null);
}
