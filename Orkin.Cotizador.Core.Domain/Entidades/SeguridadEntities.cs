using Microsoft.AspNetCore.Identity;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Usuario del sistema. Es el <c>IdentityUser&lt;Guid&gt;</c> de ASP.NET Core Identity, pero
/// <b>sobre nuestras propias tablas</b> del esquema <c>seg</c> (D-02: seguridad propia, sin
/// <c>MerakodeSoft.Auth</c> ni ninguna plataforma externa).
/// <para>El <c>Id</c> es <c>uniqueidentifier</c>, no <c>nvarchar</c>, y es el mismo
/// <c>uniqueidentifier</c> que aparece en <c>CreatedBy</c> / <c>UpdatedBy</c> de todo el resto del
/// modelo. Por eso no hace falta una tabla de perfil aparte.</para>
/// </summary>
public class Usuario : IdentityUser<Guid>
{
    /// <summary>Se muestra en el topbar. La UI no muestra nunca el <c>UserName</c> solo.</summary>
    public string NombreCompleto { get; set; } = null!;

    public string? Documento { get; set; }

    public string? Cargo { get; set; }

    public string? AvatarUrl { get; set; }

    /// <summary>Baja lógica. Un usuario con historial de cotizaciones <b>no</b> se borra.</summary>
    public bool EsActivo { get; set; } = true;

    /// <summary>
    /// <c>true</c> = no se puede desactivar ni eliminar. Es lo que impide quedarse sin
    /// administrador. Nunca son más de uno.
    /// </summary>
    public bool EsUsuarioSistema { get; set; }

    /// <summary>Obliga a cambiar la clave en el próximo ingreso.</summary>
    public bool DebeCambiarContrasena { get; set; }

    public DateTime? UltimoAccesoUtc { get; set; }

    /// <summary><c>nvarchar(45)</c>: soporta IPv6.</summary>
    public string? UltimoAccesoIp { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Roles NO se declara aca: la trae IdentityUser<TKey> como ICollection<IdentityUserRole<TKey>>,
    // y el tipo tiene que ser ese. Redeclararla con una clase propia compilaria, pero UserStore
    // buscaria Set<IdentityUserRole<Guid>>() y no lo encontraria -el modelo empareja por el tipo
    // de C# exacto, no por el de la tabla-, y el ingreso fallaria al armar el principal.

    public ICollection<UsuarioPermiso> Permisos { get; set; } = [];
}

/// <summary>Rol. Los nombres exactos están en la matriz de roles del README.</summary>
public class Rol : IdentityRole<Guid>
{
    public string? Description { get; set; }

    /// <summary>No se puede eliminar.</summary>
    public bool EsSistema { get; set; }

    public bool EsActivo { get; set; } = true;

    /// <summary>FK → <c>dbo.NivelAccesoCatalogo</c> (§5.17). Oculta menús.</summary>
    public byte NivelAccesoId { get; set; }

    public NivelAccesoCatalogo? NivelAcceso { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public ICollection<RolPermiso> Permisos { get; set; } = [];
}

/// <summary>
/// Permiso. El código es el <b>nombre de la política</b> de autorización, así que se escribe como
/// <c>MODULO.Accion</c>: <c>COTIZACIONES.CREAR</c>, <c>ADMIN.USUARIOS.GESTIONAR</c>.
/// <para>El módulo es FK a un catálogo y la acción es texto libre: los módulos son un conjunto
/// cerrado y las acciones crecen sin que haya que migrar.</para>
/// </summary>
public class Permiso : BaseEntity
{
    /// <summary>Código completo del permiso, tal como lo evalúa la política de autorización.</summary>
    public string Codigo { get; set; } = null!;

    /// <summary>Etiqueta para la UI.</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>FK → <c>dbo.ModuloPermisoCatalogo</c> (§5.16).</summary>
    public byte ModuloId { get; set; }

    public ModuloPermisoCatalogo? Modulo { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>Orden en la pantalla de roles.</summary>
    public short Orden { get; set; }

    public ICollection<RolPermiso> Roles { get; set; } = [];

    public ICollection<UsuarioPermiso> Usuarios { get; set; } = [];
}

/// <summary>Permisos de un rol.</summary>
public class RolPermiso
{
    public Guid RolId { get; set; }

    public Rol Rol { get; set; } = null!;

    public Guid PermisoId { get; set; }

    public Permiso Permiso { get; set; } = null!;

    public DateTime AsignadoUtc { get; set; }

    public Guid? AsignadoPor { get; set; }
}

// No existe una clase UsuarioRol. La tabla seg.UsuarioRol la mapea el IdentityUserRole<Guid> de
// Identity, sin envoltura propia, y por eso no lleva AsignadoUtc ni AsignadoPor. Esa informacion
// no se pierde: vive en aud.RegistroAuditoria, igual que para cualquier otra asignacion. Ponerla
// tambien en la tabla puente seria una segunda copia, mas debil, de lo que la bitacora ya
// responde bien.

/// <summary>
/// Permisos <b>directos</b> de un usuario, por encima de los de su rol.
/// <para><see cref="Concedido"/> = <c>false</c> es una <b>denegación</b> y <b>manda sobre
/// cualquier permiso de rol</b>. Es el mecanismo para decirle a un vendedor "no puedes ver las
/// cotizaciones de la sucursal norte" sin crear un rol nuevo.</para>
/// </summary>
public class UsuarioPermiso : BaseEntity
{
    public Guid UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public Guid PermisoId { get; set; }

    public Permiso Permiso { get; set; } = null!;

    /// <summary><c>true</c> = concedido; <c>false</c> = denegado (manda sobre el rol).</summary>
    public bool Concedido { get; set; } = true;

    public string? Motivo { get; set; }

    /// <summary>Permiso temporal. <c>null</c> = indefinido.</summary>
    public DateTime? VigenteHasta { get; set; }
}
