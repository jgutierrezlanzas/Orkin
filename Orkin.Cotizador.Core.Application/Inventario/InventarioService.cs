using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Inventario;

/// <summary>Fila de la grilla del modo <c>DocumentoInventario</c> del WinForms.</summary>
public sealed record DocumentoResumen(
    DocumentoInventario Cabecera,
    int Lineas,
    string? Bodega,
    string? NombreBodega);

/// <summary>Detalle por artículo con la descripción real de <c>vvArticulos</c> y <c>vvBodegas</c>.</summary>
public sealed record DocumentoLineaDetalle(
    int Linea,
    string Articulo,
    string? DescripcionArticulo,
    string? Unidad,
    string? Bodega,
    string? NombreBodega,
    string? BodegaDestino,
    string? NombreBodegaDestino,
    decimal Cantidad,
    decimal CostoUnitario,
    decimal CostoTotalLocal,
    string? CentroCosto,
    string? CuentaContable);

/// <summary>Documento completo sobre las tablas legacy, con nombres legibles.</summary>
public sealed record DocumentoCompleto(
    DocumentoInventario Cabecera,
    IReadOnlyList<DocumentoLineaDetalle> Lineas);

/// <summary>
/// Fase 6: documentos de inventario sobre <c>dbo.Documento_Inv</c> y
/// <c>dbo.Documento_Inv_linea</c>, las tablas que ya existen en el ERP (D-22). La generación y el
/// traslado a Exactus no se reimplementan: se delegan en <see cref="IExactusService"/>, que envuelve
/// los SPs originales con parámetros y registra su resultado.
/// </summary>
public sealed class InventarioService
{
    private readonly CotizadorDbContext _contexto;
    private readonly IExactusService _exactus;

    public InventarioService(CotizadorDbContext contexto, IExactusService exactus)
    {
        _contexto = contexto;
        _exactus = exactus;
    }

    // ── Lectura ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Listado del WinForms, acotado por <paramref name="limite"/> porque el ERP tiene más de
    /// 40.000 documentos. <paramref name="transferido"/> acepta <c>SI</c>, <c>NO</c> o
    /// <c>Todos</c>, exactamente las tres opciones del combo legacy.
    /// </summary>
    public async Task<IReadOnlyList<DocumentoResumen>> ListarAsync(
        string? texto = null,
        string? transferido = null,
        DateOnly? desde = null,
        DateOnly? hasta = null,
        int limite = 200,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.DocumentoInventario.AsNoTracking();

        var filtroTransferido = transferido?.Trim().ToUpperInvariant();
        if (filtroTransferido is "SI" or "NO")
        {
            consulta = consulta.Where(d => d.Transferido == filtroTransferido);
        }

        if (desde is { } d)
        {
            var inicio = d.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(doc => doc.FechaDocumento >= inicio);
        }

        if (hasta is { } h)
        {
            var fin = h.ToDateTime(TimeOnly.MaxValue);
            consulta = consulta.Where(doc => doc.FechaDocumento <= fin);
        }

        var filtro = texto?.Trim();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            // Contains sobre un parámetro se traduce a CHARINDEX; se evita Contains(lista), que en
            // algunos proveedores se convierte en OPENJSON('$').
            consulta = consulta.Where(doc =>
                doc.PaqueteInventario.Contains(filtro) ||
                doc.DocumentoInv.Contains(filtro) ||
                (doc.Consecutivo ?? string.Empty).Contains(filtro) ||
                doc.RequisaNumero.Contains(filtro) ||
                doc.Referencia.Contains(filtro) ||
                doc.Usuario.Contains(filtro) ||
                (doc.MensajeSistema ?? string.Empty).Contains(filtro));
        }

        var cabeceras = await consulta
            .OrderByDescending(doc => doc.FechaDocumento)
            .ThenByDescending(doc => doc.DocumentoInv)
            .Take(Math.Clamp(limite, 1, 500))
            .ToListAsync(cancellationToken);

        if (cabeceras.Count == 0)
        {
            return [];
        }

        var min = cabeceras.Min(doc => doc.DocumentoInv);
        var max = cabeceras.Max(doc => doc.DocumentoInv);
        var resumenLineas = await ResumenLineasAsync(min, max, cancellationToken);
        var nombresBodega = await NombresBodegaAsync(
            resumenLineas.Values.Select(l => l.Bodega), cancellationToken);

        return cabeceras.Select(doc =>
        {
            var lineas = resumenLineas.GetValueOrDefault(doc.DocumentoInv);
            return new DocumentoResumen(
                doc,
                lineas?.Total ?? 0,
                lineas?.Bodega,
                lineas?.Bodega is { } codigo ? nombresBodega.GetValueOrDefault(codigo) : null);
        }).ToList();
    }

    /// <summary>Detalle por artículo, o <c>null</c> si el documento no existe en el ERP.</summary>
    public async Task<DocumentoCompleto?> ObtenerAsync(
        string paqueteInventario,
        string documentoInv,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.DocumentoInventario.AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.PaqueteInventario == paqueteInventario && d.DocumentoInv == documentoInv,
                cancellationToken);

        if (cabecera is null)
        {
            return null;
        }

        var lineasLegacy = await _contexto.DocumentoInventarioLinea.AsNoTracking()
            .Where(l => l.PaqueteInventario == paqueteInventario && l.DocumentoInv == documentoInv)
            .ToListAsync(cancellationToken);

        var nombresArticulo = await NombresArticuloAsync(
            lineasLegacy.Select(l => l.Articulo), cancellationToken);
        var nombresBodega = await NombresBodegaAsync(
            lineasLegacy.Select(l => l.Bodega).Concat(lineasLegacy.Select(l => l.BodegaDestino)),
            cancellationToken);

        var lineas = lineasLegacy.Select(l =>
        {
            var articulo = nombresArticulo.GetValueOrDefault(l.Articulo);
            // El detalle legacy calcula el unitario como COSTO_TOTAL_LOCAL / CANTIDAD, no lee la
            // columna COSTO_UNITARIO (que además es anulable en la tabla real).
            var unitario = l.Cantidad == 0 ? 0m : l.CostoTotalLocal / l.Cantidad;
            return new DocumentoLineaDetalle(
                l.LineaDocInv,
                l.Articulo,
                articulo?.Descripcion,
                articulo?.Unidad,
                l.Bodega,
                l.Bodega is null ? null : nombresBodega.GetValueOrDefault(l.Bodega),
                l.BodegaDestino,
                l.BodegaDestino is null ? null : nombresBodega.GetValueOrDefault(l.BodegaDestino),
                l.Cantidad,
                unitario,
                l.CostoTotalLocal,
                l.CentroCosto,
                l.CuentaContable);
        })
        // El WinForms ordena por A.DESCRIP_ARTICULO.
        .OrderBy(l => l.DescripcionArticulo).ThenBy(l => l.Linea)
        .ToList();

        return new DocumentoCompleto(cabecera, lineas);
    }

    // ── Escritura: solo a través de los SPs legacy ────────────────────────────────

    /// <summary>Delega en <c>SINCO_GENERA_DOCINV_EXACTUS</c>, que valida la requisa y genera las
    /// líneas. Los mensajes <c>RAISERROR</c> llegan sin traducir desde <see cref="IExactusService"/>.</summary>
    public Task<ExactusResultado> GenerarDesdeRequisaAsync(
        string requisaNumero,
        CancellationToken cancellationToken = default)
        => _exactus.GenerarDesdeRequisaAsync(requisaNumero, cancellationToken);

    /// <summary>
    /// Delega en <c>SINCO_TRASLADAR_DOCINV_EXACTUS</c> con el número de documento legacy; el SP
    /// actualiza <c>TRANSFERIDO</c>, que es justo el filtro SI/NO del listado.
    /// </summary>
    public async Task<ExactusResultado> TransferirAsync(
        string paqueteInventario,
        string documentoInv,
        CancellationToken cancellationToken = default)
    {
        _ = await _contexto.DocumentoInventario.AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.PaqueteInventario == paqueteInventario && d.DocumentoInv == documentoInv,
                cancellationToken)
            ?? throw new KeyNotFoundException("El documento de inventario no existe.");

        return await _exactus.TransferirAsync(documentoInv, cancellationToken);
    }

    // ── Consultas auxiliares (rango + agrupación en memoria) ────────────────────────

    private sealed record ResumenLineas(int Total, string? Bodega);
    private sealed record InfoArticulo(string? Descripcion, string? Unidad);

    private async Task<Dictionary<string, ResumenLineas>> ResumenLineasAsync(
        string min,
        string max,
        CancellationToken cancellationToken)
    {
        var filas = await _contexto.DocumentoInventarioLinea.AsNoTracking()
            .Where(l => string.Compare(l.DocumentoInv, min) >= 0 &&
                        string.Compare(l.DocumentoInv, max) <= 0)
            .OrderBy(l => l.DocumentoInv).ThenBy(l => l.LineaDocInv)
            .Select(l => new { l.DocumentoInv, l.Bodega })
            .ToListAsync(cancellationToken);

        return filas.GroupBy(l => l.DocumentoInv).ToDictionary(
            g => g.Key,
            g => new ResumenLineas(g.Count(), g.First().Bodega));
    }

    private async Task<IReadOnlyDictionary<string, InfoArticulo>> NombresArticuloAsync(
        IEnumerable<string> codigos,
        CancellationToken cancellationToken)
    {
        var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (lista.Count == 0)
        {
            return new Dictionary<string, InfoArticulo>();
        }

        var min = lista.Min()!;
        var max = lista.Max()!;
        var articulos = await _contexto.VistaArticuloErp.AsNoTracking()
            .Where(a => string.Compare(a.CodigoArticulo, min) >= 0 &&
                        string.Compare(a.CodigoArticulo, max) <= 0)
            .Select(a => new { a.CodigoArticulo, a.Descripcion, a.DescripArticulo, a.UnidadVenta })
            .ToListAsync(cancellationToken);

        return articulos.Where(a => lista.Contains(a.CodigoArticulo))
            .GroupBy(a => a.CodigoArticulo)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var a = g.First();
                    return new InfoArticulo(
                        a.DescripArticulo ?? a.Descripcion,
                        a.UnidadVenta);
                });
    }

    private async Task<IReadOnlyDictionary<string, string>> NombresBodegaAsync(
        IEnumerable<string?> codigos,
        CancellationToken cancellationToken)
    {
        var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        if (lista.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var min = lista.Min()!;
        var max = lista.Max()!;
        var bodegas = await _contexto.VistaBodegaErp.AsNoTracking()
            .Where(b => string.Compare(b.Codigo, min) >= 0 && string.Compare(b.Codigo, max) <= 0)
            .Select(b => new { b.Codigo, b.Descripcion })
            .ToListAsync(cancellationToken);

        return bodegas.Where(b => lista.Contains(b.Codigo))
            .GroupBy(b => b.Codigo)
            .ToDictionary(g => g.Key, g => g.First().Descripcion ?? string.Empty);
    }
}
