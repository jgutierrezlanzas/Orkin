using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Cliente de ORKIN. El Excel <b>no tiene</b> tabla de clientes: es una de las cosas que la web
/// agrega y de las que más valor aporta.
/// <para>⚠️ <b>ADR-018</b>: <see cref="TipoClienteId"/> es FK a <c>dbo.TipoClienteCatalogo</c>, no
/// un <c>enum</c>. Cuál método de costeo se le aplica sale de
/// <c>TipoClienteCatalogo.MetodoCosteoId</c>, tampoco compilado.</para>
/// </summary>
public class Cliente : BaseEntity
{
    /// <summary>Correlativo interno (<c>CLI-0001</c>). Lo genera <c>dbo.Consecutivo</c>.</summary>
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>Solo en clientes empresariales.</summary>
    public string? RazonSocial { get; set; }

    /// <summary>FK → <c>dbo.TipoClienteCatalogo</c>. Determina el método de costeo (RN-04 / RN-07).</summary>
    public byte TipoClienteId { get; set; }

    public TipoClienteCatalogo? TipoCliente { get; set; }

    public string? CedulaORuc { get; set; }

    /// <summary>RT/NIT del cliente, si el ERP lo trae.</summary>
    public string? IdentificacionFiscal { get; set; }

    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Departamento { get; set; }
    public string? Telefono { get; set; }

    /// <summary>Destinatario del PDF de la cotización.</summary>
    public string? Correo { get; set; }

    public string? ContactoNombre { get; set; }
    public string? ContactoCargo { get; set; }
    public string? SitioWeb { get; set; }
    public string? Notas { get; set; }

    /// <summary>FK → <c>dbo.OrigenDatoCatalogo</c>.</summary>
    public byte OrigenDatoId { get; set; }

    /// <summary>FK → <c>int.ErpConexion</c>. <c>null</c> si es manual.</summary>
    public Guid? ErpConexionId { get; set; }

    public string? CodigoExterno { get; set; }
    public byte[]? HashContenido { get; set; }
    public DateTime? UltimaSyncUtc { get; set; }
}

/// <summary>
/// Insumo consumible (insecticida, rodenticida, cebo…). Consolida las cuatro tablas del Excel
/// (<c>Tabla1</c>, <c>Tabla16</c>, <c>Tabla1912</c>, <c>Tabla10</c>).
/// <para>⚠️ <b>H-10</b>: hay solapamiento entre esas cuatro, así que la importación debe
/// consolidarlas en <b>una sola</b> tabla de productos (tarea 2.6).</para>
/// </summary>
public class Producto : BaseEntity
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public Guid? CategoriaId { get; set; }

    public CategoriaProducto? Categoria { get; set; }

    public Guid UnidadMedidaId { get; set; }

    public UnidadMedida UnidadMedida { get; set; } = null!;

    /// <summary>
    /// Lo que ORKIN paga. <c>decimal(18,6)</c> <b>sin redondeo intermedio</b>: el Excel trae
    /// precios como <c>9.999444</c> y <c>415.261225</c> (ADR-014).
    /// </summary>
    public decimal CostoActual { get; set; }

    /// <summary>Precio de lista. Informativo: el motor de cálculo usa <see cref="CostoActual"/>.</summary>
    public decimal PrecioVentaActual { get; set; }

    /// <summary>
    /// ⚠️ <b>H-04</b>: en el Excel los 19 activos no traen vida útil. Acá es obligatoria
    /// (<c>NOT NULL</c> + <c>CHECK &gt; 0</c>), lo que bloquea la carga hasta que se resuelva
    /// <b>D-04</b>. Para consumibles se deja en 0.
    /// </summary>
    public int? VidaUtilMeses { get; set; }

    /// <summary>FK → <c>dbo.OrigenPrecioCatalogo</c> (Manual / Excel / ERP).</summary>
    public byte OrigenPrecioId { get; set; }

    public Guid? ErpConexionId { get; set; }
    public string? CodigoExterno { get; set; }
    public byte[]? HashContenido { get; set; }
    public DateTime? UltimaSyncUtc { get; set; }

    public ICollection<PrecioProducto> Precios { get; set; } = [];
}

/// <summary>
/// Historial de precios de un producto. Una fila por cambio, con vigencia. Es lo que permite
/// responder "¿por qué el insecticide cuesta 412 el 15 de marzo?".
/// </summary>
public class PrecioProducto : BaseEntity
{
    public Guid ProductoId { get; set; }

    public Producto Producto { get; set; } = null!;

    public DateOnly VigenteDesde { get; set; }

    /// <summary><c>null</c> = vigente. Solo un vigente por producto (índice único filtrado).</summary>
    public DateOnly? VigenteHasta { get; set; }

    public decimal Costo { get; set; }

    public decimal PrecioVenta { get; set; }

    /// <summary>FK → <c>dbo.OrigenPrecioCatalogo</c>.</summary>
    public byte OrigenPrecioId { get; set; }
}

/// <summary>
/// Activo amortizable (trampa, lámpara UV, cebadero, equipo de aplicación).
/// A diferencia del producto, <b>se descuenta por meses</b> dentro de la cotización.
/// </summary>
public class ActivoAmortizable : BaseEntity
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public Guid? CategoriaId { get; set; }

    public CategoriaProducto? Categoria { get; set; }

    public Guid UnidadMedidaId { get; set; }

    public UnidadMedida UnidadMedida { get; set; } = null!;

    public decimal CostoActual { get; set; }

    /// <summary>⚠️ <b>H-04</b>: obligatorio y <c>&gt; 0</c>. Bloquea la carga hasta resolver <b>D-04</b>.</summary>
    public int MesesVidaUtil { get; set; }

    /// <summary>Valor residual. Lo normal es 0, pero se guarda porque el ERP puede traerlo.</summary>
    public decimal ValorResidual { get; set; }

    /// <summary>
    /// Cuántos activos hay. El motor calcula <c>Costo / MesesVidaUtil × Meses</c>; si hay varios
    /// iguales, la cantidad multiplica.
    /// </summary>
    public decimal Cantidad { get; set; } = 1;

    /// <summary>FK → <c>dbo.OrigenPrecioCatalogo</c>.</summary>
    public byte OrigenPrecioId { get; set; }

    public Guid? ErpConexionId { get; set; }
    public string? CodigoExterno { get; set; }
    public byte[]? HashContenido { get; set; }
    public DateTime? UltimaSyncUtc { get; set; }

    public ICollection<PrecioActivo> Precios { get; set; } = [];
}

/// <summary>Historial de precios de un activo amortizable.</summary>
public class PrecioActivo : BaseEntity
{
    public Guid ActivoAmortizableId { get; set; }

    public ActivoAmortizable ActivoAmortizable { get; set; } = null!;

    public DateOnly VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public decimal Costo { get; set; }

    /// <summary>FK → <c>dbo.OrigenPrecioCatalogo</c>.</summary>
    public byte OrigenPrecioId { get; set; }
}

/// <summary>
/// Tipo de cambio córdoba/dólar.
/// <para>⚠️ <b>H-01</b> / RN-05: la cotización toma el <b>último valor cuya fecha sea ≤ la fecha de
/// la cotización</b>, no el valor de hoy.</para>
/// </summary>
public class TipoCambio : BaseEntity
{
    public DateOnly Fecha { get; set; }

    public decimal Valor { get; set; }

    /// <summary>FK → <c>dbo.FuenteTipoCambioCatalogo</c>. Depende de <b>D-06</b>.</summary>
    public byte FuenteId { get; set; }

    public FuenteTipoCambioCatalogo? Fuente { get; set; }

    /// <summary>FK → <c>dbo.OrigenDatoCatalogo</c>.</summary>
    public byte OrigenDatoId { get; set; }

    public Guid? ErpConexionId { get; set; }
    public string? CodigoExterno { get; set; }
    public byte[]? HashContenido { get; set; }
    public DateTime? UltimaSyncUtc { get; set; }
}
