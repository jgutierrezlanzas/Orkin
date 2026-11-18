using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Catalogos;

/// <summary>Cliente vigente del ERP, ya resuelto al segmento que usa el motor de cálculo.</summary>
public sealed record ClienteErp(
    string Codigo,
    string Nombre,
    byte TipoClienteId,
    string? Categoria);

/// <summary>Tipo de servicio vigente del ERP.</summary>
public sealed record TipoServicioErp(
    string Codigo,
    string Nombre,
    string? Actividad);

/// <summary>Técnico de campo vigente del ERP.</summary>
public sealed record TecnicoErp(
    string Codigo,
    string Nombre,
    string? Iniciales);

/// <summary>
/// Artículo vigente del ERP. <see cref="CostoUnitario"/> es el costo con el que se cotiza: se
/// congela en el detalle para que la cotización no cambie cuando el ERP actualice el precio.
/// <para><c>vvArticulos</c> no trae vida útil: ese dato lo ingresa quien cotiza en cada línea
/// amortizable.</para>
/// </summary>
public sealed record ArticuloErp(
    string Codigo,
    string Nombre,
    string UnidadMedida,
    decimal CostoUnitario,
    string? Clasificacion,
    bool Amortizable);

/// <summary>Todas las opciones ERP que necesita el cotizador, leídas de las vistas <c>vv*</c>.</summary>
public sealed record OpcionesCotizacionErp(
    IReadOnlyList<ClienteErp> Clientes,
    IReadOnlyList<TipoServicioErp> TiposServicio,
    IReadOnlyList<TecnicoErp> Tecnicos,
    IReadOnlyList<ArticuloErp> Articulos,
    IReadOnlyList<string> Unidades,
    IReadOnlyList<string> Clasificaciones);

/// <summary>
/// Validación y resolución centralizada de los códigos ERP que guarda el cotizador (D-20/D-24).
/// <para>No copia catálogos: consulta <c>vvClientes</c>, <c>vvTipoServicios</c>, <c>vvTecnicos</c> y
/// <c>vvArticulos</c> con <c>AsNoTracking</c>, igual que <c>PlantillaService</c>. Un código vacío es
/// opcional donde el campo admite null; un código presente pero inexistente o inactivo se rechaza
/// con el nombre del campo y el código, para que el error se pueda corregir en pantalla.</para>
/// <para><c>CAT_CLIENTE</c> se traduce al <c>TipoClienteCatalogo</c> de la plataforma usando el
/// mapeo configurable de la conexión activa y, si no hay fila, la regla heredada del cargador:
/// <c>C01/C02 → EMPRESARIAL</c>, <c>C03 → RESIDENCIAL</c>.</para>
/// </summary>
public sealed class CatalogoErpService
{
    private const string ActivoCorto = "S";
    private const string ActivoSi = "SI";

    private readonly CotizadorDbContext _contexto;

    public CatalogoErpService(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>Opciones para los combos del cotizador. Todo sale de las vistas, sin tablas espejo.</summary>
    public async Task<OpcionesCotizacionErp> ObtenerOpcionesAsync(CancellationToken cancellationToken = default)
    {
        var tiposCliente = await CargarTiposClienteAsync(cancellationToken);
        var mapeoCategoria = await CargarMapeoCategoriaAsync(tiposCliente, cancellationToken);

        var clientes = await _contexto.VistaClienteErp
            .AsNoTracking()
            .Where(v => v.Activo == ActivoCorto)
            .OrderBy(v => v.Cliente)
            .ToListAsync(cancellationToken);

        var tiposServicio = await _contexto.VistaTipoServicioErp
            .AsNoTracking()
            .Where(v => v.Activo == ActivoSi)
            .OrderBy(v => v.Codigo)
            .ToListAsync(cancellationToken);

        var tecnicos = await _contexto.VistaTecnicoErp
            .AsNoTracking()
            .Where(v => v.Activo == ActivoSi)
            .OrderBy(v => v.Codigo)
            .ToListAsync(cancellationToken);

        var articulos = await _contexto.VistaArticuloErp
            .AsNoTracking()
            .Where(v => v.Activo == ActivoCorto)
            .OrderBy(v => v.CodigoArticulo)
            .ToListAsync(cancellationToken);

        var articulosErp = articulos.Select(MapperArticulo).ToList();

        return new OpcionesCotizacionErp(
            clientes.Select(c => new ClienteErp(
                c.Cliente.Trim(),
                Texto(c.NombreCliente) ?? c.Cliente.Trim(),
                ResolverTipoCliente(c.CategoriaCliente, tiposCliente, mapeoCategoria),
                Texto(c.CategoriaCliente))).ToList(),
            tiposServicio.Select(v => new TipoServicioErp(
                v.Codigo.Trim(),
                Texto(v.Descripcion) ?? v.Codigo.Trim(),
                Texto(v.TipoActividad))).ToList(),
            tecnicos.Select(v => new TecnicoErp(
                v.Codigo.Trim(),
                Texto(v.NombreCompleto) ?? Texto(v.Iniciales) ?? v.Codigo.Trim(),
                Texto(v.Iniciales))).ToList(),
            articulosErp,
            articulosErp.Select(a => a.UnidadMedida)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            articulosErp.Select(a => a.Clasificacion)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    /// <summary>Resuelve un cliente activo y su segmento. El código es obligatorio.</summary>
    public async Task<ClienteErp> ClienteAsync(
        string? codigo,
        CancellationToken cancellationToken = default)
    {
        var texto = Requerir(codigo, "Cliente");
        var cliente = await _contexto.VistaClienteErp
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Activo == ActivoCorto && v.Cliente.Trim() == texto, cancellationToken);

        if (cliente is null)
        {
            throw new InvalidOperationException(
                $"Cliente: el código '{texto}' no existe o está inactivo en el catálogo ERP.");
        }

        var tiposCliente = await CargarTiposClienteAsync(cancellationToken);
        var mapeoCategoria = await CargarMapeoCategoriaAsync(tiposCliente, cancellationToken);
        var categoria = Texto(cliente.CategoriaCliente);
        var tipoClienteId = ResolverTipoCliente(categoria, tiposCliente, mapeoCategoria);

        if (tipoClienteId == 0)
        {
            throw new InvalidOperationException(
                $"Cliente '{texto}': la categoría del ERP '{categoria ?? "—"}' no se pudo traducir a " +
                "un tipo de cliente de la plataforma. Configure el mapeo en int.ErpMapeoTipoCliente.");
        }

        return new ClienteErp(
            cliente.Cliente.Trim(),
            Texto(cliente.NombreCliente) ?? cliente.Cliente.Trim(),
            tipoClienteId,
            categoria);
    }

    /// <summary>Resuelve un tipo de servicio activo. El código es obligatorio.</summary>
    public async Task<TipoServicioErp> TipoServicioAsync(
        string? codigo,
        CancellationToken cancellationToken = default)
    {
        var texto = Requerir(codigo, "Tipo de servicio");
        var vista = await _contexto.VistaTipoServicioErp
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Activo == ActivoSi && v.Codigo.Trim() == texto, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Tipo de servicio: el código '{texto}' no existe o está inactivo en el catálogo ERP.");

        return new TipoServicioErp(
            vista.Codigo.Trim(),
            Texto(vista.Descripcion) ?? vista.Codigo.Trim(),
            Texto(vista.TipoActividad));
    }

    /// <summary>Resuelve un técnico activo. Vacío significa "sin técnico asignado".</summary>
    public async Task<TecnicoErp?> TecnicoAsync(
        string? codigo,
        CancellationToken cancellationToken = default)
    {
        var texto = Texto(codigo);
        if (texto is null)
        {
            return null;
        }

        var vista = await _contexto.VistaTecnicoErp
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Activo == ActivoSi && v.Codigo.Trim() == texto, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Técnico: el código '{texto}' no existe o está inactivo en el catálogo ERP.");

        return new TecnicoErp(
            vista.Codigo.Trim(),
            Texto(vista.NombreCompleto) ?? Texto(vista.Iniciales) ?? vista.Codigo.Trim(),
            Texto(vista.Iniciales));
    }

    /// <summary>
    /// Resuelve varios artículos activos de una sola consulta. Si <paramref name="soloAmortizables"/>
    /// es <c>true</c>, exige <c>AMORTIZABLE = SI</c>; si es <c>false</c>, no filtra (los insumos
    /// pueden ser cualquier artículo vigente).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ArticuloErp>> ArticulosAsync(
        IEnumerable<string?> codigos,
        bool soloAmortizables,
        CancellationToken cancellationToken = default)
    {
        var buscados = codigos
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (buscados.Count == 0)
        {
            return new Dictionary<string, ArticuloErp>(StringComparer.OrdinalIgnoreCase);
        }

        var consulta = _contexto.VistaArticuloErp
            .AsNoTracking()
            .Where(v => v.Activo == ActivoCorto && buscados.Contains(v.CodigoArticulo));

        if (soloAmortizables)
        {
            consulta = consulta.Where(v => v.Amortizable == ActivoSi);
        }

        var filas = await consulta.ToListAsync(cancellationToken);

        return filas.ToDictionary(
            v => v.CodigoArticulo.Trim(),
            MapperArticulo,
            StringComparer.OrdinalIgnoreCase);
    }

    private static ArticuloErp MapperArticulo(VistaArticuloErp v)
    {
        var codigo = v.CodigoArticulo.Trim();
        var costo = v.CostoUnitario
            ?? throw new InvalidOperationException(
                $"Artículo '{codigo}': el ERP no trae COSTO_UNITARIO. No se puede cotizar con un costo en blanco.");

        var nombre = Texto(v.DescripArticulo) ?? Texto(v.Descripcion) ?? codigo;
        var unidad = Texto(v.UnidadVenta) ?? Texto(v.UnidadAlmacen) ?? string.Empty;

        return new ArticuloErp(
            codigo,
            nombre,
            unidad,
            costo,
            Texto(v.Clasificacion),
            string.Equals(Texto(v.Amortizable), ActivoSi, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Dictionary<string, byte>> CargarTiposClienteAsync(CancellationToken cancellationToken)
        => await _contexto.TipoClienteCatalogo
            .AsNoTracking()
            .Where(t => t.EsActivo)
            .ToDictionaryAsync(t => t.Codigo.Trim(), t => t.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

    private async Task<Dictionary<string, byte>> CargarMapeoCategoriaAsync(
        IReadOnlyDictionary<string, byte> tiposCliente,
        CancellationToken cancellationToken)
    {
        var conexionActivaId = await _contexto.ErpConexion
            .AsNoTracking()
            .Where(c => c.IsActive && c.Estado == IntegracionCatalogos.EstadoConexionActiva)
            .OrderBy(c => c.CreatedAt)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (conexionActivaId is not { } idConexion)
        {
            return new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        }

        var mapas = await _contexto.ErpMapeoTipoCliente
            .AsNoTracking()
            .Where(m => m.IsActive && m.ErpConexionId == idConexion)
            .ToListAsync(cancellationToken);

        return mapas
            .Where(m => tiposCliente.Values.Contains(m.TipoClienteCatalogoId))
            .GroupBy(m => m.CodigoCategoriaErp.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().TipoClienteCatalogoId, StringComparer.OrdinalIgnoreCase);
    }

    private static byte ResolverTipoCliente(
        string? categoria,
        IReadOnlyDictionary<string, byte> tiposCliente,
        IReadOnlyDictionary<string, byte> mapeoCategoria)
    {
        if (string.IsNullOrWhiteSpace(categoria))
        {
            return 0;
        }

        // Si la vista ya trae el código de la plataforma, manda.
        if (tiposCliente.TryGetValue(categoria.Trim(), out var tipoDirecto))
        {
            return tipoDirecto;
        }

        if (mapeoCategoria.TryGetValue(categoria.Trim(), out var tipoMapeado))
        {
            return tipoMapeado;
        }

        return categoria.Trim().ToUpperInvariant() switch
        {
            "C01" => tiposCliente.GetValueOrDefault("EMPRESARIAL"),
            "C02" => tiposCliente.GetValueOrDefault("EMPRESARIAL"),
            "C03" => tiposCliente.GetValueOrDefault("RESIDENCIAL"),
            _ => 0
        };
    }

    private static string Requerir(string? valor, string campo)
        => Texto(valor)
           ?? throw new InvalidOperationException($"{campo}: el código es obligatorio.");

    private static string? Texto(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
