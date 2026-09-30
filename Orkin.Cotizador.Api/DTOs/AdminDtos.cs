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

/// <summary>Solicitud para fijar los permisos de un rol. La lista reemplaza la anterior completa.</summary>
public sealed record AsignarPermisosRolRequest(IReadOnlyList<string> Permisos);

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

public sealed record ActualizarParametroRequest(string Valor);

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
