using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Configuración editable desde Administración, sin necesidad de redeploy.
/// <para>El nombre es la clave: <c>APROBACION.MARGEN_MINIMO</c>, <c>PDF.PIE_LEGAL</c>. La
/// aplicación lee <see cref="TipoDatoId"/> para convertir el texto sin adivinar.</para>
/// <para>⚠️ <b>ADR-018</b>: si algún valor ajustable estuviera compilado en C#, no sería un
/// parámetro, sería un bug esperando que alguien lo cambie a mano en un despliegue. Acá no hay
/// ninguno: lo que se ajusta vive en esta tabla.</para>
/// </summary>
public class ParametroGeneral : BaseEntity
{
    /// <summary>Clave única del parámetro. Ej. <c>APROBACION.MARGEN_MINIMO</c>.</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>Texto de la etiqueta en la pantalla de parámetros.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Valor crudo, tal como lo teclea el usuario. La conversión la hace el catálogo de tipo.</summary>
    public string? Valor { get; set; }

    /// <summary>FK → <c>dbo.TipoDatoParametroCatalogo</c> (§5.14).</summary>
    public byte TipoDatoId { get; set; }

    public TipoDatoParametroCatalogo? TipoDato { get; set; }

    /// <summary>Grupo en la pantalla: "General", "Aprobación", "PDF", "Correo", "Integraciones".</summary>
    public string? Grupo { get; set; }

    public short Orden { get; set; }

    /// <summary>Un parámetro de sistema no se borra desde la UI.</summary>
    public bool EsSistema { get; set; }

    /// <summary>La UI lo enmascara (tokens, claves).</summary>
    public bool EsSecreto { get; set; }
}

/// <summary>
/// Consecutivo por tipo y año. Resuelve el requisito del SRS de numeración oficial
/// (<c>COT-2026-0413</c>). <b>No se usa identidad de SQL Server</b>: la numeración oficial de
/// ORKIN es un correlate propio y tiene que ser transaccional.
/// <para>El prefijo y el ancho no están en un formateador de C#: salen de
/// <c>dbo.TipoConsecutivoCatalogo</c> (§5.15).</para>
/// </summary>
public class Consecutivo : BaseEntity
{
    /// <summary>FK → <c>dbo.TipoConsecutivoCatalogo</c>.</summary>
    public byte TipoId { get; set; }

    public TipoConsecutivoCatalogo? Tipo { get; set; }

    public short Anio { get; set; }

    /// <summary>Prefijo completo del año. Ej. <c>COT-2026-</c>.</summary>
    public string? Prefijo { get; set; }

    /// <summary>Último número emitido. Empieza en <c>0</c>.</summary>
    public long UltimoNumero { get; set; }

    /// <summary>Ceros a la izquierda: <c>0413</c> son 4.</summary>
    public int Digitos { get; set; } = 4;
}

/// <summary>Bitácora de correos enviados. Una fila por envío, con su resultado.</summary>
public class NotificacionEnviada : BaseEntity
{
    /// <summary>FK → <c>dbo.TipoNotificacionCatalogo</c> (§5.9).</summary>
    public byte TipoId { get; set; }

    /// <summary>Entidad relacionada, normalmente una cotización.</summary>
    public Guid? CotizacionId { get; set; }

    public string Destinatario { get; set; } = null!;

    public string? Asunto { get; set; }

    /// <summary>FK → <c>dbo.EstadoNotificacionCatalogo</c> (§5.10).</summary>
    public byte EstadoId { get; set; }

    public int Intentos { get; set; }

    public DateTime? EnviadoUtc { get; set; }

    public string? IdExterno { get; set; }

    public string? Error { get; set; }
}

/// <summary>PDFs y archivos generados. La cotización se conserva aunque el PDF se borre del disco.</summary>
public class DocumentoGenerado : BaseEntity
{
    public Guid CotizacionId { get; set; }

    /// <summary>FK → <c>dbo.TipoDocumentoCatalogo</c> (§5.11).</summary>
    public byte TipoId { get; set; }

    public string NombreArchivo { get; set; } = null!;

    public string? Ruta { get; set; }

    public long TamanoBytes { get; set; }

    /// <summary>SHA-256 del archivo. Detecta si alguien lo cambió.</summary>
    public byte[]? Hash { get; set; }

    public DateTime? GeneradoUtc { get; set; }

    public Guid? GeneradoPor { get; set; }
}
