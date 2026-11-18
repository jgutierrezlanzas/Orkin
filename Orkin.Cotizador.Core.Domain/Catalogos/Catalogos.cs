using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Catalogos;

/// <summary>
/// Forma común de los catálogos maestros propios de la plataforma.
/// <para>Los catálogos de dominio (ADR-018) se configuran aparte y se siembran desde
/// <c>scripts/seed/*.json</c>. Los códigos que antes se copiaban desde el ERP se leen ahora de las
/// vistas <c>vv*</c> (D-24), así que esta clase base no representa ninguna tabla espejo.</para>
/// </summary>
public abstract class CatalogoBase : BaseEntity
{
    /// <summary>Código estable y único, sin tildes ni eñes. Ej. <c>ML</c>, <c>INSECTICIDA</c>, <c>SAN</c>.</summary>
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>Orden de presentación. El PDF y la UI no ordenan por texto.</summary>
    public int Orden { get; set; }

    /// <summary>
    /// De dónde viene el registro. Permite distinguir lo cargado a mano de lo que vino del Excel
    /// o de un ERP, que es la base de la integración de la Fase 12.
    /// <para>Es FK a <c>dbo.OrigenDatoCatalogo</c>. No es un <c>enum</c>: el conjunto de orígenes
    /// está en la base.</para>
    /// </summary>
    public byte OrigenDatoId { get; set; }

    /// <summary>Clave del registro en el ERP. Es la clave de negocio del <i>upsert</i> de la Fase 12.</summary>
    public string? CodigoExterno { get; set; }

    /// <summary>Conexión ERP de la que vino. <c>null</c> si el registro es manual.</summary>
    public Guid? ErpConexionId { get; set; }

    /// <summary>SHA-256 del contenido relevante. Si no cambia, la sincronización no escribe nada.</summary>
    public byte[]? HashContenido { get; set; }

    /// <summary>Última vez que un flujo de sincronización confirmó este registro.</summary>
    public DateTime? UltimaSyncUtc { get; set; }

    /// <summary>
    /// <c>true</c> = la fila viene del seed; <c>false</c> = la creó un usuario. Marca de
    /// idempotencia de la siembra (§5.20).
    /// </summary>
    public bool EsSembrado { get; set; } = true;
}
