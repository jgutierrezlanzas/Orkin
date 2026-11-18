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

public sealed class TecnicoModel
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Cargo { get; set; }
    public int Orden { get; set; }
    public bool EsActivo { get; set; }

    /// <summary>Color #RRGGBB de su calendario en la agenda de visitas.</summary>
    public string? ColorCalendario { get; set; }
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

public sealed class TipoCambioModel
{
    public Guid Id { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Valor { get; set; }
    public byte FuenteId { get; set; }
    public string? FuenteNombre { get; set; }
    public bool EsActivo { get; set; }
}

public sealed class FuenteTipoCambioOption
{
    public byte Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>Opción legible de una vista <c>vv*</c>: el código se guarda, el texto se muestra.</summary>
public sealed class OpcionErpModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public string? UnidadMedida { get; set; }
}

public sealed class OpcionesErpModel
{
    public List<OpcionErpModel> Locaciones { get; set; } = [];
    public List<OpcionErpModel> Vehiculos { get; set; } = [];
    public List<OpcionErpModel> Plagas { get; set; } = [];
    public List<OpcionErpModel> Ubicaciones { get; set; } = [];
    public List<OpcionErpModel> Dosificaciones { get; set; } = [];
    public List<OpcionErpModel> Metodos { get; set; } = [];
    public List<OpcionErpModel> Frecuencias { get; set; } = [];
    public List<OpcionErpModel> Equipos { get; set; } = [];
}
