namespace Orkin.Cotizador.Api.DTOs;

/// <summary>
/// Contratos de la Fase 5. La identidad de una requisa es la PK real de <c>dbo.Requisa</c>
/// (<c>REQUISA_NUMERO</c>), que el SP de generación copia de la orden de trabajo (D-21/D-22).
/// </summary>

public sealed record RequisaResumenResponse(
    string RequisaNumero,
    string? OrdenNumero,
    string? Cliente,
    string? ClienteNombre,
    string? Locacion,
    string? LocacionNombre,
    DateOnly FechaRequisa,
    DateOnly? FechaOrden,
    string? Estado,
    string? Bodega,
    string? Instrucciones,
    int Lineas,
    int Tecnicos);

public sealed record RequisaLineaResponse(
    int Consecutivo,
    string? Bodega,
    string? CodigoArticulo,
    string? DescripcionArticulo,
    string? Umedida,
    decimal? CantidadEntregada,
    decimal? CantidadUtilizada,
    decimal? CantidadDevuelta,
    decimal? CantidadSobfal,
    decimal? CostoUnitario,
    decimal? CostoTotal,
    string? CentroCosto,
    string? CuentaContable);

public sealed record RequisaTecnicoResponse(
    int Consecutivo,
    string CodigoTecnico,
    string Iniciales,
    string? NombreTecnico);

public sealed record RequisaResponse(
    string RequisaNumero,
    string? OrdenNumero,
    string? Cliente,
    string? ClienteNombre,
    string? Locacion,
    string? LocacionNombre,
    DateOnly FechaRequisa,
    DateOnly? FechaOrden,
    string? Instrucciones,
    string? RequisaPendiente,
    string? Estado,
    DateOnly? FechaCreacion,
    string? UsuarioCreacion,
    string? Bodega,
    // El conteo de la grilla se llama Lineas en RequisaResumenResponse; acá el arreglo lleva otro
    // nombre para que el cliente no intente deserializar una lista dentro de un int (D-21).
    IReadOnlyList<RequisaLineaResponse> LineasDetalle,
    IReadOnlyList<RequisaTecnicoResponse> TecnicosDetalle);

public sealed record RequisaLineaRequest(
    int Consecutivo,
    string? CodigoArticulo,
    string? DescripcionArticulo,
    decimal? CantidadEntregada,
    decimal? CantidadUtilizada,
    decimal? CantidadDevuelta,
    decimal? CostoUnitario,
    string? CentroCosto,
    string? CuentaContable);

public sealed record RequisaTecnicoRequest(
    int Consecutivo,
    string CodigoTecnico,
    string Iniciales);

/// <summary>
/// <c>Bodega</c> es de la cabecera del formulario legacy: al guardar se aplica a todas las líneas,
/// igual que hacía <c>FrmCrudRequisas</c> con <c>txtBodega</c>.
/// </summary>
public sealed record RequisaGuardarRequest(
    string? Cliente,
    DateOnly? FechaRequisa,
    DateOnly? FechaOrden,
    string? Locacion,
    string? Instrucciones,
    string? Bodega,
    IReadOnlyList<RequisaLineaRequest> Lineas,
    IReadOnlyList<RequisaTecnicoRequest> Tecnicos);
