namespace Orkin.Cotizador.Api.DTOs;

/// <summary>Línea de producto de una plantilla (D-20: los códigos ERP son texto).</summary>
public sealed record PlantillaDetalleResponse(
    Guid Id,
    int Consecutivo,
    Guid? ProductoId,
    string? ProductoNombre,
    string? DescripcionArticulo,
    string? TipoPlagaCodigo,
    string? LugarTratarCodigo,
    string? Area,
    string? Concentracion,
    string? DosificacionCodigo,
    string? MetodoAplicacionCodigo,
    decimal Cantidad,
    Guid? UnidadMedidaId,
    string? UnidadAbreviatura,
    decimal CostoUnitario,
    decimal CostoTotal,
    decimal MaximoPermitido);

/// <summary>Línea de servicio de una plantilla.</summary>
public sealed record PlantillaServicioResponse(
    Guid Id,
    int Consecutivo,
    Guid? TipoServicioId,
    string? TipoServicioNombre,
    string? FrecuenciaCodigo);

/// <summary>Línea de equipo de una plantilla.</summary>
public sealed record PlantillaEquipoResponse(
    Guid Id,
    int Consecutivo,
    string? EquipoCodigo,
    bool Utilizado);

/// <summary>Plantilla completa, con sus tres colecciones.</summary>
public sealed record PlantillaResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid? ClienteId,
    string? ClienteNombre,
    string? LocacionCodigo,
    string? Direccion,
    Guid? TecnicoId,
    string? TecnicoNombre,
    string? Contacto,
    string? Telefono,
    string? VehiculoCodigo,
    string? DescripcionTrabajo,
    string? Notas,
    bool EsActivo,
    IReadOnlyList<PlantillaDetalleResponse> Detalles,
    IReadOnlyList<PlantillaServicioResponse> Servicios,
    IReadOnlyList<PlantillaEquipoResponse> Equipos);

/// <summary>Una línea de producto tal como la envía la pantalla. <c>Id</c> vacío = línea nueva.</summary>
public sealed record PlantillaDetalleGuardarRequest(
    Guid? Id,
    int Consecutivo,
    Guid? ProductoId,
    string? DescripcionArticulo,
    string? TipoPlagaCodigo,
    string? LugarTratarCodigo,
    string? Area,
    string? Concentracion,
    string? DosificacionCodigo,
    string? MetodoAplicacionCodigo,
    decimal Cantidad,
    Guid? UnidadMedidaId,
    decimal CostoUnitario,
    decimal CostoTotal,
    decimal MaximoPermitido);

/// <summary>Una línea de servicio tal como la envía la pantalla.</summary>
public sealed record PlantillaServicioGuardarRequest(
    Guid? Id,
    int Consecutivo,
    Guid? TipoServicioId,
    string? FrecuenciaCodigo);

/// <summary>Una línea de equipo tal como la envía la pantalla.</summary>
public sealed record PlantillaEquipoGuardarRequest(
    Guid? Id,
    int Consecutivo,
    string? EquipoCodigo,
    bool Utilizado);

/// <summary>Alta de plantilla con sus líneas.</summary>
public sealed record CrearPlantillaRequest(
    string Codigo,
    string Nombre,
    Guid? ClienteId,
    string? LocacionCodigo,
    string? Direccion,
    Guid? TecnicoId,
    string? Contacto,
    string? Telefono,
    string? VehiculoCodigo,
    string? DescripcionTrabajo,
    string? Notas,
    IReadOnlyList<PlantillaDetalleGuardarRequest> Detalles,
    IReadOnlyList<PlantillaServicioGuardarRequest> Servicios,
    IReadOnlyList<PlantillaEquipoGuardarRequest> Equipos);

/// <summary>Edición de plantilla. Las líneas que no vengan se dan de baja de la colección.</summary>
public sealed record ActualizarPlantillaRequest(
    string Nombre,
    Guid? ClienteId,
    string? LocacionCodigo,
    string? Direccion,
    Guid? TecnicoId,
    string? Contacto,
    string? Telefono,
    string? VehiculoCodigo,
    string? DescripcionTrabajo,
    string? Notas,
    bool EsActivo,
    IReadOnlyList<PlantillaDetalleGuardarRequest> Detalles,
    IReadOnlyList<PlantillaServicioGuardarRequest> Servicios,
    IReadOnlyList<PlantillaEquipoGuardarRequest> Equipos);

/// <summary>Copia de plantilla. Código y nombre son opcionales: si se omiten se generan.</summary>
public sealed record CopiarPlantillaRequest(string? Codigo, string? Nombre);
