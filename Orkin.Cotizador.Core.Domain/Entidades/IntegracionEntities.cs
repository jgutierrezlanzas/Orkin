using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Catálogos de dominio del esquema <c>int</c> (§7.1 de `BASE-DE-DATOS.md`). En esta primera
/// tajada vertical solo se usan los valores que el código necesita; los catálogos como tablas
/// (y sus FK) se agregan en la fase que complete el anclaje ETL.
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

    /// <summary><c>int.EstadoFlujoCatalogo</c>: 1 = BORRADOR, 3 = ACTIVO, 5 = CON_ERROR.</summary>
    public const byte EstadoFlujoBorrador = 1;
    public const byte EstadoFlujoActivo = 3;
    public const byte EstadoFlujoConError = 5;

    /// <summary><c>int.TipoDisparadorCatalogo</c>: 1 = MANUAL (por ahora solo disparo manual).</summary>
    public const byte TipoDisparadorManual = 1;

    /// <summary><c>int.ModoExtraccionCatalogo</c>: 1 = COMPLETA. La incremental (2) es fase posterior.</summary>
    public const byte ModoExtraccionCompleta = 1;

    /// <summary><c>int.EstadoSincronizacionCatalogo</c>: 1 = EN_CURSO, 2 = CORRECTA, 4 = FALLIDA.</summary>
    public const byte EstadoSincronizacionEnCurso = 1;
    public const byte EstadoSincronizacionCorrecta = 2;
    public const byte EstadoSincronizacionFallida = 4;
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
/// Un flujo ETL (Fase 12, `BASE-DE-DATOS.md` §7.3): de dónde sales los datos y a qué catálogo
/// van. Es la unidad que el usuario crea, configura y dispara desde la pantalla de integraciones.
/// <para>En esta primera tajada, el <b>origen es una vista, una tabla o una consulta SQL</b>
/// (<see cref="Origen"/>) que el cliente configura y que debe entregar el formato que nuestros
/// cargadores esperan. La variante tabla/vista/consulta resuelve el punto de que el ERP puede
/// tener los datos repartidos en varias tablas: el contrato vive en el origen, no en el ERP.</para>
/// </summary>
public class ErpFlujo : BaseEntity
{
    public Guid ErpConexionId { get; set; }

    public ErpConexion? Conexion { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    /// <summary>Catálogo destino en nuestro cotizador: "PRODUCTOS", "CLIENTES", etc.</summary>
    public string Catalogo { get; set; } = string.Empty;

    /// <summary>Nombre de una vista o tabla, o una consulta SQL completa (el "origen").</summary>
    public string Origen { get; set; } = string.Empty;

    /// <summary>
    /// Mapeo columna del origen → campo del catálogo, en JSON. <c>null</c> = el origen ya entrega
    /// los nombres exactos que el cargador espera (se usa el mapeo por identidad).
    /// </summary>
    public string? MapeoJson { get; set; }

    /// <summary>FK → <c>int.TipoDisparadorCatalogo</c>. Hoy solo MANUAL.</summary>
    public byte TipoDisparador { get; set; } = IntegracionCatalogos.TipoDisparadorManual;

    /// <summary>Programado: cada cuántos segundos. <c>null</c> = sin programación (solo manual).</summary>
    public int? IntervaloSegundos { get; set; }

    /// <summary>FK → <c>int.ModoExtraccionCatalogo</c>. Hoy solo COMPLETA.</summary>
    public byte ModoExtraccion { get; set; } = IntegracionCatalogos.ModoExtraccionCompleta;

    /// <summary>FK → <c>int.EstadoFlujoCatalogo</c>.</summary>
    public byte Estado { get; set; } = IntegracionCatalogos.EstadoFlujoActivo;

    // ── Estado de la última corrida ──────────────────────────────────────────
    public DateTime? UltimaEjecucionUtc { get; set; }

    public byte? UltimoEstado { get; set; }

    public string? UltimoError { get; set; }

    public int UltimosInsertados { get; set; }

    public int UltimosActualizados { get; set; }

    public int UltimosOmitidos { get; set; }

    public int UltimosRechazados { get; set; }

    public short FallosConsecutivos { get; set; }
}

/// <summary>
/// Bitácora de cada corrida de un flujo (Fase 12, `BASE-DE-DATOS.md` §7.10). Es append-only:
/// cada ejecución —manual o programada— deja su fila con contadores y resultado.
/// </summary>
public class SincronizacionEjecucion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ErpConexionId { get; set; }

    public ErpConexion? Conexion { get; set; }

    public Guid ErpFlujoId { get; set; }

    public ErpFlujo? Flujo { get; set; }

    public byte TipoDisparador { get; set; } = IntegracionCatalogos.TipoDisparadorManual;

    /// <summary>FK → <c>int.EstadoSincronizacionCatalogo</c>.</summary>
    public byte Estado { get; set; } = IntegracionCatalogos.EstadoSincronizacionEnCurso;

    public DateTime InicioUtc { get; set; } = DateTime.UtcNow;

    public DateTime? FinUtc { get; set; }

    public int? DuracionMs { get; set; }

    public int RegistrosLeidos { get; set; }

    public int RegistrosCreados { get; set; }

    public int RegistrosActualizados { get; set; }

    public int RegistrosSinCambio { get; set; }

    public int RegistrosRechazados { get; set; }

    public int RegistrosDesactivados { get; set; }

    public string? MensajeError { get; set; }

    /// <summary>FK → <c>seg.Usuario</c>. <c>null</c> si la corrida fue automática.</summary>
    public Guid? SolicitadoPor { get; set; }
}