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

/// <summary>
/// §5.15 — numeraciones oficiales.
/// <para><b>Lo que vive acá y no en un formateador de C#:</b> cómo se arma un número oficial.
/// Antes el formato estaba escrito en el código como <c>Prefijo + Año + "-" + Numero</c>, y eso
/// cerraba la puerta a los tres casos más comunes de una numeración real: un código sin año, un
/// código con letra al final, y un código con guion donde sí y donde no. Agregar una variante
/// obligaba a tocar el formateador, y el formateador era código.</para>
/// <para><b>Lo único que no es opcional es <c>{NUM}</c>:</b> es la parte que incrementa. Una
/// plantilla sin <c>{NUM}</c> no es un consecutivo, es una constante con otro nombre, y dejarla
/// pasar produciría un número igual para todos los documentos.</para>
/// </summary>
public class TipoConsecutivoCatalogo : CatalogoDominioBase
{
    /// <summary>Lo que se expande en <c>{PRE}</c>. Texto libre; vacío es válido.</summary>
    public string Prefijo { get; set; } = string.Empty;

    /// <summary>Lo que se expande en <c>{SUF}</c>. Texto libre; vacío es válido.</summary>
    public string Sufijo { get; set; } = string.Empty;

    /// <summary>
    /// La forma del número, con las cuatro secciones donde el usuario quiera ponerlas:
    /// <c>{PRE}</c>, <c>{ANIO}</c>, <c>{NUM}</c> y <c>{SUF}</c>. Todo lo que no sea una sección es
    /// texto que se copia tal cual, así que un guion es un guion y se pone donde corresponda.
    /// <para>La misma <c>{PRE}</c> dos veces es un error y no un resultado raro: significa que
    /// alguien quiso cambiar el prefijo a mitad de la plantilla y no se dio cuenta.</para>
    /// </summary>
    public string Plantilla { get; set; } = null!;

    /// <summary>Ceros a la izquierda de <c>{NUM}</c>: <c>0413</c> son 4. De 1 a 19.</summary>
    public int Digitos { get; set; } = 4;

    /// <summary>
    /// Si el contador vuelve a <c>1</c> cada 1 de enero.
    /// <para><b>Por qué no se deduce de la plantilla:</b> una plantilla puede no llevar
    /// <c>{ANIO}</c> y aun así reiniciar. El caso dangerous es al revés: una plantilla
    /// <em>sin</em> <c>{ANIO}</c> con el contador reiniciando produce <c>ABC0001</c> en diciembre y
    /// <c>ABC0001</c> otra vez en enero, y el segundo choca contra el índice único. La plantilla
    /// dice cómo se ve el número; esta bandera dice cuándo vuelve a empezar, y son dos
    /// preguntas distintas.</para>
    /// </summary>
    public bool ReiniciaPorAnio { get; set; } = true;
}

// ── §5.16 y §5.17 · Seguridad ─────────────────────────────────────────────────────

/// <summary>§5.16 — agrupación de permisos. Alimenta la pantalla de roles y la caché de la UI.</summary>
public class ModuloPermisoCatalogo : CatalogoDominioBase;

/// <summary>§5.17 — alcance de un rol. Oculta menús y acota la vista de datos.</summary>
public class NivelAccesoCatalogo : CatalogoDominioBase;
