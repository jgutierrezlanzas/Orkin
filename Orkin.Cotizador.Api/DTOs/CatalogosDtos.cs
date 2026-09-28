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
