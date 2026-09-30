namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>
/// Un campo que un cargador espera del origen, con lo que la pantalla de homologación
/// necesita para mostrar la tabla de mapeo.
/// </summary>
public sealed class CampoEsperadoModel
{
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public bool EsObligatorio { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

/// <summary>
/// Una columna que trae la vista del ERP. Es el otro lado de la homologación: los campos los
/// dice la plataforma, las columnas las dice el cliente.
/// </summary>
public sealed class ColumnaOrigenModel
{
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public bool PermiteNulos { get; set; }
    public int? Longitud { get; set; }

    /// <summary>Lo que se escribe en la caja de selección, con el tipo entre paréntesis.</summary>
    public string Texto => $"{Nombre} ({Tipo})";
}

/// <summary>
/// Una conexión al ERP de un cliente. No tiene <c>Contrasena</c> a propósito: la contraseña se
/// escribe una vez, al guardar, y nunca vuelve a salir del servidor. Por eso el formulario
/// distingue "sin contraseña" de "no la cambió" con un campo aparte, y no con una caja vacía que
/// parece lo mismo que borrar la contraseña.
/// </summary>
public sealed class ErpConexionModel
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Servidor { get; set; } = string.Empty;
    public int? Puerto { get; set; }
    public string NombreBaseDatos { get; set; } = string.Empty;
    public string? Esquema { get; set; }
    public string? Usuario { get; set; }

    /// <summary>Siempre <c>true</c>: la base del ERP del cliente se lee, nunca se escribe (D-19).</summary>
    public bool EsSoloLectura { get; set; } = true;

    public int TiempoEsperaSegundos { get; set; } = 30;

    /// <summary>
    /// Hay contraseña guardada. Con esto el formulario puede mostrar "dejar la que está" en vez
    /// de obligar a escribirla otra vez en cada edición de la descripción.
    /// </summary>
    public bool TieneContrasena { get; set; }

    public bool? UltimaPruebaOk { get; set; }
    public DateTime? UltimaConexionOkUtc { get; set; }
    public string? UltimoError { get; set; }
    public DateTime? UltimoErrorUtc { get; set; }
    public bool EsActivo { get; set; }
    public int TotalFlujos { get; set; }
}

/// <summary>Resultado de abrir la conexión y ejecutar un <c>SELECT 1</c>.</summary>
public sealed class ProbarConexionModel
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public string? VersionServidor { get; set; }
    public double? Milisegundos { get; set; }
}

/// <summary>
/// Un flujo ETL: qué catálogo llena y desde qué vista, tabla o consulta del ERP.
/// </summary>
public sealed class ErpFlujoModel
{
    public Guid Id { get; set; }
    public Guid ErpConexionId { get; set; }
    public string ConexionNombre { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Catálogo destino: <c>PRODUCTOS</c>, <c>ACTIVOS</c>, <c>CLIENTES</c>, <c>UNIDADES</c>, <c>TIPOS_SERVICIO</c>, <c>TIPOS_CAMBIO</c>.</summary>
    public string Catalogo { get; set; } = "PRODUCTOS";

    /// <summary>
    /// Vista, tabla o consulta SQL. Es el contrato con el ERP: si el dato del cliente está
    /// repartido en cinco tablas, la vista las une y el cargador no lo nota.
    /// </summary>
    public string Origen { get; set; } = string.Empty;

    /// <summary>
    /// Mapeo columna → campo, en JSON. Vacío significa "la vista ya trae los nombres que
    /// esperamos" y el mapeo es por identidad.
    /// </summary>
    public string? MapeoJson { get; set; }

    public bool EsActivo { get; set; }
    public DateTime? UltimaEjecucionUtc { get; set; }
    public int UltimosInsertados { get; set; }
    public int UltimosActualizados { get; set; }
    public int UltimosOmitidos { get; set; }
    public int UltimosRechazados { get; set; }
    public string? UltimoError { get; set; }

    // ── Programación ─────────────────────────────────────────────────────────
    public string TipoDisparador { get; set; } = "MANUAL";
    public int? IntervaloSegundos { get; set; }
    public short DesfaseSegundos { get; set; }
    public int? MaximoRegistrosPorCorrida { get; set; }
    public short MaximoFallosConsecutivos { get; set; }
    public DateTime? ProximaEjecucionUtc { get; set; }
    public DateTime? UltimoIntentoUtc { get; set; }
    public bool EsPausado { get; set; }
    public string? MotivoPausa { get; set; }
    public short FallosConsecutivos { get; set; }

    /// <summary>
    /// ¿Está el motor pendiente sobre este flujo? Programado, con ventana y sin pausar.
    /// <para>Es lo que decide si la pantalla muestra la cuenta regresiva o el botón de
    /// programar. No se deduce de <c>TipoDisparador</c> solo, porque un flujo pausado sigue
    /// siendo «programado» en su configuración: lo que dejó de correr fue el motor, y la
    /// pantalla tiene que poder distinguir las dos cosas.</para>
    /// </summary>
    public bool EstaProgramado =>
        TipoDisparador == "PROGRAMADO" && IntervaloSegundos is > 0 && !EsPausado;
}

/// <summary>Una corrida del flujo, de la bitácora.</summary>
public sealed class SincronizacionEjecucionModel
{
    public Guid Id { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime? FinUtc { get; set; }
    public int? DuracionMs { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int RegistrosLeidos { get; set; }
    public int RegistrosCreados { get; set; }
    public int RegistrosActualizados { get; set; }
    public int RegistrosSinCambio { get; set; }
    public int RegistrosRechazados { get; set; }
    public string? MensajeError { get; set; }
}

/// <summary>Lo que devuelve una ejecución: qué entró, qué no se movió y qué se rechazó.</summary>
public sealed class ResultadoCargaModel
{
    public string Catalogo { get; set; } = string.Empty;
    public string TipoOrigen { get; set; } = string.Empty;
    public int Insertados { get; set; }
    public int Actualizados { get; set; }
    public int Omitidos { get; set; }
    public int Rechazados { get; set; }
    public int Total => Insertados + Actualizados + Omitidos + Rechazados;
    public List<RechazoCargaModel> Rechazos { get; set; } = [];
}

public sealed class RechazoCargaModel
{
    public int NumeroFila { get; set; }
    public string? CodigoExterno { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>
/// Lo que un flujo haría si se ejecutara, sin ejecutarlo.
/// <para>Trae <see cref="Cambios"/> con el detalle siempre, no solo los que cambian: el filtro
/// del diálogo deja ver los omitidos, porque "estas 111 filas no se tocan" es información, no
/// relleno.</para>
/// </summary>
public sealed class SimulacionFlujoModel
{
    public string Catalogo { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Insertados { get; set; }
    public int Actualizados { get; set; }
    public int Omitidos { get; set; }
    public int Rechazados { get; set; }

    /// <summary>Hay al menos un campo que cambiaría. Distingue "no pasa nada" de "cambia el 30%".</summary>
    public bool HabriaCambios { get; set; }

    public List<CambioSimuladoModel> Cambios { get; set; } = [];
    public List<RechazoCargaModel> Rechazos { get; set; } = [];
    public DateTime SimuladoUtc { get; set; }

    /// <summary>Las filas que de verdad se tocarían, para el contador de la portada.</summary>
    public int AModificar => Cambios.Count(c => c.Accion is "Actualizar" or "Adoptar");
}

public sealed class CambioSimuladoModel
{
    public int NumeroFila { get; set; }
    public string? CodigoExterno { get; set; }

    /// <summary>"Insertar", "Actualizar", "Omitir" o "Adoptar". Texto, no número: llega del servidor así.</summary>
    public string Accion { get; set; } = string.Empty;

    public List<CampoSimuladoModel> Campos { get; set; } = [];
}

public sealed class CampoSimuladoModel
{
    public string Campo { get; set; } = string.Empty;
    public string? Anterior { get; set; }
    public string? Nuevo { get; set; }
}
