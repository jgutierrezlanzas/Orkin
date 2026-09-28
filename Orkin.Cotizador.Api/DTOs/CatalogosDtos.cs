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
