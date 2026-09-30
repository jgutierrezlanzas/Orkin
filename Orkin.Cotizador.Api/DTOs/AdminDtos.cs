namespace Orkin.Cotizador.Api.DTOs;

// ── Usuarios ─────────────────────────────────────────────────────────────────

/// <summary>Solicitud para crear un usuario. La contraseña la elige quien lo crea.</summary>
public sealed record CrearUsuarioRequest(
    string NombreCuenta,
    string NombreCompleto,
    string Correo,
    string Contrasena,
    string? Documento,
    string? Cargo,
    bool EsActivo,
    IReadOnlyList<string> Roles);

/// <summary>Solicitud para editar un usuario. La contraseña no va aquí: se cambia aparte.</summary>
public sealed record ActualizarUsuarioRequest(
    string NombreCompleto,
    string Correo,
    string? Documento,
    string? Cargo,
    bool EsActivo,
    IReadOnlyList<string> Roles);

/// <summary>Solicitud para cambiar la contraseña de un usuario (el administrador la restablece).</summary>
public sealed record CambiarContrasenaUsuarioRequest(string Contrasena, bool ObligarCambio);

public sealed record UsuarioResponse(
    Guid Id,
    string NombreCuenta,
    string NombreCompleto,
    string? Correo,
    string? Documento,
    string? Cargo,
    bool EsActivo,
    bool EsUsuarioSistema,
    bool DebeCambiarContrasena,
    DateTime? UltimoAccesoUtc,
    IReadOnlyList<string> Roles);

// ── Roles y permisos ─────────────────────────────────────────────────────────

public sealed record RolResponse(
    Guid Id,
    string Nombre,
    string? Descripcion,
    bool EsSistema,
    bool EsActivo,
    IReadOnlyList<string> Permisos);

/// <summary>
/// La lista de permisos que un rol puede tener. Va junto con el rol y no dentro de él porque la
/// pantalla de roles necesita el catálogo COMPLETO (para los casilleros apagados) y no solo los
/// que ya tiene el rol.
/// </summary>
public sealed record PermisoResponse(
    string Codigo,
    string Nombre,
    string Modulo,
    string? Descripcion);

/// <summary>Opción para dropdown de activos amortizables.</summary>
public sealed record ActivoAmortizableOption(
    Guid Id,
    string Codigo,
    string Nombre,
    decimal CostoActual,
    int MesesVidaUtil,
    string UnidadAbreviatura);

/// <summary>Opción para dropdown de técnicos. Por ahora salen de los usuarios activos.</summary>
public sealed record TecnicoOption(
    Guid Id,
    string Nombre);

/// <summary>Solicitud para fijar los permisos de un rol. La lista reemplaza la anterior completa.</summary>
public sealed record AsignarPermisosRolRequest(IReadOnlyList<string> Permisos);

/// <summary>Solicitud para crear un rol.</summary>
public sealed record CrearRolRequest(string Nombre, string? Descripcion);

/// <summary>Solicitud para editar un rol (nombre y descripción; los permisos van aparte).</summary>
public sealed record ActualizarRolRequest(string? Nombre, string? Descripcion);

    /// <summary>Los campos que un catálogo espera del origen, para la tabla de homologación.</summary>
    public sealed record CampoEsperadoResponse(
    string Nombre,
    string Tipo,
    bool EsObligatorio,
    string Descripcion);

/// <summary>Una columna que trae la vista del ERP, para cotejarla contra los campos esperados.</summary>
public sealed record ColumnaOrigenResponse(
    string Nombre,
    string Tipo,
    bool PermiteNulos,
    int? Longitud);

/// <summary>
/// Qué columnas leer. Va como cuerpo y no en la URL porque abre una conexión al ERP del cliente:
/// la cadena de conexión y el nombre de la vista no pueden quedar en un log de acceso.
/// </summary>
public sealed record LeerColumnasRequest(
    Guid ErpConexionId,
    string Origen);

// ── Parámetros generales ─────────────────────────────────────────────────────

public sealed record ParametroResponse(
    Guid Id,
    string Nombre,
    string? Descripcion,
    string? Valor,
    string TipoDato,
    string? Grupo,
    short Orden,
    bool EsSistema,
    bool EsSecreto);

/// <summary>Solicitud para actualizar un parámetro: valor y descripción.</summary>
public sealed record ActualizarParametroRequest(string Valor, string? Descripcion);

// ── Auditoría ────────────────────────────────────────────────────────────────

public sealed record RegistroAuditoriaResponse(
    long Id,
    string Entidad,
    Guid EntidadId,
    string Accion,
    string? Usuario,
    DateTime FechaUtc,
    string? CamposModificados,
    string? ValorAnteriorJson,
    string? ValorNuevoJson);
