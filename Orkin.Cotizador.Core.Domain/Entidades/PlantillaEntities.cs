namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Cabecera de la plantilla de orden de trabajo <b>legacy</b>. Se mapea directamente a
/// <c>dbo.Plantilla_Orden_Trabajo</c>: la migración de esta plataforma NO crea ni reemplaza la
/// tabla. Lo que se migra es la forma de gestionarla (API + frontend), no su estructura.
/// <para>
/// No hereda de <see cref="Comun.BaseEntity"/>: la tabla no tiene <c>Id</c>, <c>CreatedAt</c> ni
/// <c>RowVersion</c>. Su clave es <c>PLANTILLA</c> (bigint, sin identity) y la auditoría propia
/// son <c>FECHA_CREACION</c> y <c>USUARIO_CREACION</c>. La baja lógica es <c>ACTIVO</c> con los
/// mismos códigos del ERP: <c>SI</c> / <c>NO</c>.
/// </para>
/// </summary>
public class PlantillaOrdenTrabajo
{
    /// <summary>Clave legacy. No es identity: el servicio asigna <c>MAX(PLANTILLA) + 1</c>.</summary>
    public long Plantilla { get; set; }

    /// <summary>Código de cliente de SOFTLAND (varchar 20). No se modela como FK.</summary>
    public string? Cliente { get; set; }

    /// <summary>Código de <c>vvLocacionesClientes</c> (varchar 5).</summary>
    public string? Locacion { get; set; }

    public string? Direccion { get; set; }

    /// <summary>Código de <c>vvTecnicos</c> (varchar 4). Vacío en las plantillas actuales.</summary>
    public string? Tecnico { get; set; }

    public string? Contacto { get; set; }

    public string? Telefono { get; set; }

    /// <summary>Código de <c>vvVehiculos</c> (varchar 4).</summary>
    public string? Vehiculo { get; set; }

    /// <summary><c>DESCRIP_TRABAJO</c>. Es el "nombre" visible de la plantilla en el legacy.</summary>
    public string? DescripTrabajo { get; set; }

    public string? Notas { get; set; }

    /// <summary><c>SI</c> = activa, <c>NO</c> = dada de baja. Mismos códigos que el WinForms.</summary>
    public string Activo { get; set; } = "SI";

    public DateOnly? FechaCreacion { get; set; }

    public string? UsuarioCreacion { get; set; }
}

/// <summary>
/// Línea de producto de <c>dbo.Plantilla_Orden_Trabajo_Detalle</c>. Clave compuesta
/// <c>(PLANTILLA, CONSECUTIVO)</c>, exactamente como en la tabla legacy.
/// </summary>
public class PlantillaOrdenTrabajoDetalle
{
    public int Plantilla { get; set; }

    public int Consecutivo { get; set; }

    /// <summary>Código de artículo de SOFTLAND (varchar 10).</summary>
    public string? CodigoArticulo { get; set; }

    public string? DescripArticulo { get; set; }

    /// <summary>Código de <c>vvTipodePlagas</c>.</summary>
    public string? TipoPlaga { get; set; }

    /// <summary>Código de <c>vvTipodeUbicaciones</c> (<c>LUGAR_TRATAR</c>).</summary>
    public string? LugarTratar { get; set; }

    public decimal? Area { get; set; }

    public string? Concentracion { get; set; }

    /// <summary>Código de <c>vvDosificacion</c>.</summary>
    public string? CodigoDosificacion { get; set; }

    /// <summary>Código de <c>vvMetodosAplicacion</c>.</summary>
    public string? MetodoAplicacion { get; set; }

    public decimal? Cantidad { get; set; }

    /// <summary>Unidad de venta tal como viene del ERP (varchar 6), sin normalizar.</summary>
    public string? Umedida { get; set; }

    public decimal? CostoUnitario { get; set; }

    public decimal? CostoTotal { get; set; }

    /// <summary>
    /// <c>MAXIMO_PERMITIDO</c>. El copiador del WinForms lo omitía; el de la API lo conserva.
    /// </summary>
    public decimal? MaximoPermitido { get; set; }
}

/// <summary>
/// Línea de servicio de <c>dbo.Plantilla_Orden_Trabajo_Servicio</c>. La clave legacy tiene cuatro
/// columnas <c>(PLANTILLA, CONSECUTIVO, CODIGO_SERVICIO, CODIGO_FRECUENCIA)</c> y se respeta tal cual.
/// </summary>
public class PlantillaOrdenTrabajoServicio
{
    public int Plantilla { get; set; }

    public int Consecutivo { get; set; }

    public string CodigoServicio { get; set; } = null!;

    public string CodigoFrecuencia { get; set; } = null!;
}

/// <summary>
/// Línea de equipo de <c>dbo.Plantilla_Orden_Trabajo_Equipos</c>. <c>UTILIZADO</c> usa los mismos
/// códigos del legacy (<c>SI</c> / <c>NO</c>), no un <c>bool</c> de la plataforma.
/// </summary>
public class PlantillaOrdenTrabajoEquipos
{
    public int Plantilla { get; set; }

    public int Consecutivo { get; set; }

    public string? CodigoEquipo { get; set; }

    public string? Utilizado { get; set; }
}
