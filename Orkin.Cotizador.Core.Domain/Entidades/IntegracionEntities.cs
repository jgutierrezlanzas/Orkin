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

    /// <summary><c>int.TipoDisparadorCatalogo</c>: 1 = MANUAL.</summary>
    public const byte TipoDisparadorManual = 1;

    /// <summary>
    /// <c>int.TipoDisparadorCatalogo</c>: 3 = PROGRAMADO. El motor corre el flujo cada
    /// <see cref="ErpFlujo.IntervaloSegundos"/> segundos por su cuenta.
    /// <para>El 3 y no el 2 es el mismo criterio del resto de catálogos de <c>int</c>
    /// (BORRADOR=1, activo=3, CON_ERROR=5): deja el 2 libre para el valor que se agregue después,
    /// sin tener que renumerar filas ya sembradas.</para>
    /// </summary>
    public const byte TipoDisparadorProgramado = 3;

    /// <summary>
    /// <c>int.TipoDisparadorCatalogo</c>: 5 = EVENTO. El flujo se despierta cuando el ERP avisa por
    /// webhook (12.12). Sigue teniendo sondeo de respaldo, porque un evento que no llega no
    /// distingue de un ERP que no notifica.
    /// </summary>
    public const byte TipoDisparadorEvento = 5;

    /// <summary>
    /// Fallos consecutivos a partir de los cuales el flujo se pausa solo. Está en el código y no en
    /// <c>int</c> como catálogo porque no es un dato de negocio que alguien edite desde
    /// Administración: es la política de seguridad del motor contra su propio error de bucle.
    /// <para>Sin este corte, un ERP caído produce un bucle infinito de intentos fallidos contra la
    /// base de un cliente, a razón de uno cada 10 segundos, para siempre.</para>
    /// </summary>
    public const short MaximoFallosConsecutivosPorDefecto = 5;

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

    /// <summary>FK → <c>int.TipoDisparadorCatalogo</c>: MANUAL, PROGRAMADO o EVENTO.</summary>
    public byte TipoDisparador { get; set; } = IntegracionCatalogos.TipoDisparadorManual;

    /// <summary>
    /// Cada cuántos segundos corre el flujo cuando es <c>PROGRAMADO</c> o <c>EVENTO</c>.
    /// <c>null</c> = solo manual.
    /// <para>Es configurable y no un constante porque es la latencia prometida a quien mira el
    /// precio, y esa latencia no es la misma para todos los catálogos: el tipo de cambio se mueve
    /// una vez al día y el precio de un producto, cada hora. Poner un único intervalo fijo
    /// obligaría a gastar en el cliente un presupuesto de consultas que no necesita.</para>
    /// <para>El piso de 10 segundos lo impone la base (<c>CK_ErpFlujo_IntervaloMinimo</c>), no
    /// este código: un flujo guardado con 2 segundos tiene que ser imposible, no improbable.</para>
    /// </summary>
    public int? IntervaloSegundos { get; set; }

    /// <summary>
    /// Corrimiento aleatorio de 0 a 30 segundos sobre <see cref="IntervaloSegundos"/>.
    /// <para>Existe porque todos los flujos programados se crean el mismo día y, sin este
    /// desfase, todos golpean el ERP del cliente en el mismo instante. A las 12:00:00 exactas
    /// llegarían a la vez las consultas de los ocho clientes, y ese pico no lo generó ninguno de
    /// los usuarios de esos ERP: lo generó nuestra configuración.</para>
    /// <para>Se asigna una vez, al crear el flujo, y no se recalcula: cambiarlo en cada arranque
    /// movería el pico de lugar sin querer, que es justo lo que se quiere evitar.</para>
    /// </summary>
    public short DesfaseSegundos { get; set; }

    /// <summary>
    /// Tope de filas por corrida; <c>null</c> = sin tope.
    /// <para>Es la válvula para cuando el cliente pide frecuencia alta sobre un ERP lento: se
    /// sincronizan las primeras N filas y el resto espera a la corrida siguiente, en vez de que
    /// una lectura de 40.000 filas tarde un minuto y se solape con la que ya arrancó.</para>
    /// </summary>
    public int? MaximoRegistrosPorCorrida { get; set; }

    /// <summary>
    /// Cuántos fallos seguidos se toleran antes de que el motor pause el flujo.
    /// Por defecto <see cref="IntegracionCatalogos.MaximoFallosConsecutivosPorDefecto"/>.
    /// </summary>
    public short MaximoFallosConsecutivos { get; set; }
        = IntegracionCatalogos.MaximoFallosConsecutivosPorDefecto;

    /// <summary>
    /// Cuándo le toca correr a este flujo. <c>null</c> = nunca: solo manual, o pausado.
    /// <para>El motor no calcula la próxima ejecución en cada vuelta: la lee de aquí, y es el
    /// reclamo atómico el que la avanza. Esa es la diferencia entre un planificador que decide y
    /// uno que choca con otra instancia al decidir.</para>
    /// </summary>
    public DateTime? ProximaEjecucionUtc { get; set; }

    /// <summary>
    /// Último intento, haya funcionado o no. Es el latido del flujo.
    /// <para>Va aparte de <see cref="UltimaEjecucionUtc"/>, que solo avanza con corridas que
    /// dejan rastro en la bitácora. Un flujo que corre cada 10 s y no cambia nada deja
    /// <see cref="UltimaEjecucionUtc"/> quieto durante horas, y sin este campo la pantalla
    /// diría "nunca se ha corrido" de un flujo que no ha parado de correr.</para>
    /// </summary>
    public DateTime? UltimoIntentoUtc { get; set; }

    /// <summary>
    /// Desde cuándo hay una corrida en curso sobre este flujo. Es el alquiler que impide que dos
    ///pahos de ejecución se pisen: el que lo toma lo escribe antes de leer el ERP y lo borra al
    /// terminar, y tanto el reclamo del motor como la corrida a mano se niegan a arrancar si
    /// encuentra la marca puesta.
    /// <para><b>Por qué hace falta y no basta con <see cref="ProximaEjecucionUtc"/>:</b> la ventana
    /// solo dice <i>cuándo le toca</i>, no <i>quién lo está haciendo</i>. Un flujo programado
    /// cuya ventana aún no vence puede estar corriendo justo ahora, y si la corrida a mano no
    /// respeta esa diferencia, un «Ejecutar» pulsado en el segundo justo de una corrida
    /// programada carga el mismo catálogo dos veces a la vez, contra el mismo ERP, escribiendo
    /// sobre las mismas filas.</para>
    /// <para>Se toma siempre con su caducidad
    /// (<see cref="PlanificadorSincronizacion.ToleranciaCorridaEnCurso"/>): un proceso que se muere
    /// a media carga no puede dejar el flujo marcado para siempre, y un flujo que no vuelve nunca a
    /// correr es un síntoma que no lleva a ninguna parte.</para>
    /// </summary>
    public DateTime? CorridaEnCursoUtc { get; set; }

    /// <summary>
    /// El motor lo pausó tras <see cref="MaximoFallosConsecutivos"/> fallos seguidos. Mientras
    /// está en <c>true</c> el flujo no se programa ni se toca, hasta que alguien lo reanude.
    /// <para>Es un estado propio y no un <c>Estado = CON_ERROR</c> porque significan cosas
    /// distintas: <c>CON_ERROR</c> es «la última corrida falló» y se recupera sola en la
    /// siguiente; pausado es «dejemos de golpear el ERP de este cliente hasta que alguien lo
    /// mire».</para>
    /// </summary>
    public bool EsPausado { get; set; }

    /// <summary>Por qué se pausó. Se muestra en la UI: una pausa que nadie puede leer no existe.</summary>
    public string? MotivoPausa { get; set; }

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
/// Bitácora de las corridas de un flujo (Fase 12, `BASE-DE-DATOS.md` §7.10). Es append-only: una
/// fila por corrida, con contadores y resultado.
/// <para><b>Qué corridas dejan fila no es lo mismo para todas</b>, y es una decisión, no un
/// descuido. La corrida manual siempre deja. La programada deja solo si insertó, actualizó,
/// desactivó o rechazó algo, o si falló. El motivo es aritmético: un flujo con intervalo de
/// 30 s son 2.880 corridas diarias, y anotarlas todas convertiría la bitácora —que existe para
/// responder «¿qué le pasó a este catálogo?»— en un ruido de 2.880 filas diarias que nadie va
/// a leer y que terminaría empujando a un truncado. Lo que la programada no hace es
/// «nada happenido», y eso ya está en otro sitio: <c>ErpFlujo.UltimoIntentoUtc</c> es el latido
/// del flujo, y avanza aunque la corrida no se anote.</para>
/// <para>El fallo <b>sí</b> se anota siempre, y esa es la razón de fondo de la tabla: una
/// sincronización que falló y no se registró es indistinguible de una que nunca corrió.</para>
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