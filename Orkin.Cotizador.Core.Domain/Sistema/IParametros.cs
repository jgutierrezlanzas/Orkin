using System.Text.Json;

namespace Orkin.Cotizador.Core.Domain.Sistema;

/// <summary>
/// Lee los parámetros de <c>dbo.ParametroGeneral</c> ya convertidos al tipo que declara
/// <c>dbo.TipoDatoParametroCatalogo</c> (ADR-018).
/// <para><b>Por qué existe:</b> cualquier valor que el negocio pueda querer mover —el umbral de
/// aprobación, los días de vigencia, el nombre del archivo del PDF— va en la base y se edita desde
/// Administración. Si estuviera compilado en C#, moverlo exigiría un redespliegue, y alguien
/// terminaría cambiándolo a mano en un <c>.json</c> de despliegue.</para>
/// <para>La conversión la decide el catálogo, no una cadena de <c>if</c> en el código. Agregar un
/// tipo de dato es una fila en la base, no un método nuevo.</para>
/// <para>Los métodos devuelven <c>null</c> cuando el parámetro no existe. Un parámetro ausente no es
/// un parámetro con valor por defecto: es una omisión, y el que llama decide si puede seguir.</para>
/// </summary>
public interface IParametros
{
    Task<string?> TextoAsync(string clave, CancellationToken cancellationToken = default);

    Task<int?> EnteroAsync(string clave, CancellationToken cancellationToken = default);

    Task<decimal?> DecimalAsync(string clave, CancellationToken cancellationToken = default);

    Task<bool?> LogicoAsync(string clave, CancellationToken cancellationToken = default);

    Task<DateOnly?> FechaAsync(string clave, CancellationToken cancellationToken = default);

    /// <summary>Como <see cref="TextoAsync"/>, pero garantiza que el texto sea JSON bien formado.</summary>
    Task<JsonElement?> JsonAsync(string clave, CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual que <see cref="DecimalAsync"/>, pero falla si el parámetro no existe o está vacío.
    /// <para>Se usa para los umbrales que el motor necesita sí o sí. Un umbral ausente tiene que
    /// ser un error visible, no un 0 silencioso que dejaría pasar todas las aprobaciones.</para>
    /// </summary>
    Task<decimal> DecimalRequeridoAsync(string clave, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida uno o todos los parámetros cacheados. La llama el servicio de administración al
    /// guardar un cambio, para que el efecto se vea sin reiniciar.
    /// </summary>
    Task InvalidarCacheAsync(string? clave = null, CancellationToken cancellationToken = default);
}
