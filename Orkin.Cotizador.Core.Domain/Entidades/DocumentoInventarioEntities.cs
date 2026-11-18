/// <summary>
/// Entidades de la Fase 6 sobre las tablas <b>legacy existentes</b> de documentos de inventario
/// (<c>dbo.Documento_Inv</c> y <c>dbo.Documento_Inv_linea</c>). Se mapean con
/// <c>ExcludeFromMigrations()</c>: EF las lee, pero nunca las crea ni las altera (D-22).
/// <para>Documento_Inv_linea no se escribe desde acá: las líneas las genera
/// <c>SINCO_GENERA_DOCINV_EXACTUS</c> desde la requisa. Se modelan solo para consultar el detalle
/// con las claves reales del ERP (D-21): <c>(PAQUETE_INVENTARIO, DOCUMENTO_INV)</c> y
/// <c>(PAQUETE_INVENTARIO, DOCUMENTO_INV, LINEA_DOC_INV)</c>.</para>
/// </summary>
namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>Cabecera de <c>dbo.Documento_Inv</c>. La clave legacy es compuesta.</summary>
public class DocumentoInventario
{
    public string PaqueteInventario { get; set; } = null!;
    public string DocumentoInv { get; set; } = null!;
    public string? Consecutivo { get; set; }
    public string? OrdenNumero { get; set; }
    public string RequisaNumero { get; set; } = null!;
    public string Referencia { get; set; } = null!;
    public DateTime FechaHorCreacion { get; set; }
    public DateTime FechaDocumento { get; set; }
    public string Seleccionado { get; set; } = null!;
    public string Usuario { get; set; } = null!;
    public string? MensajeSistema { get; set; }
    public string? UsuarioApro { get; set; }
    public DateTime? FechaHoraAprob { get; set; }
    public string? Aprobado { get; set; }
    public string Transferido { get; set; } = null!;
}

/// <summary>Línea de <c>dbo.Documento_Inv_linea</c>, tal como la deja el SP de generación.</summary>
public class DocumentoInventarioLinea
{
    public string PaqueteInventario { get; set; } = null!;
    public string DocumentoInv { get; set; } = null!;
    public int LineaDocInv { get; set; }
    public string AjusteConfig { get; set; } = null!;
    public string? Nit { get; set; }
    public string Articulo { get; set; } = null!;
    public string? Bodega { get; set; }
    public string? Localizacion { get; set; }
    public string? Lote { get; set; }
    public string Tipo { get; set; } = null!;
    public string Subtipo { get; set; } = null!;
    public string Subsubtipo { get; set; } = null!;
    public decimal Cantidad { get; set; }
    // El SP de transferencia toma esta columna de origen para el COSTO_TOTAL_LOCAL de Exactus.
    public decimal? CostoUnitario { get; set; }
    public decimal CostoTotalLocal { get; set; }
    public decimal CostoTotalDolar { get; set; }
    public decimal PrecioTotalLocal { get; set; }
    public decimal PrecioTotalDolar { get; set; }
    public string? BodegaDestino { get; set; }
    public string? LocalizacionDest { get; set; }
    public string? UnidadDistribucion { get; set; }
    public string? CentroCosto { get; set; }
    public string? CuentaContable { get; set; }
    public byte NoteExistsFlag { get; set; }
    public DateTime RecordDate { get; set; }
}
