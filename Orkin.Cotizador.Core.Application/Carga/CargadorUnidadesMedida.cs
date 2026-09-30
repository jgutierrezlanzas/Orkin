using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Carga el catálogo de unidades de medida desde el ERP del cliente. La abreviatura, si la
/// vista no la trae, es el propio código: en los maestros de unidades de un ERP, el código suele
/// a la vez ser lo que se imprime ("ml", "UND"). Los decimales se permiten por defecto —la
/// mayoría de los insumos se miden en ml, gr o litros— y la vista puede decir lo contrario por
/// unidad ("UND" contable).
/// </summary>
public sealed class CargadorUnidadesMedida : CargadorCatalogoMaestro<UnidadMedida>
{
    public CargadorUnidadesMedida(CotizadorDbContext contexto) : base(contexto)
    {
    }

    public override string Catalogo => "UNIDADES_MEDIDA";

    protected override DbSet<UnidadMedida> Set() => Contexto.UnidadMedida;

    protected override UnidadMedida CrearEntidad() => new();

    protected override Task AplicarExtrasAsync(
        UnidadMedida entidad, Dictionary<string, object?> mapeada, CancellationToken ct)
    {
        entidad.Abreviatura =
            Normalizacion.AString(mapeada.GetValueOrDefault("Abreviatura")) ?? entidad.Codigo;
        entidad.PermiteDecimales =
            Normalizacion.ABool(mapeada.GetValueOrDefault("PermiteDecimales"), porDefecto: true);
        return Task.CompletedTask;
    }

    protected override Task<List<CampoSimulado>> DiferenciaExtrasAsync(
        UnidadMedida existente, Dictionary<string, object?> mapeada, CancellationToken ct)
    {
        var cambios = new List<CampoSimulado>();

        var abreviatura = Normalizacion.AString(mapeada.GetValueOrDefault("Abreviatura")) ?? existente.Codigo;
        if (existente.Abreviatura != abreviatura)
        {
            cambios.Add(new CampoSimulado("Abreviatura", existente.Abreviatura, abreviatura));
        }

        var decimales = Normalizacion.ABool(mapeada.GetValueOrDefault("PermiteDecimales"), porDefecto: true);
        if (existente.PermiteDecimales != decimales)
        {
            cambios.Add(new CampoSimulado("Permite decimales", existente.PermiteDecimales ? "Sí" : "No", decimales ? "Sí" : "No"));
        }

        return Task.FromResult(cambios);
    }
}
