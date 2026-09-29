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

/// <summary>
/// Lo que un flujo haría si se ejecutara ahora, sin ejecutarlo. Es la respuesta del botón
/// "Simular": los mismos cuatro contadores de la carga real, más el detalle de qué tocaría
/// cada fila.
/// <para>No es un tipo nuevo con reglas propias: sale de <see cref="ResultadoCarga"/> con la
/// acción pasada a texto. Se traduce en el borde y no en el dominio porque al cliente lo
/// interesa leer "Actualizar", no saber que internamente es el entero 1 de un enum.</para>
/// </summary>
public sealed record SimulacionFlujoResponse(
    string Catalogo,
    int Total,
    int Insertados,
    int Actualizados,
    int Omitidos,
    int Rechazados,

    /// <summary>
    /// <c>true</c> si al menos un campo de al menos una fila cambiaría. Es lo que la pantalla
    /// muestra arriba de todo, porque "no cambia nada" y "cambia el precio del 30% de los
    /// productos" son noticias muy distintas, y una simulación que solo diga "0 / 0 / 111
    /// omitidos" esconde justo el caso que importa.
    /// </summary>
    bool HabriaCambios,

    IReadOnlyList<CambioSimuladoResponse> Cambios,
    IReadOnlyList<RechazoCargaResponse> Rechazos,
    DateTime SimuladoUtc);

/// <summary>Qué haría una fila, en texto: "Insertar", "Actualizar", "Omitir" o "Adoptar".</summary>
public sealed record CambioSimuladoResponse(
    int NumeroFila,
    string? CodigoExterno,
    string Accion,
    IReadOnlyList<CampoSimuladoResponse> Campos);

/// <summary>Un campo que cambiaría, con su valor de antes y el de después.</summary>
public sealed record CampoSimuladoResponse(string Campo, string? Anterior, string? Nuevo);

public sealed record RechazoCargaResponse(
    int NumeroFila,
    string? CodigoExterno,
    string Motivo,
    string? FilaJson);
