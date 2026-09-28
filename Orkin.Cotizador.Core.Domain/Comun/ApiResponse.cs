namespace Orkin.Cotizador.Core.Domain.Comun;

/// <summary>
/// La forma única en que la API contesta. Todo lo que sale de un controlador lleva uno.
/// <para><b>Por qué un sobre y no el objeto desnudo:</b> quien llama —la UI web o un consumidor
/// externo— necesita distinguir tres cosas que en un cuerpo plano se ven igual: que la operación
/// salió bien, que salió bien pero no hay nada que mostrar, y que salió mal. El código HTTP ya
/// distingue la primera de la tercera, pero el <i>por qué</i> solo cabe en el cuerpo, y metido en el
/// cuerpo del éxito deja de estar tipado. Con el sobre, el compilador obliga a leer
/// <see cref="Exito"/> antes de leer <see cref="Datos"/>.</para>
/// <para><b>Por qué vive en el Domain y no en la API:</b> la UI de Blazor compila a WebAssembly y
/// no puede arrastrar Entity Framework. Si el sobre estuviera en un proyecto que la API sí conoce y
/// el navegador no, habría que declarar el mismo registro dos veces y mantenerlo sincronizado a
/// mano. Acá el contrato es un <c>record</c> sin dependencias, y los dos lados lo comparten. Lo que
/// sí es de la API —qué excepción se traduce a qué código, y qué número HTTP corresponde— vive
///_allí_, porque el navegador no.exceptions ni las conoce.</para>
/// <para><b>Por qué <see cref="Codigo"/> es texto y no un número:</b> es lo mismo que los catálogos
/// de negocio (ADR-018). El día que aparezca un código nuevo, quien llama tiene que poder seguir
/// leyendo los viejos sin romperse, y un número que cambia de valor al reordenar un <c>enum</c>
/// rompe exactamente eso.</para>
/// </summary>
/// <typeparam name="T">Lo que devuelve la operación.</typeparam>
public sealed record ApiResponse<T>
{
    /// <summary><c>true</c> si la operación salió bien. Nunca contradice al código HTTP.</summary>
    public required bool Exito { get; init; }

    /// <summary>
    /// Mensaje para mostrar, redactado para una persona. En los errores es la explicación, y
    /// siempre viene con <see cref="Codigo"/> al lado.
    /// </summary>
    public string? Mensaje { get; init; }

    /// <summary>
    /// Código estable del error, para que quien llama decida sin leer el mensaje. Ej.
    /// <c>COTIZACION_NO_ENCONTRADA</c>.
    /// </summary>
    public string? Codigo { get; init; }

    /// <summary>El resultado. <c>null</c> cuando la operación no devolvió nada o falló.</summary>
    public T? Datos { get; init; }

    /// <summary>Errores de validación, campo por campo. Vacío cuando no hay.</summary>
    public IReadOnlyList<ErrorCampo> Errores { get; init; } = [];

    /// <summary>
    /// Un error de un campo. <see cref="Campo"/> es el nombre de la propiedad tal como la ve
    /// quien está usando la pantalla, no el de la tabla: el mensaje se pinta junto a un control, y
    /// un control tiene nombre, no tabla.
    /// </summary>
    public sealed record ErrorCampo(string Campo, string Mensaje);

    // ── Constructores de uso común ────────────────────────────────────────────────

    /// <summary>Respuesta exitosa, con o sin resultado.</summary>
    public static ApiResponse<T> Ok(T? datos = default, string? mensaje = null) => new()
    {
        Exito = true,
        Datos = datos,
        Mensaje = mensaje
    };

    /// <summary>Respuesta exitosa sin contenido, para <c>DELETE</c> y para <c>PUT</c> que no devuelven nada.</summary>
    public static ApiResponse<T> SinContenido() => new() { Exito = true };

    /// <summary>Respuesta de error con su código estable.</summary>
    public static ApiResponse<T> Error(string codigo, string mensaje) => new()
    {
        Exito = false,
        Codigo = codigo,
        Mensaje = mensaje
    };

    /// <summary>
    /// Respuesta de validación. El código es siempre <c>VALIDACION</c> porque el detalle está en
    /// <see cref="Errores"/>, y un código distinto por cada campo que falló sería una lista de
    /// catálogos quemada en el cliente.
    /// </summary>
    public static ApiResponse<T> Validacion(IReadOnlyList<ErrorCampo> errores) => new()
    {
        Exito = false,
        Codigo = "VALIDACION",
        Mensaje = "Hay datos que corregir.",
        Errores = errores
    };
}
