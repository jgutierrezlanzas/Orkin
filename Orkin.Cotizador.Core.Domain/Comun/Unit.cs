namespace Orkin.Cotizador.Core.Domain.Comun;

/// <summary>
/// El tipo de "no hay ningún dato que devolver".
/// <para><b>Por qué existe:</b> sin él, un caso de uso que solo guarda y no devuelve nada tiene que
/// inventar un <c>bool</c> o un <c>string</c> vacío para poder usar el mismo
/// <see cref="ApiResponse{T}"/>, y los dos lifestyles producen el mismo código en el cliente: o
/// <c>datos: null</c> con <c>exito: true</c> porque no había nada, o <c>datos: null</c> con
/// <c>exito: false</c> porque algo falló. Con <see cref="Unit"/>, el tipo dice cuál de los dos es,
/// y el compilador obliga a llenarlo.</para>
/// <para>Es un <c>struct</c> vacío: no ocupa nada y no se puede construir con contenido, así que
/// dos <c>Unit</c> son siempre iguales. Por eso <c>Task</c> —que devuelve <c>Task</c> pelado— sigue
/// siendo la opción correcta donde no hay un sobre que devolver, y <c>ApiResponse&lt;Unit&gt;</c>
/// se reserva para las operaciones que pasan por la canalización de respuestas.</para>
/// </summary>
public readonly record struct Unit;
