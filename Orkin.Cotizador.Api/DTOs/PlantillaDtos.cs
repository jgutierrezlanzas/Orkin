namespace Orkin.Cotizador.Api.DTOs;

/// <summary>
/// Contrato de la API para las tablas legacy de plantillas. Los nombres y los códigos son los de
/// <c>SINCOERPORKIN</c>: no se inventa un <c>Id</c> ni un <c>Codigo</c> nuevos (D-22).
/// </summary>

public sealed record PlantillaDetalleResponse(
    int Plantilla,
    int Consecutivo,
    string? CodigoArticulo,
    string? DescripArticulo,
    string? TipoPlaga,
    string? LugarTratar,
    decimal? Area,
    string? Concentracion,
    string? CodigoDosificacion,
    string? MetodoAplicacion,
    decimal? Cantidad,
    string? Umedida,
    decimal? CostoUnitario,
    decimal? CostoTotal,
    decimal? MaximoPermitido);

public sealed record PlantillaServicioResponse(
    int Plantilla,
    int Consecutivo,
    string CodigoServicio,
    string CodigoFrecuencia);

public sealed record PlantillaEquipoResponse(
    int Plantilla,
    int Consecutivo,
    string? CodigoEquipo,
    string? Utilizado);

/// <summary>Fila de la grilla: cabecera legacy y conteos, sin cargar todas las líneas.</summary>
public sealed record PlantillaResumenResponse(
    long Plantilla,
    string? Cliente,
    string? ClienteNombre,
    string? Locacion,
    string? Direccion,
    string? DescripcionTrabajo,
    string Activo,
    DateOnly? FechaCreacion,
    string? UsuarioCreacion,
    int DetallesCount,
    int ServiciosCount,
    int EquiposCount);

public sealed record PlantillaResponse(
    long Plantilla,
    string? Cliente,
    string? ClienteNombre,
    string? Locacion,
    string? Direccion,
    string? Tecnico,
    string? Contacto,
    string? Telefono,
    string? Vehiculo,
    string? DescripcionTrabajo,
    string? Notas,
    string Activo,
    DateOnly? FechaCreacion,
    string? UsuarioCreacion,
    int DetallesCount,
    int ServiciosCount,
    int EquiposCount,
    IReadOnlyList<PlantillaDetalleResponse> Detalles,
    IReadOnlyList<PlantillaServicioResponse> Servicios,
    IReadOnlyList<PlantillaEquipoResponse> Equipos);

/// <summary>Una línea de producto tal como la envía la pantalla.</summary>
public sealed record PlantillaDetalleGuardarRequest(
    string? CodigoArticulo,
    string? DescripArticulo,
    string? TipoPlaga,
    string? LugarTratar,
    decimal? Area,
    string? Concentracion,
    string? CodigoDosificacion,
    string? MetodoAplicacion,
    decimal? Cantidad,
    string? Umedida,
    decimal? CostoUnitario,
    decimal? MaximoPermitido);

/// <summary>Una línea de servicio tal como la envía la pantalla.</summary>
public sealed record PlantillaServicioGuardarRequest(
    string CodigoServicio,
    string CodigoFrecuencia);

/// <summary>Una línea de equipo tal como la envía la pantalla.</summary>
public sealed record PlantillaEquipoGuardarRequest(
    string? CodigoEquipo,
    string? Utilizado);

/// <summary>Alta de plantilla con sus líneas. La clave <c>PLANTILLA</c> la asigna la API.</summary>
public sealed record CrearPlantillaRequest(
    string? Cliente,
    string? Locacion,
    string? Direccion,
    string? Tecnico,
    string? Contacto,
    string? Telefono,
    string? Vehiculo,
    string? DescripcionTrabajo,
    string? Notas,
    IReadOnlyList<PlantillaDetalleGuardarRequest> Detalles,
    IReadOnlyList<PlantillaServicioGuardarRequest> Servicios,
    IReadOnlyList<PlantillaEquipoGuardarRequest> Equipos);

/// <summary>Edición de plantilla. Las líneas que no vengan se eliminan del conjunto de la plantilla.</summary>
public sealed record ActualizarPlantillaRequest(
    bool Activo,
    string? Cliente,
    string? Locacion,
    string? Direccion,
    string? Tecnico,
    string? Contacto,
    string? Telefono,
    string? Vehiculo,
    string? DescripcionTrabajo,
    string? Notas,
    IReadOnlyList<PlantillaDetalleGuardarRequest> Detalles,
    IReadOnlyList<PlantillaServicioGuardarRequest> Servicios,
    IReadOnlyList<PlantillaEquipoGuardarRequest> Equipos);

/// <summary>Copia de plantilla. La descripción es opcional: si se omite se conserva la original.</summary>
public sealed record CopiarPlantillaRequest(string? DescripcionTrabajo);
