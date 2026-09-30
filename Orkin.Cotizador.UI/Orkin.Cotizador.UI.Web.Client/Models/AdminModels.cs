namespace Orkin.Cotizador.UI.Web.Client.Models;

// ── Usuarios ─────────────────────────────────────────────────────────────────

public sealed class UsuarioModel
{
    public Guid Id { get; set; }
    public string NombreCuenta { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string? Documento { get; set; }
    public string? Cargo { get; set; }
    public bool EsActivo { get; set; } = true;
    public bool EsUsuarioSistema { get; set; }
    public bool DebeCambiarContrasena { get; set; }
    public DateTime? UltimoAccesoUtc { get; set; }
    public List<string> Roles { get; set; } = [];
}

// ── Roles y permisos ─────────────────────────────────────────────────────────

public sealed class RolModel
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; }
    public bool EsActivo { get; set; }
    public List<string> Permisos { get; set; } = [];
}

public sealed class PermisoModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

// ── Parámetros generales ─────────────────────────────────────────────────────

public sealed class ParametroModel
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Valor { get; set; }
    public string TipoDato { get; set; } = string.Empty;
    public string? Grupo { get; set; }
    public short Orden { get; set; }
    public bool EsSistema { get; set; }
    public bool EsSecreto { get; set; }
}

// ── Auditoría ────────────────────────────────────────────────────────────────

public sealed class RegistroAuditoriaModel
{
    public long Id { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public Guid EntidadId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? Usuario { get; set; }
    public DateTime FechaUtc { get; set; }
    public string? CamposModificados { get; set; }
    public string? ValorAnteriorJson { get; set; }
    public string? ValorNuevoJson { get; set; }
}

public sealed class PaginaAuditoria
{
    public List<RegistroAuditoriaModel> Items { get; set; } = [];
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int Tamanio { get; set; }
}
