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

public sealed class ProductoModel
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public Guid? CategoriaId { get; set; }
    public string? CategoriaNombre { get; set; }
    public Guid UnidadMedidaId { get; set; }
    public string? UnidadMedidaNombre { get; set; }
    public string? UnidadMedidaAbreviatura { get; set; }
    public decimal CostoActual { get; set; }
    public decimal PrecioVentaActual { get; set; }
    public int? VidaUtilMeses { get; set; }
    public bool EsActivo { get; set; }
}

public sealed class ClienteModel
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? RazonSocial { get; set; }
    public byte TipoClienteId { get; set; }
    public string? TipoClienteNombre { get; set; }
    public string? CedulaORuc { get; set; }
    public string? IdentificacionFiscal { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Departamento { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? ContactoNombre { get; set; }
    public string? ContactoCargo { get; set; }
    public string? SitioWeb { get; set; }
    public string? Notas { get; set; }
    public bool EsActivo { get; set; }
}

public sealed class TipoClienteOption
{
    public byte Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
