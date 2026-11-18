namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>
/// Modelos de la Fase 5. La clave es la PK real de <c>dbo.Requisa</c>
/// (<c>REQUISA_NUMERO</c>), que es el número de la orden que la generó (D-21).
/// </summary>

public sealed class RequisaModel
{
    public string RequisaNumero { get; set; } = string.Empty;
    public string? OrdenNumero { get; set; }
    public string? Cliente { get; set; }
    public string? ClienteNombre { get; set; }
    public string? Locacion { get; set; }
    public string? LocacionNombre { get; set; }
    public DateOnly FechaRequisa { get; set; }
    public DateOnly? FechaOrden { get; set; }
    public string? Instrucciones { get; set; }
    public string? RequisaPendiente { get; set; }
    public string? Estado { get; set; }
    public DateOnly? FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; }

    /// <summary>Bodega del formulario legacy: se aplica a todas las líneas al guardar.</summary>
    public string? Bodega { get; set; }

    public int Lineas { get; set; }
    public int Tecnicos { get; set; }

    public List<RequisaLineaModel> LineasDetalle { get; set; } = [];
    public List<RequisaTecnicoModel> TecnicosDetalle { get; set; } = [];

    /// <summary>Segmento de ruta con la PK legacy.</summary>
    public string Ruta => Uri.EscapeDataString(RequisaNumero);
}

public sealed class RequisaLineaModel
{
    public int Consecutivo { get; set; }
    public string? Bodega { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? DescripcionArticulo { get; set; }
    public string? Umedida { get; set; }
    public decimal? CantidadEntregada { get; set; }
    public decimal? CantidadUtilizada { get; set; }
    public decimal? CantidadDevuelta { get; set; }
    public decimal? CantidadSobfal { get; set; }
    public decimal? CostoUnitario { get; set; }
    public decimal? CostoTotal { get; set; }
    public string? CentroCosto { get; set; }
    public string? CuentaContable { get; set; }
}

public sealed class RequisaTecnicoModel
{
    public int Consecutivo { get; set; }
    public string CodigoTecnico { get; set; } = string.Empty;
    public string Iniciales { get; set; } = string.Empty;
    public string? NombreTecnico { get; set; }
}
