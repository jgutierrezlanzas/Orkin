namespace Orkin.Cotizador.UI.Web.Client.Models;

public sealed class UnidadMedidaModel
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Abreviatura { get; set; } = string.Empty;
    public bool PermiteDecimales { get; set; }
    public int Orden { get; set; }
    public bool EsActivo { get; set; }
}

public sealed class CategoriaProductoModel
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool EsActivo { get; set; }
}

public sealed class TipoServicioModel
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public Guid? UnidadMedidaId { get; set; }
    public string? UnidadMedidaNombre { get; set; }
    public bool EsResidencialPorDefecto { get; set; }
    public bool EsEmpresarialPorDefecto { get; set; }
    public int Orden { get; set; }
    public bool EsActivo { get; set; }
}
