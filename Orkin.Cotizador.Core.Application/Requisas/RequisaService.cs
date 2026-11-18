using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Ordenes;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Requisas;

/// <summary>Fila de la grilla: cabecera legacy, nombres legibles y bodega de la primera línea.</summary>
public sealed record RequisaResumen(
    Requisa Cabecera,
    string? ClienteNombre,
    string? LocacionNombre,
    string? Bodega,
    int Lineas,
    int Tecnicos);

/// <summary>Requisa completa sobre las tres tablas originales.</summary>
public sealed record RequisaCompleta(
    Requisa Cabecera,
    IReadOnlyList<RequisaDetalle> Lineas,
    IReadOnlyList<RequisaTecnico> Tecnicos,
    string? ClienteNombre,
    string? LocacionNombre,
    string? Bodega,
    IReadOnlyDictionary<string, string> NombresTecnico);

/// <summary>Datos editables. La clave, el consecutivo y los importes calculados los resuelve el
/// servicio, igual que el WinForms: el formulario nunca elige <c>CONSECUTIVO</c>, <c>SOBFAL</c>
/// ni <c>COSTO_TOTAL</c>.</summary>
public sealed record RequisaGuardar(
    string? Cliente,
    DateOnly? FechaRequisa,
    DateOnly? FechaOrden,
    string? Locacion,
    string? Instrucciones,
    string? Bodega,
    IReadOnlyList<RequisaDetalle> Lineas,
    IReadOnlyList<RequisaTecnico> Tecnicos);

/// <summary>
/// Gestión de requisas sobre las <b>tres tablas legacy existentes</b> (D-22): <c>Requisa</c>,
/// <c>Requisa_Detalle</c> y <c>Requisa_Tecnicos</c>.
/// <para>La requisa ya fue creada por <c>SINCO_GENERA_REQUISA</c> cuando la Fase 4 generó la orden;
/// acá no se vuelve a ejecutar ese SP. La opción «Nueva desde orden» delega en
/// <see cref="OrdenTrabajoService.GenerarRequisaAsync"/>, que es el mismo flujo de siempre, y la
/// anulación delega en <see cref="OrdenTrabajoService.AnularAsync"/>. No hay SP ni tabla nueva.</para>
/// </summary>
public sealed class RequisaService
{
    private const string Generada = "GE";
    private const string Aprobada = "AP";
    private const string Anulada = "AN";

    private readonly CotizadorDbContext _contexto;
    private readonly OrdenTrabajoService _ordenes;

    public RequisaService(CotizadorDbContext contexto, OrdenTrabajoService ordenes)
    {
        _contexto = contexto;
        _ordenes = ordenes;
    }

    // ── Lectura ───────────────────────────────────────────────────────────────────

    /// <summary>Listado del modo <c>RequisaGenerada</c>, acotado por <paramref name="limite"/> porque
    /// el ERP tiene más de 42.000 requisas y la pantalla no necesita cargarlas todas.</summary>
    public async Task<IReadOnlyList<RequisaResumen>> ListarAsync(
        string? texto = null,
        string? cliente = null,
        string? estado = null,
        DateOnly? desde = null,
        DateOnly? hasta = null,
        int limite = 200,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.Requisa.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(cliente))
        {
            consulta = consulta.Where(r => r.Cliente == cliente.Trim());
        }

        var filtroEstado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (filtroEstado is not null)
        {
            consulta = consulta.Where(r => r.Estado == filtroEstado);
        }

        if (desde is { } d)
        {
            consulta = consulta.Where(r => r.FechaRequisa >= d);
        }

        if (hasta is { } h)
        {
            consulta = consulta.Where(r => r.FechaRequisa <= h);
        }

        var filtro = texto?.Trim();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            // Contains sobre un parámetro se traduce a CHARINDEX y funciona en el ERP. El problema
            // de compatibilidad era Contains(lista) → OPENJSON('$'), que no se usa acá.
            consulta = consulta.Where(r =>
                r.RequisaNumero.Contains(filtro) ||
                (r.OrdenNumero ?? string.Empty).Contains(filtro) ||
                (r.Cliente ?? string.Empty).Contains(filtro) ||
                (r.Instrucciones ?? string.Empty).Contains(filtro));
        }

        var cabeceras = await consulta
            .OrderByDescending(r => r.FechaRequisa)
            .ThenByDescending(r => r.RequisaNumero)
            .Take(Math.Clamp(limite, 1, 500))
            .ToListAsync(cancellationToken);

        if (cabeceras.Count == 0)
        {
            return [];
        }

        var min = cabeceras.Min(r => r.RequisaNumero) ?? string.Empty;
        var max = cabeceras.Max(r => r.RequisaNumero) ?? string.Empty;

        var lineas = await ResumenLineasAsync(min, max, cancellationToken);
        var tecnicos = await ConteoTecnicosAsync(min, max, cancellationToken);
        var nombresCliente = await NombresClienteAsync(cabeceras.Select(r => r.Cliente), cancellationToken);
        var nombresLocacion = await NombresLocacionAsync(cabeceras, cancellationToken);

        return cabeceras.Select(r =>
        {
            var resumenLineas = lineas.GetValueOrDefault(r.RequisaNumero);
            return new RequisaResumen(
                r,
                r.Cliente is null ? null : nombresCliente.GetValueOrDefault(r.Cliente),
                nombresLocacion.GetValueOrDefault((r.Cliente, r.Locacion)),
                resumenLineas?.Bodega,
                resumenLineas?.Total ?? 0,
                tecnicos.GetValueOrDefault(r.RequisaNumero));
        }).ToList();
    }

    /// <summary>Detalle completo, o <c>null</c> si el número no existe en el ERP.</summary>
    public async Task<RequisaCompleta?> ObtenerAsync(
        string numero,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.Requisa.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequisaNumero == numero, cancellationToken);

        if (cabecera is null)
        {
            return null;
        }

        var lineas = await _contexto.RequisaDetalle.AsNoTracking()
            .Where(d => d.RequisaNumero == numero)
            .OrderBy(d => d.Consecutivo).ToListAsync(cancellationToken);
        var tecnicos = await _contexto.RequisaTecnico.AsNoTracking()
            .Where(t => t.RequisaNumero == numero)
            .OrderBy(t => t.Consecutivo).ThenBy(t => t.CodigoTecnico)
            .ToListAsync(cancellationToken);

        var nombresCliente = await NombresClienteAsync([cabecera.Cliente], cancellationToken);
        var nombresLocacion = await NombresLocacionAsync([cabecera], cancellationToken);
        var nombresTecnico = await NombresTecnicoAsync(
            tecnicos.Select(t => t.CodigoTecnico), cancellationToken);

        return new RequisaCompleta(
            cabecera,
            lineas,
            tecnicos,
            cabecera.Cliente is null ? null : nombresCliente.GetValueOrDefault(cabecera.Cliente),
            nombresLocacion.GetValueOrDefault((cabecera.Cliente, cabecera.Locacion)),
            lineas.FirstOrDefault()?.Bodega,
            nombresTecnico);
    }

    // ── Escritura ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Alta reutilizando el flujo existente de orden de trabajo: el mismo
    /// <c>SINCO_GENERA_REQUISA</c> que ya usa la Fase 4, sin duplicar lógica ni SP. Si la orden ya
    /// tiene requisa, el servicio de órdenes devuelve el mensaje legacy tal cual.
    /// </summary>
    public async Task<RequisaCompleta> CrearDesdeOrdenAsync(
        OrdenClave clave,
        CancellationToken cancellationToken = default)
    {
        await _ordenes.GenerarRequisaAsync(clave, cancellationToken);

        return await ObtenerAsync(clave.OrdenNumero, cancellationToken)
            ?? throw new InvalidOperationException("No se pudo releer la requisa generada.");
    }

    /// <summary>Edita cabecera, líneas y técnicos solo mientras la requisa sigue en <c>GE</c>.</summary>
    public async Task<RequisaCompleta> ActualizarAsync(
        string numero,
        RequisaGuardar datos,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.Requisa
            .FirstOrDefaultAsync(r => r.RequisaNumero == numero, cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la requisa {numero}.");

        ValidarEditable(cabecera.Estado);
        await ValidarAsync(datos, cancellationToken);

        cabecera.Cliente = Limite(datos.Cliente, 20);
        cabecera.FechaRequisa = datos.FechaRequisa ?? cabecera.FechaRequisa;
        cabecera.FechaOrden = datos.FechaOrden;
        cabecera.Locacion = Limite(datos.Locacion, 5);
        cabecera.Instrucciones = datos.Instrucciones;
        // ESTADO, ORDEN_NUMERO, REQUISA_PENDIENTE y la auditoría no se tocan: son del SP legacy.

        await GuardarHijosAsync(numero, datos, cancellationToken);
        await _contexto.SaveChangesAsync(cancellationToken);

        return await ObtenerAsync(numero, cancellationToken)
            ?? throw new InvalidOperationException("No se pudo releer la requisa actualizada.");
    }

    /// <summary>
    /// Anula la requisa delegando en <see cref="OrdenTrabajoService.AnularAsync"/>, que invoca
    /// <c>SINCO_ANULAR_ORDENES_DE_TRABAJO</c> con la orden relacionada. Ese SP pone en <c>AN</c> la
    /// orden y la requisa; no se crea otro SP y no se borra ninguna fila.
    /// </summary>
    public async Task AnularAsync(string numero, CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.Requisa.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequisaNumero == numero, cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la requisa {numero}.");

        var ordenNumero = cabecera.OrdenNumero ?? numero;
        var orden = await _contexto.OrdenTrabajo.AsNoTracking()
            .Where(o => o.OrdenNumero == ordenNumero)
            .OrderBy(o => o.Plantilla)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la orden {ordenNumero} asociada a la requisa.");

        await _ordenes.AnularAsync(
            new OrdenClave(orden.OrdenNumero, orden.IdVisita, orden.Plantilla),
            cancellationToken);
    }

    // ── Reglas del WinForms ────────────────────────────────────────────────────────

    private const string MensajeAprobada =
        "No puede guardar, ya que esta requisa se encuentra aprobada.";
    private const string MensajeBodega = "Debe seleccionar una bodega valida.";

    private static void ValidarEditable(string? estado)
    {
        // El WinForms bloquea AP y AN al abrir la requisa; el único mensaje de guardado que define
        // es este, así que se reutiliza para ambos sin inventar un texto nuevo.
        if (estado is Aprobada or Anulada)
        {
            throw new InvalidOperationException(MensajeAprobada);
        }
    }

    private async Task ValidarAsync(
        RequisaGuardar datos,
        CancellationToken cancellationToken)
    {
        if (datos.Lineas.Count == 0)
        {
            throw new InvalidOperationException("Debe establecer un producto para esta plantilla.");
        }

        if (string.IsNullOrWhiteSpace(datos.Cliente))
        {
            throw new InvalidOperationException("Debe seleccionar un cliente.");
        }

        if (datos.Tecnicos.Count == 0)
        {
            throw new InvalidOperationException("Debe seleccionar al menos un técnico.");
        }

        var bodega = Limite(datos.Bodega, 4);
        if (bodega is null)
        {
            throw new InvalidOperationException(MensajeBodega);
        }

        var bodegasValidas = await _contexto.VistaBodegaErp.AsNoTracking()
            .Where(b => b.Activo == "SI")
            .Select(b => b.Codigo)
            .ToListAsync(cancellationToken);

        if (!bodegasValidas.Contains(bodega))
        {
            throw new InvalidOperationException(MensajeBodega);
        }

        var cliente = datos.Cliente.Trim();
        var locacion = Limite(datos.Locacion, 5);
        var existeLocacion = locacion is not null && await _contexto.VistaLocacionErp.AsNoTracking()
            .AnyAsync(l => l.Codigo == locacion && l.Cliente == cliente, cancellationToken);

        if (!existeLocacion)
        {
            throw new InvalidOperationException(
                "La locación no existe para este cliente, por favor valide en su ERP que exista la locación " +
                (locacion ?? string.Empty) + " para el cliente " + cliente + ".");
        }

        foreach (var linea in datos.Lineas)
        {
            if (linea.CantidadEntregada is < 0)
            {
                throw new InvalidOperationException("La cantidad entregada no puede ser menor a cero");
            }

            if (linea.CantidadUtilizada is < 0)
            {
                throw new InvalidOperationException("La cantidad utilizada no puede ser menor a cero");
            }

            // Bug heredado de FormDetalleRequisa: el mensaje de "devuelta" se dispara con la
            // cantidad utilizada. Se conserva (y documenta) para no cambiar el comportamiento.
            if (linea.CantidadUtilizada is < 0)
            {
                throw new InvalidOperationException("La cantidad devuelta no puede ser menor a cero");
            }

            // Cálculo legacy: (entregada - utilizada - devuelta) * -1. El signo está invertido en
            // el WinForms original; se reproduce tal cual para no alterar lo que ve el usuario.
            linea.CantidadSobfal = Math.Round(
                ((linea.CantidadEntregada ?? 0) - (linea.CantidadUtilizada ?? 0) -
                 (linea.CantidadDevuelta ?? 0)) * -1, 0);
            linea.CostoTotal = Math.Round(
                (linea.CantidadUtilizada ?? 0) * (linea.CostoUnitario ?? 0), 2);
        }
    }

    private async Task GuardarHijosAsync(
        string numero,
        RequisaGuardar datos,
        CancellationToken cancellationToken)
    {
        var bodega = Limite(datos.Bodega, 4);
        var lineasActuales = await _contexto.RequisaDetalle
            .Where(d => d.RequisaNumero == numero)
            .OrderBy(d => d.Consecutivo).ToListAsync(cancellationToken);
        var tecnicosActuales = await _contexto.RequisaTecnico
            .Where(t => t.RequisaNumero == numero)
            .OrderBy(t => t.Consecutivo).ToListAsync(cancellationToken);

        // Las listas originales se conservan para saber qué filas hay que borrar: las nuevas se
        // agregan a las listas de trabajo y no deben entrar en el RemoveRange.
        var lineasOriginales = lineasActuales.ToList();
        var tecnicosOriginales = tecnicosActuales.ToList();

        var maximoLinea = lineasActuales.Count == 0 ? 0 : lineasActuales.Max(d => d.Consecutivo);
        foreach (var origen in datos.Lineas)
        {
            var destino = lineasActuales.FirstOrDefault(d => d.Consecutivo == origen.Consecutivo);
            if (destino is null)
            {
                destino = new RequisaDetalle
                {
                    RequisaNumero = numero,
                    Consecutivo = ++maximoLinea
                };
                lineasActuales.Add(destino);
                _contexto.RequisaDetalle.Add(destino);
            }

            destino.Bodega = bodega;
            destino.CodigoArticulo = Limite(origen.CodigoArticulo, 10);
            destino.DescripArticulo = Limite(origen.DescripArticulo, 50);
            destino.CantidadEntregada = RedondearEntero(origen.CantidadEntregada);
            destino.CantidadUtilizada = RedondearEntero(origen.CantidadUtilizada);
            destino.CantidadDevuelta = RedondearEntero(origen.CantidadDevuelta);
            destino.CantidadSobfal = RedondearEntero(origen.CantidadSobfal);
            destino.CostoUnitario = origen.CostoUnitario is { } unitario ? Math.Round(unitario, 6) : null;
            destino.CostoTotal = origen.CostoTotal is { } total ? Math.Round(total, 2) : null;
            destino.CentroCosto = Limite(origen.CentroCosto, 25);
            destino.CuentaContable = Limite(origen.CuentaContable, 25);
            // UMEDIDA no está en el formulario de edición: se conserva la del SP legacy.
        }

        var maximoTecnico = tecnicosActuales.Count == 0 ? 0 : tecnicosActuales.Max(t => t.Consecutivo);
        foreach (var origen in datos.Tecnicos)
        {
            var codigo = Limite(origen.CodigoTecnico, 4)
                ?? throw new InvalidOperationException("El técnico requiere código.");
            var iniciales = Limite(origen.Iniciales, 2)
                ?? throw new InvalidOperationException("El técnico requiere iniciales.");
            var destino = tecnicosActuales.FirstOrDefault(t =>
                t.CodigoTecnico == codigo && t.Iniciales == iniciales);

            if (destino is null)
            {
                destino = new RequisaTecnico
                {
                    RequisaNumero = numero,
                    Consecutivo = ++maximoTecnico,
                    CodigoTecnico = codigo,
                    Iniciales = iniciales
                };
                tecnicosActuales.Add(destino);
                _contexto.RequisaTecnico.Add(destino);
                continue;
            }

            destino.Iniciales = iniciales;
        }

        var lineasEnviadas = datos.Lineas.Select(l => l.Consecutivo).ToHashSet();
        var tecnicosEnviados = datos.Tecnicos
            .Select(t => (Limite(t.CodigoTecnico, 4), Limite(t.Iniciales, 2)))
            .ToHashSet();

        _contexto.RequisaDetalle.RemoveRange(
            lineasOriginales.Where(d => !lineasEnviadas.Contains(d.Consecutivo)));
        _contexto.RequisaTecnico.RemoveRange(
            tecnicosOriginales.Where(t => !tecnicosEnviados.Contains((t.CodigoTecnico, t.Iniciales))));
    }

    // ── Consultas auxiliares (rango + agrupación en memoria) ────────────────────────

    private sealed record ResumenLineas(int Total, string? Bodega);

    private async Task<Dictionary<string, ResumenLineas>> ResumenLineasAsync(
        string min,
        string max,
        CancellationToken cancellationToken)
    {
        var filas = await _contexto.RequisaDetalle.AsNoTracking()
            .Where(d => string.Compare(d.RequisaNumero, min) >= 0 &&
                        string.Compare(d.RequisaNumero, max) <= 0)
            .OrderBy(d => d.RequisaNumero).ThenBy(d => d.Consecutivo)
            .Select(d => new { d.RequisaNumero, d.Bodega })
            .ToListAsync(cancellationToken);

        return filas.GroupBy(f => f.RequisaNumero).ToDictionary(
            g => g.Key,
            g => new ResumenLineas(g.Count(), g.First().Bodega));
    }

    private async Task<Dictionary<string, int>> ConteoTecnicosAsync(
        string min,
        string max,
        CancellationToken cancellationToken)
    {
        var agrupados = await _contexto.RequisaTecnico.AsNoTracking()
            .Where(t => string.Compare(t.RequisaNumero, min) >= 0 &&
                        string.Compare(t.RequisaNumero, max) <= 0)
            .GroupBy(t => t.RequisaNumero)
            .Select(g => new { Numero = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        return agrupados.ToDictionary(g => g.Numero, g => g.Total);
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

        var min = lista.Min();
        var max = lista.Max();
        var clientes = await _contexto.Cliente.AsNoTracking()
            .Where(c => string.Compare(c.Codigo, min) >= 0 && string.Compare(c.Codigo, max) <= 0)
            .Select(c => new { c.Codigo, c.Nombre })
            .ToListAsync(cancellationToken);

        return clientes.Where(c => lista.Contains(c.Codigo))
            .GroupBy(c => c.Codigo)
            .ToDictionary(g => g.Key, g => g.First().Nombre);
    }

    private async Task<IReadOnlyDictionary<(string? Cliente, string? Locacion), string>> NombresLocacionAsync(
        IEnumerable<Requisa> cabeceras,
        CancellationToken cancellationToken)
    {
        var codigos = cabeceras
            .Where(r => !string.IsNullOrWhiteSpace(r.Locacion))
            .Select(r => r.Locacion!)
            .Distinct().ToList();
        if (codigos.Count == 0)
        {
            return new Dictionary<(string?, string?), string>();
        }

        var min = codigos.Min();
        var max = codigos.Max();
        var locaciones = await _contexto.VistaLocacionErp.AsNoTracking()
            .Where(l => string.Compare(l.Codigo, min) >= 0 && string.Compare(l.Codigo, max) <= 0)
            .Select(l => new { l.Codigo, l.Cliente, l.Nombre })
            .ToListAsync(cancellationToken);

        return cabeceras
            .Select(r => (Clave: (r.Cliente, r.Locacion), Nombre: locaciones
                .FirstOrDefault(l => l.Codigo == r.Locacion && l.Cliente == r.Cliente)?.Nombre))
            .Where(x => !string.IsNullOrWhiteSpace(x.Nombre))
            .GroupBy(x => x.Clave)
            .ToDictionary(g => g.Key, g => g.First().Nombre!);
    }

    private async Task<IReadOnlyDictionary<string, string>> NombresTecnicoAsync(
        IEnumerable<string> codigos,
        CancellationToken cancellationToken)
    {
        var lista = codigos.Distinct().ToList();
        if (lista.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var min = lista.Min();
        var max = lista.Max();
        var tecnicos = await _contexto.Tecnico.AsNoTracking()
            .Where(t => string.Compare(t.Codigo, min) >= 0 && string.Compare(t.Codigo, max) <= 0)
            .Select(t => new { t.Codigo, t.Nombre })
            .ToListAsync(cancellationToken);

        return tecnicos.Where(t => lista.Contains(t.Codigo))
            .GroupBy(t => t.Codigo)
            .ToDictionary(g => g.Key, g => g.First().Nombre);
    }

    private static decimal? RedondearEntero(decimal? valor)
        => valor is { } v ? Math.Round(v, 0) : null;

    private static string? Limite(string? valor, int maximo)
        => valor is null ? null : (valor.Trim() is { Length: > 0 } t ? t[..Math.Min(t.Length, maximo)] : null);
}
