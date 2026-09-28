namespace Orkin.Cotizador.Api.Seguridad;

/// <summary>
/// Configuración de la autenticación y la autorización.
/// <para><b>Por qué la firma es simétrica y no asimétrica:</b> el emisor y el verificador son el
/// mismo proceso —esta misma API— (ADR-007). Una clave pública/privada se justifica cuando hay
/// muchos verificadores que no deberían poder firmar; acá hay uno solo, y la única clave privada
/// que existe ya está en el proceso que firma. La asimétrica agregaría un segundo par de claves
/// que además hay que rotar por separado, y en un monolito eso es trabajo sin contrapartida.</para>
/// <para><b>Por qué la clave se valida al arrancar y no al primer ingreso:</b> una clave demasiado
/// corta es un error de configuración, y descubrirlo a las tres de la mañana cuando nadie puede
/// iniciar sesión es la peor forma. Con <c>ValidateOnStart</c>, el proceso no levanta y el
/// orquestador lo reinicia hasta que alguien corrija el secreto —que es justo lo que uno quiere que
/// pase.</para>
/// </summary>
public sealed class SeguridadOptions
{
    /// <summary>Sección de configuración que se enlaza.</summary>
    public const string Seccion = "Seguridad";

    /// <summary>
    /// Identificador del emisor. Va en el token y se compara al validar, para que un token firmado
    /// por otra cosa —un servicio vecino que comparte la clave por error— no sea aceptado.
    /// </summary>
    public string Emisor { get; set; } = string.Empty;

    /// <summary>Para quién es el token. Se compara al validar, por la misma razón.</summary>
    public string Audiencia { get; set; } = string.Empty;

    /// <summary>
    /// Clave de firma, en texto. <b>Vive en el <c>.env</c> o en el gestor de secretos, nunca en
    /// <c>appsettings.json</c> versionado.</b>
    /// <para>Mínimo 32 bytes: HMAC-SHA256 con una clave más corta deja de ser más difícil de
    /// adivinar que la salida, y una clave débil aquí es lo que permite a cualquiera emitir un
    /// token de administrador.</para>
    /// </summary>
    public string ClaveFirma { get; set; } = string.Empty;

    /// <summary>
    /// Horas de validez del token de acceso. Corto a propósito: es la ventana en la que un token
    /// robado sirve sin que nadie note nada.
    /// </summary>
    public int HorasValidez { get; set; } = 8;

    /// <summary>
    /// Tolerancia de reloj, en minutos, al validar <c>nbf</c> y <c>exp</c>. Sin ella, un token
    /// emitido por un servidor con el reloj medio minuto adelantado se rechaza en el otro, y el
    /// síntoma es "a veces el ingreso no funciona" sin que nadie sepa por qué.
    /// </summary>
    public int MinutosToleranciaReloj { get; set; } = 2;
}
