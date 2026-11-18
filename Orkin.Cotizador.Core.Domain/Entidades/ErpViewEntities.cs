namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Proyecciones de solo lectura sobre las vistas <c>vv*</c> del ERP. No copian catálogos (D-20):
/// solo permiten validar, desde la API, los códigos que el WinForms validaba contra esas mismas
/// vistas. Se mapean <c>HasNoKey</c> y <c>ExcludeFromMigrations</c>: EF las consulta y nunca las
/// crea ni altera.
/// </summary>

public class VistaClienteErp
{
    public string Cliente { get; set; } = null!;
    public string? NombreCliente { get; set; }
    /// <summary>CAT_CLIENTE del ERP (C01/C02/C03); alimenta el tipo de cliente del cotizador.</summary>
    public string? CategoriaCliente { get; set; }
    public string? Activo { get; set; }
}

public class VistaLocacionErp
{
    public string Codigo { get; set; } = null!;
    public string? Nombre { get; set; }
    public string? Cliente { get; set; }
    public string? Activo { get; set; }
}

public class VistaTipoServicioErp
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? TipoActividad { get; set; }
    public string? Activo { get; set; }
}

public class VistaVehiculoErp
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? Placa { get; set; }
    public string? TipoVehiculo { get; set; }
    public string? Activo { get; set; }
}

public class VistaTecnicoErp
{
    public string Codigo { get; set; } = null!;
    public string? Iniciales { get; set; }
    public string? NombreCompleto { get; set; }
    public string? Activo { get; set; }
}

/// <summary>
/// <c>vvBodegas</c>: bodegas activas del ERP. Es la única fuente de bodegas (D-20/D-21); la
/// requisa no copia este catálogo, solo guarda el código en <c>Requisa_Detalle.BODEGA</c>.
/// </summary>
public class VistaBodegaErp
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? Tipo { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? ConsecutivoTraslados { get; set; }
    public string? BodegaPrincipal { get; set; }
    public string? Activo { get; set; }
}

/// <summary>
/// <c>vvArticulos</c>: origen de la descripción real de los artículos de un documento de
/// inventario. El detalle legacy hace <c>INNER JOIN vvArticulos ON ARTICULO = CODIGO_ARTICULO</c>;
/// acá se consulta la misma vista, sin copiar el catálogo (D-20).
/// </summary>
public class VistaArticuloErp
{
    public string CodigoArticulo { get; set; } = null!;
    public string? Clasificacion { get; set; }
    public string? Descripcion { get; set; }
    public string? DescripArticulo { get; set; }
    public string? UnidadAlmacen { get; set; }
    public string? UnidadEmpaque { get; set; }
    public string? UnidadVenta { get; set; }
    public decimal? CostoUnitario { get; set; }
    public string? Tipo { get; set; }
    public string? ArticuloCuenta { get; set; }
    public string? CCOSTO { get; set; }
    public string? CuentaCosto { get; set; }
    public string? Amortizable { get; set; }
    public string? Activo { get; set; }
}

/// <summary>Contrato mínimo de las vistas que solo aportan código, descripción y activo.</summary>
public interface IOpcionErpView
{
    string Codigo { get; set; }
    string? Descripcion { get; set; }
    string? Activo { get; set; }
}

/// <summary><c>vvTipodePlagas</c>: TIPO_PLAGA del detalle de plantilla.</summary>
public class VistaPlagaErp : IOpcionErpView
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? Activo { get; set; }
}

/// <summary><c>vvTipodeUbicaciones</c>: LUGAR_TRATAR del detalle de plantilla.</summary>
public class VistaUbicacionErp : IOpcionErpView
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? Activo { get; set; }
}

/// <summary><c>vvDosificacion</c>: CODIGO_DOSIFICACION del detalle de plantilla.</summary>
public class VistaDosificacionErp
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public decimal? Cantidad { get; set; }
    public string? CodigoUnidadMedida { get; set; }
    public string? Activo { get; set; }
}

/// <summary><c>vvMetodosAplicacion</c>: METODO_APLICACION del detalle de plantilla.</summary>
public class VistaMetodoAplicacionErp : IOpcionErpView
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? Activo { get; set; }
}

/// <summary><c>vvFrecuencia</c>: CODIGO_FRECUENCIA del servicio de plantilla.</summary>
public class VistaFrecuenciaErp
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int? NumeroServicios { get; set; }
    public string? Activo { get; set; }
}

/// <summary><c>vvTipodeEquipos</c>: CODIGO_EQUIPO de la plantilla.</summary>
public class VistaEquipoErp : IOpcionErpView
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? Activo { get; set; }
}
