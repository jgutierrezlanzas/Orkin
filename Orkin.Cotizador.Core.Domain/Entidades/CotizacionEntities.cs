using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// La tabla central. Cada columna de resultado del motor de cálculo es un <b>snapshot persistido</b>:
/// los totales no se recalculan al leer, se leen tal como quedaron al guardar (RN-03 a RN-08).
/// <para>Por eso una cotización aprobada hoy sigue cuadrando aunque mañana cambien el costo, el
/// precio o el tipo de cambio.</para>
/// <para>⚠️ <b>No lleva <c>IsActive</c></b>: una cotización se anula, no se borra.</para>
/// <para>⚠️ <b>ADR-018</b>: las columnas segmentadas (<c>TipoClienteId</c>, <c>EstadoId</c>,
/// <c>ModalidadId</c>, <c>MargenOrigenId</c>) son FK a catálogos. Ninguna es un <c>enum</c>.</para>
/// </summary>
public class Cotizacion : BaseEntity
{
    /// <summary>Número formal: <c>COT-2026-0413</c>. Lo genera <c>ConsecutivoService</c>.</summary>
    public string Numero { get; set; } = null!;

    /// <summary>
    /// FK a <c>dbo.TipoConsecutivoCatalogo</c> (§5.15): qué tipo de consecutivo produjo
    /// <see cref="Numero"/>.
    /// <para><b>Por qué queda registrado y no se deduce:</b> el tipo de un catálogo se puede
    /// desactivar o cambiar de prefijo, y si la fila no guardara cuál fue, dentro de un año no
    /// habría forma de saber por qué el número de esa cotización tiene esa forma. Guardar el hecho
    /// junto al documento es lo que permite auditarlo.</para>
    /// <para><b>Por qué es nullable:</b> solo las cotizaciones que nacen en la plataforma llevan
    /// número propio. Las que se cargan desde el ERP traen el número del ERP y este tipo no aplica.
    /// Un <c>NOT NULL</c> obligaría a inventar un tipo "importado", que es un tipo que no
    /// numera nada.</para>
    /// <para><b>Sin navegación</b>, por la convención de ADR-013: la clave va como texto y el
    /// tipo se resuelve cuando haga falta.</para>
    /// </summary>
    public byte? TipoConsecutivoId { get; set; }

    public short Anio { get; set; }

    /// <summary><c>1, 2, 3…</c> Una corrección no pisa: crea una versión nueva.</summary>
    public short Version { get; set; } = 1;

    public Guid? VersionAnteriorId { get; set; }

    public Cotizacion? VersionAnterior { get; set; }

    /// <summary>Una sola versión vigente por <see cref="Numero"/> (índice único filtrado).</summary>
    public bool EsVersionActual { get; set; } = true;

    /// <summary>
    /// FK → <c>dbo.Cliente</c>. <b>Null cuando la cotización es para un cliente nuevo</b>, que
    /// todavía no existe en el catálogo: alguien llamó, pidió precios y todavía no es cliente.
    /// En ese caso la persona solo aparece en <see cref="ClienteNombre"/> y la cotización no
    /// arrastra al catálogo una ficha que nadie confirmó. Si después el cliente se carga del
    /// ERP, las cotizaciones viejas no se le re-asignan: son un snapshot, y re-atarlas sería
    /// inventar que se cotizó "para la ficha" cuando se cotizó "para el nombre".
    /// </summary>
    public Guid? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public Guid TipoServicioId { get; set; }

    public TipoServicio TipoServicio { get; set; } = null!;

    /// <summary>FK → <c>dbo.TipoClienteCatalogo</c>. Copia del cliente al momento de cotizar; para
    /// un cliente nuevo, lo elige quien cotiza.</summary>
    public byte TipoClienteId { get; set; }

    public DateOnly Fecha { get; set; }

    /// <summary>Hasta cuándo es válida la oferta. Depende de <b>D-16</b>.</summary>
    public DateOnly? FechaVigencia { get; set; }

    public short? VigenciaDias { get; set; }

    /// <summary>FK → <c>dbo.EstadoCotizacionCatalogo</c>. Sin <c>DEFAULT</c> de SQL.</summary>
    public byte EstadoId { get; set; }

    public EstadoCotizacionCatalogo? Estado { get; set; }

    /// <summary>FK → <c>dbo.ModalidadCatalogo</c>. Sin <c>DEFAULT</c> de SQL.</summary>
    public byte? ModalidadId { get; set; }

    public ModalidadCatalogo? Modalidad { get; set; }

    public Guid? TecnicoAsignadoId { get; set; }

    public DateOnly? FechaEstimada { get; set; }

    // ── Motor de cálculo ────────────────────────────────────────────────────────────

    /// <summary>Fracción. <c>0.0550</c> = 5.5 %. <c>CHECK 0..1</c> → corrige <b>H-02</b>.</summary>
    public decimal MargenAplicado { get; set; }

    /// <summary>FK → <c>dbo.OrigenMargenCatalogo</c>: si lo tecleó alguien o vino del catálogo.</summary>
    public byte MargenOrigenId { get; set; }

    /// <summary>FK → <c>dbo.MetodoCosteoCatalogo</c>. Se persiste para poder reproducir el cálculo.</summary>
    public byte MetodoCosteoId { get; set; }

    public Guid? TipoCambioId { get; set; }

    public TipoCambio? TipoCambio { get; set; }

    /// <summary>Snapshot del TC. Aunque la fila de <c>TipoCambio</c> cambie, la cotización no.</summary>
    public decimal TipoCambioValor { get; set; }

    public DateOnly TipoCambioFecha { get; set; }

    public decimal TotalInsumosSemestre1 { get; set; }
    public decimal TotalInsumosSemestre2 { get; set; }

    /// <summary>RN-03.</summary>
    public decimal TotalInsumos { get; set; }

    /// <summary>RN-02. Suma de los <c>ValorMensual</c> de los activos.</summary>
    public decimal TotalAmortizablesMensual { get; set; }

    /// <summary>RN-04 (residencial) / RN-07 (empresarial).</summary>
    public decimal CostoTotal { get; set; }

    /// <summary>RN-05.</summary>
    public decimal PrecioCordobas { get; set; }

    /// <summary>RN-06.</summary>
    public decimal PrecioUsd { get; set; }

    /// <summary>RN-08. Indicador interno; <b>no</b> afecta el precio.</summary>
    public decimal? PorcentajePlantilla { get; set; }

    /// <summary>
    /// Huella de las entradas del motor. Si cambia sin que cambien las entradas, alguien editó
    /// la fila a mano.
    /// </summary>
    public byte[]? HashCalculo { get; set; }

    /// <summary>JSON con el estado completo del motor → permite <b>reproducir</b> el cálculo y depurar.</summary>
    public string? ParametrosCalculo { get; set; }

    // ── Snapshot de cliente y oferta ────────────────────────────────────────────────

    /// <summary>
    /// El nombre tal como se cotizó. Para un cliente del catálogo es la copia de
    /// <c>Cliente.Nombre</c>; para un cliente nuevo es el nombre que tecleó quien cotizó. Es
    /// el único lugar confiable del nombre en una cotización vieja: el cliente pudo cambiar
    /// de nombre, desactivarse o nunca llegar a existir en el catálogo.
    /// </summary>
    public string ClienteNombre { get; set; } = null!;

    public string? ClienteDocumento { get; set; }

    public string? ContactoNombre { get; set; }
    public string? ContactoTelefono { get; set; }
    public string? ContactoCorreo { get; set; }
    public string? DireccionServicio { get; set; }

    /// <summary>Texto libre del PDF.</summary>
    public string? Condiciones { get; set; }

    /// <summary>Nota interna. <b>No</b> va al PDF.</summary>
    public string? Observaciones { get; set; }

    // ── Ciclo de vida ───────────────────────────────────────────────────────────────

    public DateTime? EnviadaUtc { get; set; }
    public DateTime? AprobadaUtc { get; set; }
    public Guid? AprobadaPor { get; set; }
    public DateTime? AnuladaUtc { get; set; }
    public Guid? AnuladaPor { get; set; }
    public string? MotivoAnulacion { get; set; }

    /// <summary>Cuando pasa a orden de trabajo (Fase 11).</summary>
    public DateTime? ConvertidaUtc { get; set; }

    /// <summary>FK → <c>dbo.OrigenDatoCatalogo</c>.</summary>
    public byte OrigenDatoId { get; set; }

    public ICollection<DetalleInsumo> DetalleInsumos { get; set; } = [];

    public ICollection<DetalleAmortizable> DetalleAmortizables { get; set; } = [];

    public ICollection<CotizacionAprobacion> Aprobaciones { get; set; } = [];
}

/// <summary>
/// Réplica de <c>Tabla1[E]</c> (residencial) y <c>Tabla16[E]</c> (empresarial).
/// RN-01: <c>Total = PrecioUnitario × Cantidad</c>.
/// </summary>
public class DetalleInsumo : BaseEntity
{
    public Guid CotizacionId { get; set; }

    public Cotizacion Cotizacion { get; set; } = null!;

    public Guid ProductoId { get; set; }

    public Producto Producto { get; set; } = null!;

    /// <summary>FK → <c>dbo.SemestreCatalogo</c>. <c>null</c> = indistinto (residencial).</summary>
    public byte? SemestreId { get; set; }

    public SemestreCatalogo? Semestre { get; set; }

    /// <summary><c>decimal(18,4)</c>: admite fracciones (ml, gr, horas, km).</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Snapshot del <c>CostoActual</c> del producto al momento de cotizar.</summary>
    public decimal PrecioUnitario { get; set; }

    /// <summary>RN-01. Lo calcula el motor, no SQL.</summary>
    public decimal Total { get; set; }

    public Guid? UnidadMedidaId { get; set; }

    /// <summary>Abreviatura congelada para el PDF, por si el producto cambia de unidad.</summary>
    public string? UnidadMedidaTexto { get; set; }

    /// <summary>Nombre congelado para el PDF.</summary>
    public string? ProductoNombre { get; set; }

    /// <summary>Orden en el PDF.</summary>
    public short Orden { get; set; }

    public string? Observacion { get; set; }
}

/// <summary>
/// Réplica de <c>Tabla475[E]</c> y <c>Tabla47[E]</c>.
/// RN-02: <c>ValorMensual = (PrecioUnitario × Cantidad) / MesesVidaUtil</c>.
/// <para>⚠️ <b>H-04</b>: el Excel trae <c>Meses = 0</c> en las 13 filas de <c>Tabla47</c>, lo que
/// dejaba el valor mensual en 0 <b>en silencio</b>. Acá <c>MesesVidaUtil</c> es <c>NOT NULL</c> +
/// <c>CHECK &gt; 0</c>, y el motor no puede recibir un 0.</para>
/// </summary>
public class DetalleAmortizable : BaseEntity
{
    public Guid CotizacionId { get; set; }

    public Cotizacion Cotizacion { get; set; } = null!;

    public Guid ActivoAmortizableId { get; set; }

    public ActivoAmortizable ActivoAmortizable { get; set; } = null!;

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    /// <summary>Snapshot. Nunca 0.</summary>
    public int MesesVidaUtil { get; set; }

    /// <summary><c>PrecioUnitario × Cantidad</c>.</summary>
    public decimal Total { get; set; }

    /// <summary>RN-02.</summary>
    public decimal ValorMensual { get; set; }

    public string? UnidadMedidaTexto { get; set; }

    /// <summary>Nombre congelado para el PDF.</summary>
    public string? ActivoNombre { get; set; }

    public short Orden { get; set; }

    public string? Observacion { get; set; }
}

/// <summary>
/// Flujo de aprobación con <b>enlace público por token</b>, sin login, tal como pide el SRS.
/// <para>El token <b>nunca</b> se guarda en claro: solo su SHA-256, y con índice único para que
/// el lookup del enlace sea un seek.</para>
/// </summary>
public class CotizacionAprobacion : BaseEntity
{
    public Guid CotizacionId { get; set; }

    public Cotizacion Cotizacion { get; set; } = null!;

    /// <summary><c>1, 2…</c> si hay varios niveles de aprobación.</summary>
    public short Secuencia { get; set; } = 1;

    /// <summary>FK → <c>dbo.NivelAprobacionCatalogo</c>. Dice <b>por qué</b> se pidió (D-05).</summary>
    public byte NivelId { get; set; }

    /// <summary>FK → <c>dbo.EstadoAprobacionCatalogo</c>.</summary>
    public byte EstadoId { get; set; }

    public EstadoAprobacionCatalogo? Estado { get; set; }

    /// <summary>Texto legible: "Margen 3 % &lt; 5 %".</summary>
    public string? MotivoDisparo { get; set; }

    public decimal? MargenDisparador { get; set; }

    public decimal? MontoDisparador { get; set; }

    public Guid SolicitadoPor { get; set; }

    public DateTime SolicitadoUtc { get; set; }

    /// <summary><c>null</c> si se aprobó por el enlace público (sin usuario).</summary>
    public Guid? Aprobador { get; set; }

    /// <summary>Snapshot del nombre: la aprobación es un documento y el usuario puede irse.</summary>
    public string? NombreAprobador { get; set; }

    public string? CorreoAprobador { get; set; }

    /// <summary><c>true</c> = vino del enlace público.</summary>
    public bool RespuestaPorToken { get; set; }

    public DateTime? FechaRespuesta { get; set; }

    public string? Comentario { get; set; }

    /// <summary>SHA-256 del token del enlace.</summary>
    public byte[]? TokenHash { get; set; }

    public DateTime? TokenExpira { get; set; }

    /// <summary>Un token = un solo uso.</summary>
    public DateTime? TokenUsadoUtc { get; set; }
}
