namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>Reflejo de <c>Plantilla_Orden_Trabajo</c> (D-22): la clave es el número legacy.</summary>
public sealed class PlantillaModel
{
    public long Plantilla { get; set; }
    public string? Cliente { get; set; }
    public string? ClienteNombre { get; set; }
    public string? Locacion { get; set; }
    public string? Direccion { get; set; }
    public string? Tecnico { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Vehiculo { get; set; }
    public string? DescripcionTrabajo { get; set; }
    public string? Notas { get; set; }
    public string Activo { get; set; } = "SI";
    public DateOnly? FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; }
    public int DetallesCount { get; set; }
    public int ServiciosCount { get; set; }
    public int EquiposCount { get; set; }
    public List<PlantillaDetalleModel> Detalles { get; set; } = [];
    public List<PlantillaServicioModel> Servicios { get; set; } = [];
    public List<PlantillaEquipoModel> Equipos { get; set; } = [];
}

public sealed class PlantillaDetalleModel
{
    public int Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? DescripArticulo { get; set; }
    public string? TipoPlaga { get; set; }
    public string? LugarTratar { get; set; }
    public decimal? Area { get; set; }
    public string? Concentracion { get; set; }
    public string? CodigoDosificacion { get; set; }
    public string? MetodoAplicacion { get; set; }
    public decimal? Cantidad { get; set; }
    public string? Umedida { get; set; }
    public decimal? CostoUnitario { get; set; }
    public decimal? CostoTotal { get; set; }
    public decimal? MaximoPermitido { get; set; }
}

public sealed class PlantillaServicioModel
{
    public int Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string CodigoServicio { get; set; } = string.Empty;
    public string CodigoFrecuencia { get; set; } = string.Empty;
}

public sealed class PlantillaEquipoModel
{
    public int Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string? CodigoEquipo { get; set; }
    public string? Utilizado { get; set; }
}
