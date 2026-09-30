using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Carga los tipos de servicio desde el ERP del cliente. La unidad de cobro —<c>UND</c>,
/// <c>M2</c>, <c>HORA</c>— se resuelve por código contra el catálogo de unidades ya cargado,
/// igual que los productos resuelven la suya: sin ella, el servicio existe pero no se puede
/// cotizar. Si la vista no trae unidad, el servicio queda sin unidad de cobro y se elige a mano
/// — es opcional en la entidad a propósito.
/// </summary>
public sealed class CargadorTiposServicio : CargadorCatalogoMaestro<TipoServicio>
{
    private Dictionary<string, Guid>? _unidades;

    public CargadorTiposServicio(CotizadorDbContext contexto) : base(contexto)
    {
    }

    public override string Catalogo => "TIPOS_SERVICIO";

    protected override DbSet<TipoServicio> Set() => Contexto.TipoServicio;

    protected override IReadOnlyList<CampoEsperado> CamposExtras() =>
    [
        new("Descripcion", "texto", false, "Texto que va al PDF."),
        new("UnidadMedidaCodigo", "texto", false, "Unidad de cobro del servicio (ej. UND, M2, HORA). Si la vista no lo trae, se elige a mano."),
        new("EsResidencialPorDefecto", "booleano", false, "SI/NO. Sugerencia al abrir el formulario de cotización."),
        new("EsEmpresarialPorDefecto", "booleano", false, "SI/NO. Sugerencia al abrir el formulario de cotización."),
    ];

    protected override TipoServicio CrearEntidad() => new();

    protected override async Task AplicarExtrasAsync(
        TipoServicio entidad, Dictionary<string, object?> mapeada, CancellationToken ct)
    {
        entidad.Descripcion = Normalizacion.AString(mapeada.GetValueOrDefault("Descripcion"));

        var codigoUnidad = Normalizacion.AString(mapeada.GetValueOrDefault("UnidadMedidaCodigo"));
        if (codigoUnidad is null)
        {
            entidad.UnidadMedidaId = null;
        }
        else
        {
            var id = await ResolverUnidadAsync(codigoUnidad, ct);
            entidad.UnidadMedidaId = id ?? throw new FormatException(
                $"La unidad de cobro '{codigoUnidad}' no existe. Cargue el flujo de unidades primero.");
        }

        entidad.EsResidencialPorDefecto =
            Normalizacion.ABool(mapeada.GetValueOrDefault("EsResidencialPorDefecto"), porDefecto: false);
        entidad.EsEmpresarialPorDefecto =
            Normalizacion.ABool(mapeada.GetValueOrDefault("EsEmpresarialPorDefecto"), porDefecto: false);
    }

    protected override async Task<List<CampoSimulado>> DiferenciaExtrasAsync(
        TipoServicio existente, Dictionary<string, object?> mapeada, CancellationToken ct)
    {
        var cambios = new List<CampoSimulado>();

        var descripcion = Normalizacion.AString(mapeada.GetValueOrDefault("Descripcion"));
        if (existente.Descripcion != descripcion)
        {
            cambios.Add(new CampoSimulado("Descripción", existente.Descripcion, descripcion));
        }

        var codigoUnidad = Normalizacion.AString(mapeada.GetValueOrDefault("UnidadMedidaCodigo"));
        Guid? unidadNueva = null;
        if (codigoUnidad is not null)
        {
            unidadNueva = await ResolverUnidadAsync(codigoUnidad, ct)
                ?? throw new FormatException(
                    $"La unidad de cobro '{codigoUnidad}' no existe. Cargue el flujo de unidades primero.");
        }
        if (existente.UnidadMedidaId != unidadNueva)
        {
            var nombreAnterior = existente.UnidadMedidaId is { } id
                ? await NombreUnidadAsync(id, ct) : "(ninguna)";
            cambios.Add(new CampoSimulado("Unidad de cobro", nombreAnterior, codigoUnidad));
        }

        var residencial = Normalizacion.ABool(mapeada.GetValueOrDefault("EsResidencialPorDefecto"), porDefecto: false);
        if (existente.EsResidencialPorDefecto != residencial)
        {
            cambios.Add(new CampoSimulado("Residencial", existente.EsResidencialPorDefecto ? "Sí" : "No", residencial ? "Sí" : "No"));
        }

        var empresarial = Normalizacion.ABool(mapeada.GetValueOrDefault("EsEmpresarialPorDefecto"), porDefecto: false);
        if (existente.EsEmpresarialPorDefecto != empresarial)
        {
            cambios.Add(new CampoSimulado("Empresarial", existente.EsEmpresarialPorDefecto ? "Sí" : "No", empresarial ? "Sí" : "No"));
        }

        return cambios;
    }

    /// <summary>Resuelve el código de unidad contra el catálogo ya cargado, con caché por corrida.</summary>
    private async Task<Guid?> ResolverUnidadAsync(string codigo, CancellationToken ct)
    {
        _unidades ??= await Contexto.UnidadMedida
            .AsNoTracking()
            .Where(u => u.IsActive)
            .ToDictionaryAsync(u => u.Codigo, u => u.Id, StringComparer.OrdinalIgnoreCase, ct);

        return _unidades.TryGetValue(codigo, out var id) ? id : null;
    }

    private async Task<string> NombreUnidadAsync(Guid id, CancellationToken ct)
    {
        _nombresUnidad ??= await Contexto.UnidadMedida
            .AsNoTracking()
            .Where(u => u.IsActive)
            .ToDictionaryAsync(u => u.Id, u => u.Abreviatura, ct);

        return _nombresUnidad.TryGetValue(id, out var nombre) ? nombre : "(no encontrada)";
    }

    private Dictionary<Guid, string>? _nombresUnidad;
}
