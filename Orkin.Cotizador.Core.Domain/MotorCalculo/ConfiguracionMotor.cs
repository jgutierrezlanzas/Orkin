

namespace Orkin.Cotizador.Core.Domain.MotorCalculo;

/// <summary>
/// La fotografía de los catálogos que el motor necesita, ya resuelta. Se arma <b>una vez al
/// arrancar</b>, leyendo las tablas.
/// <para>Es la pieza que cumple el trato de ADR-018: el motor recibe ids y estrategias resueltas, y
/// por lo tanto no contiene ni un número mágico ni un nombre de catálogo.</para>
/// <para>Si mañana se agrega un método de costeo nuevo, el arranque falla con un mensaje explícito
/// ("la fila SUMA_X no tiene implementación") en vez de calcular en silencio con el método anterior.</para>
/// </summary>
public sealed record ConfiguracionMotor
{
    /// <summary>
    /// Método de costeo por <c>Id</c> de <c>dbo.MetodoCosteoCatalogo</c>. Lo consume
    /// <see cref="CotizadorCalculo"/>.
    /// </summary>
    public required IReadOnlyDictionary<byte, IMetodoCosteo> MetodosPorId { get; init; }

    /// <summary>Ids de <c>dbo.SemestreCatalogo</c> ya resueltos.</summary>
    public required SemestresResueltos Semestres { get; init; }

    /// <summary>
    /// Método de costeo que corresponde a cada segmento, leído de
    /// <c>dbo.TipoClienteCatalogo.MetodoCosteoId</c> (§5.1). Es lo que permite que el servicio de
    /// cotización no sepa qué método usa cada tipo de cliente.
    /// </summary>
    public required IReadOnlyDictionary<byte, byte> MetodoPorSegmento { get; init; }
}

/// <summary>
/// Arma la <see cref="ConfiguracionMotor"/> desde la base.
/// <para>Lo implementa la capa de infraestructura, porque es la única que sabe consultar SQL.</para>
/// </summary>
public interface IConstructorConfiguracionMotor
{
    Task<ConfiguracionMotor> ConstruirAsync(CancellationToken cancellationToken = default);
}
