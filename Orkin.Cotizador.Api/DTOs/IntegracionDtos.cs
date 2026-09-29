namespace Orkin.Cotizador.Api.DTOs;

// ── Conexiones al ERP ───────────────────────────────────────────────────────

/// <summary>
/// Una conexión al ERP de un cliente. La contraseña nunca sale: la conexión la descifra el
/// servidor cuando va a usarla.
/// </summary>
public sealed record ErpConexionResponse(
    Guid Id,
    string Nombre,
    string? Descripcion,
    string Servidor,
    int? Puerto,
    string NombreBaseDatos,
    string? Esquema,
    string? Usuario,
    bool EsSoloLectura,
    int TiempoEsperaSegundos,
    bool TieneContrasena,
    bool? UltimaPruebaOk,
    DateTime? UltimaConexionOkUtc,
    string? UltimoError,
    DateTime? UltimoErrorUtc,
    bool EsActivo,
    int TotalFlujos);

public sealed record CrearErpConexionRequest(
    string Nombre,
    string? Descripcion,
    string Servidor,
    int? Puerto,
    string NombreBaseDatos,
    string? Esquema,
    string? Usuario,
    string? Contrasena,
    int? TiempoEsperaSegundos);

public sealed record ActualizarErpConexionRequest(
    string? Descripcion,
    string Servidor,
    int? Puerto,
    string NombreBaseDatos,
    string? Esquema,
    string? Usuario,
    string? Contrasena,
    int? TiempoEsperaSegundos);

/// <summary>Respuesta de la prueba de conexión.</summary>
public sealed record ProbarConexionResponse(
    bool Exito,
    string Mensaje,
    string? VersionServidor,
    double? Milisegundos);

// ── Flujos ──────────────────────────────────────────────────────────────────

/// <summary>
/// Un flujo ETL: qué catálogo llena, desde qué origen. El origen es una vista, una tabla o una
/// consulta SQL del ERP del cliente, tal como la define su DBA.
/// </summary>
public sealed record ErpFlujoResponse(
    Guid Id,
    Guid ErpConexionId,
    string ConexionNombre,
    string Nombre,
    string? Descripcion,
    string Catalogo,
    string Origen,
    string? MapeoJson,
    bool EsActivo,
    DateTime? UltimaEjecucionUtc,
    int UltimosInsertados,
    int UltimosActualizados,
    int UltimosOmitidos,
    int UltimosRechazados,
    string? UltimoError);

public sealed record CrearErpFlujoRequest(
    Guid ErpConexionId,
    string Nombre,
    string? Descripcion,
    string Catalogo,
    string Origen,
    string? MapeoJson);

public sealed record ActualizarErpFlujoRequest(
    string? Descripcion,
    string Catalogo,
    string Origen,
    string? MapeoJson);

/// <summary>Una corrida del flujo, de la bitácora.</summary>
public sealed record SincronizacionEjecucionResponse(
    Guid Id,
    DateTime InicioUtc,
    DateTime? FinUtc,
    int? DuracionMs,
    string Estado,
    int RegistrosLeidos,
    int RegistrosCreados,
    int RegistrosActualizados,
    int RegistrosSinCambio,
    int RegistrosRechazados,
    string? MensajeError);
