namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>
/// Modelos de la Fase 4. La clave es la PK real de <c>dbo.Orden_Trabajo</c>
/// (<c>ORDEN_NUMERO + ID_VISITA + PLANTILLA</c>): no se inventa un identificador nuevo (D-21).
/// </summary>

public sealed class OrdenModel
{
    public string OrdenNumero { get; set; } = string.Empty;
    public long IdVisita { get; set; }
    public long Plantilla { get; set; }
    public string? Cliente { get; set; }
    public string? ClienteNombre { get; set; }
    public DateOnly? FechaOrden { get; set; }
    public TimeSpan? Hora { get; set; }
    public string? Direccion { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Vehiculo { get; set; }
    public string? ServicioRealizado { get; set; }
    public string? DescripTrabajo { get; set; }
    public string? Notas { get; set; }
    public string? Instrucciones { get; set; }
    public string? Estado { get; set; }
    public DateOnly? FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; }

    public int Detalles { get; set; }
    public int Servicios { get; set; }
    public int Equipos { get; set; }
    public int Tecnicos { get; set; }

    public List<OrdenProductoModel> Productos { get; set; } = [];
    public List<OrdenServicioModel> ServiciosDetalle { get; set; } = [];
    public List<OrdenEquipoModel> EquiposDetalle { get; set; } = [];
    public List<OrdenTecnicoModel> TecnicosDetalle { get; set; } = [];

    /// <summary>Segmento de ruta con la clave compuesta de la tabla legacy.</summary>
    public string Ruta => $"{Uri.EscapeDataString(OrdenNumero)}/{IdVisita}/{Plantilla}";
}

public sealed class OrdenProductoModel
{
    public int Consecutivo { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? DescripcionArticulo { get; set; }
    public string? TipoPlaga { get; set; }
    public string? LugarTratar { get; set; }
    public decimal? Area { get; set; }
    public string? Concentracion { get; set; }
    public string? CodigoDosificacion { get; set; }
    public string? MetodoAplicacion { get; set; }
    public decimal? Cantidad { get; set; }
    public string? Umedida { get; set; }
    public decimal? CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
}

public sealed class OrdenServicioModel
{
    public long Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string CodigoServicio { get; set; } = string.Empty;
    public string CodigoFrecuencia { get; set; } = string.Empty;
}

public sealed class OrdenEquipoModel
{
    public int Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string? CodigoEquipo { get; set; }
    public string? Utilizado { get; set; }
}

public sealed class OrdenTecnicoModel
{
    public long Plantilla { get; set; }
    public int Consecutivo { get; set; }
    public string CodigoTecnico { get; set; } = string.Empty;
    public string Iniciales { get; set; } = string.Empty;
}
