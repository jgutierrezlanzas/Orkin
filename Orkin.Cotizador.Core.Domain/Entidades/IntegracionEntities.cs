using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Catálogos de dominio del esquema <c>int</c> (§7.1 de `BASE-DE-DATOS.md`) que usa la conexión
/// al ERP del cliente.
/// </summary>
public static class IntegracionCatalogos
{
    /// <summary><c>int.TipoProveedorCatalogo</c>: 1 = SQL_SERVER.</summary>
    public const byte TipoProveedorSqlServer = 1;

    /// <summary><c>int.MetodoAutenticacionCatalogo</c> implícito: 1 = Básica (usuario/contraseña).</summary>
    public const byte MetodoAutenticacionBasica = 1;

    /// <summary><c>int.EstadoConexionCatalogo</c>: 1 = BORRADOR, 3 = ACTIVA, 5 = CON_ERROR.</summary>
    public const byte EstadoConexionBorrador = 1;
    public const byte EstadoConexionActiva = 3;
    public const byte EstadoConexionConError = 5;
}

/// <summary>
/// Anclaje de la integración (Fase 12, `BASE-DE-DATOS.md` §7.2): a qué ERP nos conectamos.
/// <para>El esquema completo define también URL, OAuth, lotes y configuraciones; esta primera
/// tajada vertical cubre conexiones SQL Server (TipoProveedor = <c>SQL_SERVER</c>) con
/// usuario/contraseña básica.</para>
/// <para>⚠️ <b>D-19 (solo lectura):</b> la columna <c>EsSoloLectura</c> lleva un
/// <c>CHECK (EsSoloLectura = 1)</c> en la base —nadie puede marcar una conexión como escribible,
/// ni por error. El usuario que se da de alta en el ERP del cliente es de solo lectura.</para>
/// </summary>
public class ErpConexion : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    /// <summary>FK → <c>int.TipoProveedorCatalogo</c>. Hoy solo <c>SQL_SERVER</c>.</summary>
    public byte TipoProveedor { get; set; } = IntegracionCatalogos.TipoProveedorSqlServer;

    public string Servidor { get; set; } = string.Empty;

    public int? Puerto { get; set; }

    public string NombreBaseDatos { get; set; } = string.Empty;

    public string? Esquema { get; set; } = "dbo";

    /// <summary>FK → catálogo de métodos de autenticación. Hoy solo básica (usuario/contraseña).</summary>
    public byte MetodoAutenticacion { get; set; } = IntegracionCatalogos.MetodoAutenticacionBasica;

    public string? Usuario { get; set; }

    /// <summary>Contraseña cifrada (ASP.NET Core Data Protection). Nunca en claro (D-26).</summary>
    public byte[]? ContrasenaCifrada { get; set; }

    public int TiempoEsperaSegundos { get; set; } = 30;

    public int LoteRegistros { get; set; } = 500;

    /// <summary>Siempre <c>true</c>: <c>CHECK (EsSoloLectura = 1)</c> en la base (D-19).</summary>
    public bool EsSoloLectura { get; set; } = true;

    public DateTime? UltimaConexionOkUtc { get; set; }

    public string? UltimoError { get; set; }

    public DateTime? UltimoErrorUtc { get; set; }

    /// <summary>Estado (BORRADOR/ACTIVA/CON_ERROR), reflejo de <c>int.EstadoConexionCatalogo</c>.</summary>
    public byte Estado { get; set; } = IntegracionCatalogos.EstadoConexionBorrador;
}

/// <summary>
/// Mapeo configurable entre la categoría de cliente que traiga el ERP (ej. <c>C01</c>, <c>C02</c>,
/// <c>C03</c> de Softland) y el <c>dbo.TipoClienteCatalogo</c> de la plataforma
/// (<c>RESIDENCIAL</c> / <c>EMPRESARIAL</c>). El usuario lo configura por conexión: cada cliente
/// puede tener su propia codificación.
/// </summary>
public class ErpMapeoTipoCliente : BaseEntity
{
    public Guid ErpConexionId { get; set; }

    public ErpConexion? ErpConexion { get; set; }

    /// <summary>Código de categoría que viene del ERP (ej. <c>C01</c>).</summary>
    public string CodigoCategoriaErp { get; set; } = null!;

    /// <summary>FK → <c>dbo.TipoClienteCatalogo</c>.</summary>
    public byte TipoClienteCatalogoId { get; set; }

    public TipoClienteCatalogo? TipoClienteCatalogo { get; set; }
}

// D-24: no hay tablas de flujos ni bitácora de sincronización. El ERP es la única fuente de los
// catálogos; acá solo queda la conexión y su mapeo de tipo de cliente.
