namespace Orkin.Cotizador.Api.DTOs;

/// <summary>
/// Contratos de la Fase 6. La identidad de un documento es la PK real de
/// <c>dbo.Documento_Inv</c>: <c>(PAQUETE_INVENTARIO, DOCUMENTO_INV)</c> (D-21/D-22).
/// </summary>

public sealed record DocumentoResumenResponse(
    string PaqueteInventario,
    string DocumentoInv,
    string? Consecutivo,
    string? OrdenNumero,
    string RequisaNumero,
    string Referencia,
    DateTime FechaDocumento,
    DateTime FechaHorCreacion,
    string Transferido,
    string? Bodega,
    string? NombreBodega,
    int Lineas,
    string Usuario,
    string? MensajeSistema);

public sealed record DocumentoLineaResponse(
    int Linea,
    string Articulo,
    string? DescripcionArticulo,
    string? Unidad,
    string? Bodega,
    string? NombreBodega,
    string? BodegaDestino,
    string? NombreBodegaDestino,
    decimal Cantidad,
    decimal CostoUnitario,
    decimal CostoTotalLocal,
    string? CentroCosto,
    string? CuentaContable);

public sealed record DocumentoResponse(
    string PaqueteInventario,
    string DocumentoInv,
    string? Consecutivo,
    string? OrdenNumero,
    string RequisaNumero,
    string Referencia,
    DateTime FechaDocumento,
    DateTime FechaHorCreacion,
    string Seleccionado,
    string Usuario,
    string? MensajeSistema,
    string? UsuarioApro,
    DateTime? FechaHoraAprob,
    string? Aprobado,
    string Transferido,
    IReadOnlyList<DocumentoLineaResponse> LineasDetalle);

/// <summary>
/// <c>Mensaje</c> es el que mostraba el WinForms al terminar; <c>ResultadoSp</c> es el
/// <c>SELECT ... AS RESULTADO</c> que antes se descartaba con <c>ExecuteNonQuery</c>.
/// </summary>
public sealed record ExactusResponse(
    bool Exitoso,
    string Mensaje,
    string? ResultadoSp);
