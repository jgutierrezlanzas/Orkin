namespace Orkin.Cotizador.Api.DTOs;

/// <summary>Respuesta de una unidad de medida.</summary>
public sealed record UnidadMedidaResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string Abreviatura,
    bool PermiteDecimales,
    int Orden,
    bool EsActivo);

/// <summary>Solicitud para crear una unidad de medida.</summary>
public sealed record CrearUnidadMedidaRequest(
    string Codigo,
    string Nombre,
    string Abreviatura,
    bool PermiteDecimales,
    int Orden);

/// <summary>Solicitud para actualizar una unidad de medida.</summary>
public sealed record ActualizarUnidadMedidaRequest(
    string Nombre,
    string Abreviatura,
    bool PermiteDecimales,
    int Orden,
    bool EsActivo);

/// <summary>Respuesta de un técnico del catálogo compartido.</summary>
public sealed record TecnicoResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Telefono,
    string? Correo,
    string? Cargo,
    int Orden,
    bool EsActivo,
    string? ColorCalendario = null);

/// <summary>Solicitud para crear un técnico manual.</summary>
public sealed record CrearTecnicoRequest(
    string Codigo,
    string Nombre,
    string? Telefono,
    string? Correo,
    string? Cargo,
    int Orden,
    string? ColorCalendario = null);

/// <summary>Solicitud para actualizar un técnico.</summary>
public sealed record ActualizarTecnicoRequest(
    string Nombre,
    string? Telefono,
    string? Correo,
    string? Cargo,
    int Orden,
    bool EsActivo,
    string? ColorCalendario = null);

/// <summary>Respuesta de una categoría de producto.</summary>
public sealed record CategoriaProductoResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    int Orden,
    bool EsActivo);

public sealed record CrearCategoriaProductoRequest(
    string Codigo,
    string Nombre,
    string? Descripcion,
    int Orden);

public sealed record ActualizarCategoriaProductoRequest(
    string Nombre,
    string? Descripcion,
    int Orden,
    bool EsActivo);

/// <summary>Respuesta de un tipo de servicio.</summary>
public sealed record TipoServicioResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    Guid? UnidadMedidaId,
    string? UnidadMedidaNombre,
    bool EsResidencialPorDefecto,
    bool EsEmpresarialPorDefecto,
    int Orden,
    bool EsActivo);

public sealed record CrearTipoServicioRequest(
    string Codigo,
    string Nombre,
    string? Descripcion,
    Guid? UnidadMedidaId,
    bool EsResidencialPorDefecto,
    bool EsEmpresarialPorDefecto,
    int Orden);

public sealed record ActualizarTipoServicioRequest(
    string Nombre,
    string? Descripcion,
    Guid? UnidadMedidaId,
    bool EsResidencialPorDefecto,
    bool EsEmpresarialPorDefecto,
    int Orden,
    bool EsActivo);

/// <summary>Respuesta de un producto.</summary>
public sealed record ProductoResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    Guid? CategoriaId,
    string? CategoriaNombre,
    Guid UnidadMedidaId,
    string UnidadMedidaNombre,
    string UnidadMedidaAbreviatura,
    decimal CostoActual,
    decimal PrecioVentaActual,
    int? VidaUtilMeses,
    bool EsActivo);

public sealed record CrearProductoRequest(
    string Codigo,
    string Nombre,
    string? Descripcion,
    Guid? CategoriaId,
    Guid UnidadMedidaId,
    decimal CostoActual,
    decimal PrecioVentaActual,
    int? VidaUtilMeses);

public sealed record ActualizarProductoRequest(
    string Nombre,
    string? Descripcion,
    Guid? CategoriaId,
    Guid UnidadMedidaId,
    decimal CostoActual,
    decimal PrecioVentaActual,
    int? VidaUtilMeses,
    bool EsActivo);

/// <summary>Respuesta de un cliente.</summary>
public sealed record ClienteResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? RazonSocial,
    byte TipoClienteId,
    string TipoClienteNombre,
    string? CedulaORuc,
    string? IdentificacionFiscal,
    string? Direccion,
    string? Ciudad,
    string? Departamento,
    string? Telefono,
    string? Correo,
    string? ContactoNombre,
    string? ContactoCargo,
    string? SitioWeb,
    string? Notas,
    bool EsActivo);

public sealed record TipoClienteOption(
    byte Id,
    string Codigo,
    string Nombre);

public sealed record CrearClienteRequest(
    string Codigo,
    string Nombre,
    string? RazonSocial,
    byte TipoClienteId,
    string? CedulaORuc,
    string? IdentificacionFiscal,
    string? Direccion,
    string? Ciudad,
    string? Departamento,
    string? Telefono,
    string? Correo,
    string? ContactoNombre,
    string? ContactoCargo,
    string? SitioWeb,
    string? Notas);

public sealed record ActualizarClienteRequest(
    string Nombre,
    string? RazonSocial,
    byte TipoClienteId,
    string? CedulaORuc,
    string? IdentificacionFiscal,
    string? Direccion,
    string? Ciudad,
    string? Departamento,
    string? Telefono,
    string? Correo,
    string? ContactoNombre,
    string? ContactoCargo,
    string? SitioWeb,
    string? Notas,
    bool EsActivo);

/// <summary>Respuesta de un tipo de cambio.</summary>
public sealed record TipoCambioResponse(
    Guid Id,
    DateOnly Fecha,
    decimal Valor,
    byte FuenteId,
    string FuenteNombre,
    bool EsActivo);

public sealed record FuenteTipoCambioOption(
    byte Id,
    string Codigo,
    string Nombre);

public sealed record CrearTipoCambioRequest(
    DateOnly Fecha,
    decimal Valor,
    byte FuenteId);

public sealed record ActualizarTipoCambioRequest(
    DateOnly Fecha,
    decimal Valor,
    byte FuenteId,
    bool EsActivo);

/// <summary>Respuesta de una frecuencia de servicio.</summary>
public sealed record FrecuenciaResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    int? NumeroServicios,
    bool Activo,
    int Orden,
    bool EsActivo);

public sealed record CrearFrecuenciaRequest(
    string Codigo,
    string Nombre,
    int? NumeroServicios,
    bool Activo,
    int Orden);

public sealed record ActualizarFrecuenciaRequest(
    string Nombre,
    int? NumeroServicios,
    bool Activo,
    int Orden,
    bool EsActivo);

/// <summary>Respuesta de una locación de cliente.</summary>
public sealed record LocacionResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? ClienteCodigo,
    string? Departamento,
    string? Municipio,
    string? ZonaBarrio,
    string? Contacto1,
    string? Contacto2,
    string? Contacto3,
    string? Telefono,
    string? TelefonoConvencional,
    string? Correo1,
    string? Correo2,
    string? Correo3,
    decimal? Distancia,
    decimal? Latitud,
    decimal? Longitud,
    bool Activo,
    bool Notificar,
    int Orden,
    bool EsActivo);

public sealed record CrearLocacionRequest(
    string Codigo,
    string Nombre,
    string? ClienteCodigo,
    string? Departamento,
    string? Municipio,
    string? ZonaBarrio,
    string? Contacto1,
    string? Contacto2,
    string? Contacto3,
    string? Telefono,
    string? TelefonoConvencional,
    string? Correo1,
    string? Correo2,
    string? Correo3,
    decimal? Distancia,
    decimal? Latitud,
    decimal? Longitud,
    bool Activo,
    bool Notificar,
    int Orden);

public sealed record ActualizarLocacionRequest(
    string Nombre,
    string? ClienteCodigo,
    string? Departamento,
    string? Municipio,
    string? ZonaBarrio,
    string? Contacto1,
    string? Contacto2,
    string? Contacto3,
    string? Telefono,
    string? TelefonoConvencional,
    string? Correo1,
    string? Correo2,
    string? Correo3,
    decimal? Distancia,
    decimal? Latitud,
    decimal? Longitud,
    bool Activo,
    bool Notificar,
    int Orden,
    bool EsActivo);

/// <summary>Respuesta de un método de aplicación.</summary>
public sealed record MetodoAplicacionResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    bool Activo,
    int Orden,
    bool EsActivo);

public sealed record CrearMetodoAplicacionRequest(
    string Codigo,
    string Nombre,
    bool Activo,
    int Orden);

public sealed record ActualizarMetodoAplicacionRequest(
    string Nombre,
    bool Activo,
    int Orden,
    bool EsActivo);

/// <summary>Respuesta de un tipo de plaga.</summary>
public sealed record TipoPlagaResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    bool Activo,
    int Orden,
    bool EsActivo);

public sealed record CrearTipoPlagaRequest(
    string Codigo,
    string Nombre,
    bool Activo,
    int Orden);

public sealed record ActualizarTipoPlagaRequest(
    string Nombre,
    bool Activo,
    int Orden,
    bool EsActivo);

/// <summary>Respuesta de un tipo de ubicación.</summary>
public sealed record TipoUbicacionResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    bool Activo,
    int Orden,
    bool EsActivo);

public sealed record CrearTipoUbicacionRequest(
    string Codigo,
    string Nombre,
    bool Activo,
    int Orden);

public sealed record ActualizarTipoUbicacionRequest(
    string Nombre,
    bool Activo,
    int Orden,
    bool EsActivo);

/// <summary>Respuesta de un vehículo.</summary>
public sealed record VehiculoResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Placa,
    string? Circulacion,
    string? TipoVehiculo,
    decimal? Eficiencia,
    bool Gps,
    DateOnly? FechaGps,
    bool Activo,
    int Orden,
    bool EsActivo);

public sealed record CrearVehiculoRequest(
    string Codigo,
    string Nombre,
    string? Placa,
    string? Circulacion,
    string? TipoVehiculo,
    decimal? Eficiencia,
    bool Gps,
    DateOnly? FechaGps,
    bool Activo,
    int Orden);

public sealed record ActualizarVehiculoRequest(
    string Nombre,
    string? Placa,
    string? Circulacion,
    string? TipoVehiculo,
    decimal? Eficiencia,
    bool Gps,
    DateOnly? FechaGps,
    bool Activo,
    int Orden,
    bool EsActivo);
