namespace Orkin.Cotizador.Core.Domain.Catalogos;

/// <summary>
/// Los catálogos de dominio de `dbo.*Catalogo` (§5 de <c>BASE-DE-DATOS.md</c>).
/// <para>Cada clase es una tabla. Lo que las hace catálogos es heredar
/// <see cref="CatalogoDominioBase"/>: son de solo lectura en runtime, sin columnas de auditoría y
/// sin <c>rowversion</c>. Una fila de catálogo no "se actualiza", se sustituye.</para>
/// <para>⚠️ Aquí <b>no hay ni un solo <c>enum</c></b>. El conjunto de valores de cada una está en la
/// base de datos y se siembra desde <c>scripts/seed/*.json</c> (§5.20). Esta clase solo describe la
/// <i>forma</i> de la tabla, nunca sus filas.</para>
/// </summary>

// ── §5.12 · Costeo ─────────────────────────────────────────────────────────────────

/// <summary>§5.12 — <b>qué</b> métodos de costeo existen. El <b>cómo</b> se calcula vive en
/// <c>Core.Domain/MotorCalculo</c>, en una implementación de <c>IMetodoCosteo</c> por fila.</summary>
public class MetodoCosteoCatalogo : CatalogoDominioBase;

/// <summary>§5.1 — segmento del cliente. Solo dice <b>qué</b> método se le aplica.</summary>
public class TipoClienteCatalogo : CatalogoDominioBase
{
    /// <summary>FK → <see cref="MetodoCosteoCatalogo"/>. Dato, no lógica.</summary>
    public byte MetodoCosteoId { get; set; }

    public MetodoCosteoCatalogo? MetodoCosteo { get; set; }

    /// <summary>El segmento que se sugiere al crear un cliente.</summary>
    public bool EsPredeterminado { get; set; }
}

// ── §5.2 a §5.4 · Ciclo de vida de la cotización ──────────────────────────────────

/// <summary>§5.2 — los badges del Prototipo D.</summary>
public class EstadoCotizacionCatalogo : CatalogoDominioBase;

/// <summary>§5.3 — semestre. En <c>dbo.DetalleInsumo</c> es opcional: <c>null</c> = indistinto.</summary>
public class SemestreCatalogo : CatalogoDominioBase;

/// <summary>§5.4 — periodicidad de la cotización.</summary>
public class ModalidadCatalogo : CatalogoDominioBase
{
    /// <summary>
    /// La periodicidad que se sugiere al abrir el formulario. Es una columna y no un "la primera
    /// de la siembra" porque el orden de un archivo de seed no es un contrato: reordenar el JSON
    /// cambiaría la periodicidad que ve el usuario sin que nadie lo note.
    /// </summary>
    public bool EsPredeterminado { get; set; }
}

// ── §5.5 y §5.8 · Procedencia de un dato ──────────────────────────────────────────

/// <summary>§5.5 — de dónde viene un dato maestro. Es lo que permite que el ERP cargue catálogos
/// sin cambiar el modelo de datos (ADR-013).</summary>
public class OrigenDatoCatalogo : CatalogoDominioBase;

/// <summary>§5.8 — de dónde viene un <b>precio</b>. Distingue el dato inicial del Excel de lo que
/// cargó una persona, para saber cuánto sobrevivió a la corrección de duplicados (H-10).</summary>
public class OrigenPrecioCatalogo : CatalogoDominioBase;

// ── §5.6 · Tipo de cambio ─────────────────────────────────────────────────────────

/// <summary>§5.6 — de dónde sale el tipo de cambio. Depende de <b>D-06</b>.</summary>
public class FuenteTipoCambioCatalogo : CatalogoDominioBase;

// ── §5.7 y §5.19 · Aprobaciones ───────────────────────────────────────────────────

/// <summary>§5.7 — estado de una aprobación.</summary>
public class EstadoAprobacionCatalogo : CatalogoDominioBase
{
    /// <summary>
    /// <c>true</c> = la aprobación está resuelta. Existe para que el <c>CHECK</c> de §6.11 pueda
    /// exigir <c>FechaRespuesta</c> sin llevar el número de "rechazada" escrito en el esquema:
    /// un <c>CHECK</c> de SQL Server no admite subconsultas contra el catálogo.
    /// </summary>
    public bool EsEstadoFinal { get; set; }
}

/// <summary>§5.19 — <b>por qué</b> se pidió aprobación. Los umbrales están en
/// <c>dbo.ParametroGeneral</c>, no aquí.</summary>
public class NivelAprobacionCatalogo : CatalogoDominioBase;

// ── §5.18 · Margen ─────────────────────────────────────────────────────────────────

/// <summary>§5.18 — de dónde salió el margen aplicado.</summary>
public class OrigenMargenCatalogo : CatalogoDominioBase;

// ── §5.9 y §5.10 · Notificaciones ─────────────────────────────────────────────────

/// <summary>§5.9 — tipo de correo.</summary>
public class TipoNotificacionCatalogo : CatalogoDominioBase;

/// <summary>§5.10 — estado del envío.</summary>
public class EstadoNotificacionCatalogo : CatalogoDominioBase;

// ── §5.11 · Documentos ────────────────────────────────────────────────────────────

/// <summary>§5.11 — tipo de archivo generado.</summary>
public class TipoDocumentoCatalogo : CatalogoDominioBase;

// ── §5.13 · Auditoría ─────────────────────────────────────────────────────────────

/// <summary>§5.13 — acción registrada en <c>aud.RegistroAuditoria</c>.</summary>
public class AccionAuditoriaCatalogo : CatalogoDominioBase;

// ── §5.14 · Parámetros ────────────────────────────────────────────────────────────

/// <summary>§5.14 — cómo se convierte el texto de un parámetro. Si el conjunto viviera en el
/// código, alguien agregaría un tipo y se olvidaría de la conversión.</summary>
public class TipoDatoParametroCatalogo : CatalogoDominioBase;

// ── §5.15 · Consecutivos ───────────────────────────────────────────────────────────

/// <summary>§5.15 — numeraciones oficiales. El prefijo y el ancho viven acá, no en un
/// formateador de C#.</summary>
public class TipoConsecutivoCatalogo : CatalogoDominioBase
{
    public string Prefijo { get; set; } = null!;

    /// <summary>Ceros a la izquierda: <c>0413</c> son 4.</summary>
    public short Digitos { get; set; } = 4;
}

// ── §5.16 y §5.17 · Seguridad ─────────────────────────────────────────────────────

/// <summary>§5.16 — agrupación de permisos. Alimenta la pantalla de roles y la caché de la UI.</summary>
public class ModuloPermisoCatalogo : CatalogoDominioBase;

/// <summary>§5.17 — alcance de un rol. Oculta menús y acota la vista de datos.</summary>
public class NivelAccesoCatalogo : CatalogoDominioBase;
