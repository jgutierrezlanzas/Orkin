namespace Orkin.Cotizador.Api.DTOs;

/// <summary>
/// Contratos de la Fase 4. La identidad de una orden es la PK real de <c>dbo.Orden_Trabajo</c>:
/// <c>ORDEN_NUMERO + ID_VISITA + PLANTILLA</c>. No se inventa un <c>Id</c> nuevo (D-21/D-22).
/// </summary>

public sealed record OrdenResumenResponse(
    string OrdenNumero,
    long IdVisita,
    long Plantilla,
    string? Cliente,
    string? ClienteNombre,
    DateOnly? FechaOrden,
    TimeSpan? Hora,
    string? Direccion,
    string? DescripTrabajo,
    string? Estado,
    string? ServicioRealizado,
    int Detalles,
    int Servicios,
    int Equipos,
    int Tecnicos);

public sealed record OrdenProductoResponse(
    int Consecutivo,
    string? CodigoArticulo,
    string? DescripcionArticulo,
    string? TipoPlaga,
    string? LugarTratar,
    decimal? Area,
    string? Concentracion,
    string? CodigoDosificacion,
    string? MetodoAplicacion,
    decimal? Cantidad,
    string? Umedida,
    decimal? CostoUnitario,
    decimal? CostoTotal);

public sealed record OrdenServicioResponse(
    long Plantilla,
    int Consecutivo,
    string CodigoServicio,
    string CodigoFrecuencia);

public sealed record OrdenEquipoResponse(
    int Plantilla,
    int Consecutivo,
    string? CodigoEquipo,
    string? Utilizado);

public sealed record OrdenTecnicoResponse(
    long Plantilla,
    int Consecutivo,
    string CodigoTecnico,
    string Iniciales);

public sealed record OrdenResponse(
    string OrdenNumero,
    long IdVisita,
    long Plantilla,
    string? Cliente,
    string? ClienteNombre,
    DateOnly? FechaOrden,
    TimeSpan? Hora,
    string? Direccion,
    string? Contacto,
    string? Telefono,
    string? Vehiculo,
    string? ServicioRealizado,
    string? DescripTrabajo,
    string? Notas,
    string? Instrucciones,
    string? Estado,
    DateOnly? FechaCreacion,
    string? UsuarioCreacion,
    // Estos nombres coinciden con OrdenModel del cliente. Los conteos de la grilla viven en
    // OrdenResumenResponse con los nombres Detalles/Servicios/Equipos/Tecnicos; si acá se usaran
    // esos mismos nombres para los arreglos, el cliente intentaría deserializar un array en un int.
    IReadOnlyList<OrdenProductoResponse> Productos,
    IReadOnlyList<OrdenServicioResponse> ServiciosDetalle,
    IReadOnlyList<OrdenEquipoResponse> EquiposDetalle,
    IReadOnlyList<OrdenTecnicoResponse> TecnicosDetalle);

public sealed record OrdenProductoRequest(
    int Consecutivo,
    string? CodigoArticulo,
    string? DescripcionArticulo,
    string? TipoPlaga,
    string? LugarTratar,
    decimal? Area,
    string? Concentracion,
    string? CodigoDosificacion,
    string? MetodoAplicacion,
    decimal? Cantidad,
    string? Umedida,
    decimal? CostoUnitario);

public sealed record OrdenServicioRequest(
    int Consecutivo,
    string CodigoServicio,
    string CodigoFrecuencia);

public sealed record OrdenEquipoRequest(
    int Consecutivo,
    string? CodigoEquipo,
    string? Utilizado);

public sealed record OrdenTecnicoRequest(
    int Consecutivo,
    string CodigoTecnico,
    string Iniciales);

public sealed record OrdenGuardarRequest(
    string? Cliente,
    DateOnly? FechaOrden,
    TimeSpan? Hora,
    string? Direccion,
    string? Contacto,
    string? Telefono,
    string? Vehiculo,
    string? ServicioRealizado,
    string? DescripTrabajo,
    string? Notas,
    string? Instrucciones,
    IReadOnlyList<OrdenProductoRequest> Detalles,
    IReadOnlyList<OrdenServicioRequest> Servicios,
    IReadOnlyList<OrdenEquipoRequest> Equipos,
    IReadOnlyList<OrdenTecnicoRequest> Tecnicos);
