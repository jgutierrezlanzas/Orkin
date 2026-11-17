using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Plantilla de orden de trabajo (Fase 2). Reemplaza <c>Plantilla_Orden_Trabajo</c> del legacy,
/// con auditoría de la plataforma y baja lógica (<see cref="BaseEntity.IsActive"/>, que aquí
/// corresponde al <c>ACTIVO</c> del legacy).
/// <para>
/// <b>D-20:</b> lo que en el legacy eran columnas que apuntaban a catálogos del ERP —locación,
/// vehículo, tipo de plaga, lugar a tratar, dosificación, método, frecuencia, equipo— se guarda
/// como <b>código texto</b> congelado, sin tabla local ni FK. Los únicos FK son a entidades que
/// sí viven en la plataforma: cliente, técnico, producto, unidad de medida y tipo de servicio.
/// </para>
/// </summary>
public class Plantilla : BaseEntity
{
    /// <summary>Identificador estable y único, en lugar del <c>PLANTILLA bigint</c> del legacy.</summary>
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public Guid? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    /// <summary>Código ERP de <c>vvLocacionesClientes</c>. No se modela como entidad local.</summary>
    public string? LocacionCodigo { get; set; }

    public string? Direccion { get; set; }

    public Guid? TecnicoId { get; set; }

    public Tecnico? Tecnico { get; set; }

    public string? Contacto { get; set; }

    public string? Telefono { get; set; }

    /// <summary>Código ERP de <c>vvTipodeVehiculos</c>. No se modela como entidad local.</summary>
    public string? VehiculoCodigo { get; set; }

    public string? DescripcionTrabajo { get; set; }

    public string? Notas { get; set; }

    public ICollection<PlantillaDetalle> Detalles { get; set; } = new List<PlantillaDetalle>();

    public ICollection<PlantillaServicio> Servicios { get; set; } = new List<PlantillaServicio>();

    public ICollection<PlantillaEquipo> Equipos { get; set; } = new List<PlantillaEquipo>();
}

/// <summary>
/// Línea de producto de una plantilla. Réplica de <c>Plantilla_Orden_Trabajo_Detalle</c>.
/// No lleva <c>IsActive</c>: la línea existe mientras exista su plantilla.
/// </summary>
public class PlantillaDetalle : BaseEntity
{
    public Guid PlantillaId { get; set; }

    public Plantilla Plantilla { get; set; } = null!;

    /// <summary>Equivale al <c>CONSECUTIVO</c> del legacy; se renumera desde 1 al copiar.</summary>
    public int Consecutivo { get; set; }

    public Guid? ProductoId { get; set; }

    public Producto? Producto { get; set; }

    /// <summary>Snapshot de <c>DESCRIP_ARTICULO</c> para no depender del catálogo al imprimir.</summary>
    public string? DescripcionArticulo { get; set; }

    /// <summary>Código ERP de <c>vvTipodePlagas</c>.</summary>
    public string? TipoPlagaCodigo { get; set; }

    /// <summary>Código ERP de <c>vvTipodeUbicaciones</c> (<c>LUGAR_TRATAR</c>).</summary>
    public string? LugarTratarCodigo { get; set; }

    public string? Area { get; set; }

    public string? Concentracion { get; set; }

    /// <summary>Código ERP de <c>vvDosificacion</c>.</summary>
    public string? DosificacionCodigo { get; set; }

    /// <summary>Código ERP de <c>vvMetodosAplicacion</c>.</summary>
    public string? MetodoAplicacionCodigo { get; set; }

    public decimal Cantidad { get; set; }

    public Guid? UnidadMedidaId { get; set; }

    public UnidadMedida? UnidadMedida { get; set; }

    public decimal CostoUnitario { get; set; }

    public decimal CostoTotal { get; set; }

    /// <summary>
    /// <c>MAXIMO_PERMITIDO</c>. El copiador del legacy lo omitía; acá se copia siempre.
    /// </summary>
    public decimal MaximoPermitido { get; set; }
}

/// <summary>
/// Línea de servicio de una plantilla. Réplica de <c>Plantilla_Orden_Trabajo_Servicio</c>.
/// </summary>
public class PlantillaServicio : BaseEntity
{
    public Guid PlantillaId { get; set; }

    public Plantilla Plantilla { get; set; } = null!;

    public int Consecutivo { get; set; }

    /// <summary>FK local: los tipos de servicio sí son un catálogo compartido de la plataforma.</summary>
    public Guid? TipoServicioId { get; set; }

    public TipoServicio? TipoServicio { get; set; }

    /// <summary>Código ERP de <c>vvFrecuencia</c>. No se modela como entidad local.</summary>
    public string? FrecuenciaCodigo { get; set; }
}

/// <summary>
/// Línea de equipo de una plantilla. Réplica de <c>Plantilla_Orden_Trabajo_Equipos</c>.
/// </summary>
public class PlantillaEquipo : BaseEntity
{
    public Guid PlantillaId { get; set; }

    public Plantilla Plantilla { get; set; } = null!;

    public int Consecutivo { get; set; }

    /// <summary>Código ERP de <c>vvTipodeEquipos</c>. No se modela como entidad local.</summary>
    public string? EquipoCodigo { get; set; }

    public bool Utilizado { get; set; }
}
