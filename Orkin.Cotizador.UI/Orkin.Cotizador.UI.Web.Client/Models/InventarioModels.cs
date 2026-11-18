namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>Modelos de la Fase 6. La clave es la PK real de <c>dbo.Documento_Inv</c>.</summary>

public sealed class DocumentoModel
{
    public string PaqueteInventario { get; set; } = string.Empty;
    public string DocumentoInv { get; set; } = string.Empty;
    public string? Consecutivo { get; set; }
    public string? OrdenNumero { get; set; }
    public string RequisaNumero { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public DateTime FechaDocumento { get; set; }
    public DateTime FechaHorCreacion { get; set; }
    public string Transferido { get; set; } = string.Empty;
    public string? Bodega { get; set; }
    public string? NombreBodega { get; set; }
    public int Lineas { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string? MensajeSistema { get; set; }

    // Detalle (el listado no lo trae).
    public string Seleccionado { get; set; } = string.Empty;
    public string? UsuarioApro { get; set; }
    public DateTime? FechaHoraAprob { get; set; }
    public string? Aprobado { get; set; }
    public List<DocumentoLineaModel> LineasDetalle { get; set; } = [];

    /// <summary>Segmentos de ruta ya codificados: <c>{paquete}/{documento}</c>.</summary>
    public string Ruta =>
        $"{Uri.EscapeDataString(PaqueteInventario)}/{Uri.EscapeDataString(DocumentoInv)}";
}

public sealed class DocumentoLineaModel
{
    public int Linea { get; set; }
    public string Articulo { get; set; } = string.Empty;
    public string? DescripcionArticulo { get; set; }
    public string? Unidad { get; set; }
    public string? Bodega { get; set; }
    public string? NombreBodega { get; set; }
    public string? BodegaDestino { get; set; }
    public string? NombreBodegaDestino { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotalLocal { get; set; }
    public string? CentroCosto { get; set; }
    public string? CuentaContable { get; set; }
}

public sealed class ExactusResultadoModel
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public string? ResultadoSp { get; set; }
}
