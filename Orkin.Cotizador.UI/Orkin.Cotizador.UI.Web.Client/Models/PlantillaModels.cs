namespace Orkin.Cotizador.UI.Web.Client.Models;

public sealed class PlantillaModel
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public Guid? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public string? LocacionCodigo { get; set; }
    public string? Direccion { get; set; }
    public Guid? TecnicoId { get; set; }
    public string? TecnicoNombre { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? VehiculoCodigo { get; set; }
    public string? DescripcionTrabajo { get; set; }
    public string? Notas { get; set; }
    public bool EsActivo { get; set; } = true;
    public List<PlantillaDetalleModel> Detalles { get; set; } = [];
    public List<PlantillaServicioModel> Servicios { get; set; } = [];
    public List<PlantillaEquipoModel> Equipos { get; set; } = [];
}

public sealed class PlantillaDetalleModel
{
    public Guid? Id { get; set; }
    public int Consecutivo { get; set; }
    public Guid? ProductoId { get; set; }
    public string? ProductoNombre { get; set; }
    public string? DescripcionArticulo { get; set; }
    public string? TipoPlagaCodigo { get; set; }
    public string? LugarTratarCodigo { get; set; }
    public string? Area { get; set; }
    public string? Concentracion { get; set; }
    public string? DosificacionCodigo { get; set; }
    public string? MetodoAplicacionCodigo { get; set; }
    public decimal Cantidad { get; set; }
    public Guid? UnidadMedidaId { get; set; }
    public string? UnidadAbreviatura { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal MaximoPermitido { get; set; }
}

public sealed class PlantillaServicioModel
{
    public Guid? Id { get; set; }
    public int Consecutivo { get; set; }
    public Guid? TipoServicioId { get; set; }
    public string? TipoServicioNombre { get; set; }
    public string? FrecuenciaCodigo { get; set; }
}

public sealed class PlantillaEquipoModel
{
    public Guid? Id { get; set; }
    public int Consecutivo { get; set; }
    public string? EquipoCodigo { get; set; }
    public bool Utilizado { get; set; }
}
