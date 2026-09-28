using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Catalogos;

/// <summary>
/// Catálogo **maestro** de costeo: unidades de medida, categorías, tipos de servicio.
/// A diferencia de los catálogos de dominio (§5), estos sí llevan las columnas base de §3.1, son
/// editables desde Administración y admiten baja lógica.
/// <para>No tienen precio ni cantidad: solo el <see cref="Codigo"/> con el que los referencian las
/// tablas de negocio. Los <c>Id</c> son <c>uniqueidentifier</c> y no <c>tinyint</c> porque van a
/// ser referenciados por clave foránea desde el ERP en la Fase 12.</para>
/// <para>⚠️ <b>ADR-018:</b> las <b>filas</b> de estos catálogos viven en la base y se siembran
/// desde <c>scripts/seed/*.json</c>. Esta clase no dice qué unidades ni qué servicios existen.</para>
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

/// <summary>
/// Unidad de medida de un insumo o de un servicio.
/// <para>⚠️ <b>H-09</b>: el Excel trae <c>H.H</c>, que es ambiguo (¿horas-hom? ¿horas-hombre?).
/// Hay que normalizarlo durante la importación; la elección de <c>Codigo</c> es del cliente y
/// queda en el seed, no en el código.</para>
/// </summary>
public class UnidadMedida : CatalogoBase
{
    /// <summary>Se imprime en el PDF y en la grilla del formulario.</summary>
    public string Abreviatura { get; set; } = null!;

    /// <summary>
    /// <c>false</c> para unidades contables (UND, par, sitio, BLD): en esas, la cantidad siempre
    /// es un entero y la UI no debe permitir decimales.
    /// </summary>
    public bool PermiteDecimales { get; set; } = true;
}

/// <summary>Familia de un producto: insecticida, rodenticida, repelente, bulk, servicio…</summary>
public class CategoriaProducto : CatalogoBase
{
    public string? Descripcion { get; set; }
}

/// <summary>
/// Tipo de servicio. Las 11 filas del Excel (hoja oculta <c>Tipos de servicios</c>) se siembran
/// aquí; cuáles son, vive en el seed.
/// <para>⚠️ <b>H-01</b>: el Excel <b>no</b> deriva el margen del tipo de servicio. Ese dato no
/// existe todavía; viene de <b>D-04</b> y vive en <see cref="TipoServicioMargen"/>.</para>
/// </summary>
public class TipoServicio : CatalogoBase
{
    /// <summary>Texto que va al PDF.</summary>
    public string? Descripcion { get; set; }

    /// <summary>
    /// Unidad en que se cobra el servicio: <c>UND</c>, <c>M2</c> u <c>HORA</c>.
    /// Es distinto de la unidad del insumo que se consume.
    /// </summary>
    public Guid? UnidadMedidaId { get; set; }

    public UnidadMedida? UnidadMedida { get; set; }

    /// <summary>Sugerencia al abrir el formulario de cotización.</summary>
    public bool EsResidencialPorDefecto { get; set; }

    public bool EsEmpresarialPorDefecto { get; set; }
}

/// <summary>
/// Margen sugerido por tipo de servicio, <b>con vigencia</b>. Corrige H-02 / RN-10: hoy el margen
/// es un valor fijo que el usuario teclea y que ni siquiera queda guardado.
/// <para>Depende de <b>D-04</b>. Mientras no llegue, se siembra con los valores del Excel
/// (residencial <c>0.0550</c>, empresarial <c>0.0800</c>) con <c>OrigenDato = EXCEL</c> para que
/// sean fáciles de localizar y corregir.</para>
/// <para>El margen es una <b>fracción</b>: <c>0.0550</c> = 5.5 %. Nunca <c>5.5</c>.</para>
/// </summary>
public class TipoServicioMargen : BaseEntity
{
    public Guid TipoServicioId { get; set; }

    public TipoServicio TipoServicio { get; set; } = null!;

    public DateOnly VigenteDesde { get; set; }

    /// <summary><c>null</c> = vigente. Solo un rango vigente por tipo de servicio (índice único filtrado).</summary>
    public DateOnly? VigenteHasta { get; set; }

    /// <summary>Fracción: <c>0.0550</c> = 5.5 %. Restringido a <c>0..1</c> por <c>CHECK</c>.</summary>
    public decimal Margen { get; set; }

    /// <summary>FK a <c>dbo.OrigenDatoCatalogo</c>.</summary>
    public byte OrigenDatoId { get; set; }
}
