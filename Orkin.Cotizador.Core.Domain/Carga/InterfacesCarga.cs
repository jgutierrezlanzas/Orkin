namespace Orkin.Cotizador.Core.Domain.Carga;

/// <summary>
/// Lee filas de un origen de datos externo. Toda fuente —Excel, SQL, API, CSV— implementa
/// esta interfaz y devuelve filas como diccionarios clave-valor, para que el proceso de carga
/// no sepa de dónde vienen los datos.
/// <para>Vive en el Domain y no en la Infrastructure porque el cargador de un catálogo —que es
/// lógica de aplicación— la recibe como parámetro sin importar quién la implemente. Es el
/// mismo trato de <c>ICotizadorCalculo</c>: la abstracción en el Domain, la implementación
/// donde toque.</para>
/// </summary>
public interface ILectorOrigen
{
    /// <summary>"EXCEL", "SQL_SERVER", "API", "CSV"…</summary>
    string TipoOrigen { get; }

    /// <summary>
    /// Lee todas las filas del origen. Cada fila es un diccionario donde la clave es el nombre
    /// de la columna tal como viene en el origen, y el valor es el dato crudo.
    /// </summary>
    Task<IReadOnlyList<Dictionary<string, object?>>> LeerAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Sabe cómo cargar UN catálogo: normaliza una fila cruda, busca la fila existente, decide
/// si es insert / update / omit, y escribe en la base. Cada catálogo implementa esta
/// interfaz; el orquestador no sabe cuál es cuál.
/// </summary>
public interface ICargadorCatalogo
{
    /// <summary>"PRODUCTOS", "CLIENTES", "UNIDADES_MEDIDA", "TIPOS_SERVICIO", "TIPOS_CAMBIO".</summary>
    string Catalogo { get; }

    /// <summary>
    /// Carga las filas leídas del origen aplicando el mapeo de columnas. Hace el upsert
    /// idempotente: inserta lo nuevo, actualiza lo que cambió, omite lo que no.
    /// </summary>
    Task<ResultadoCarga> CargarAsync(
        IReadOnlyList<Dictionary<string, object?>> filas,
        ConfiguracionCarga configuracion,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuración de una carga: qué catálogo se llena, desde dónde, y cómo se mapean las columnas
/// del origen a los campos del catálogo.
/// </summary>
public sealed record ConfiguracionCarga
{
    /// <summary>"PRODUCTOS", "CLIENTES", "UNIDADES_MEDIDA", "TIPOS_SERVICIO", "TIPOS_CAMBIO".</summary>
    public required string Catalogo { get; init; }

    /// <summary>"EXCEL" o "SQL_SERVER". Decide qué lector se usa.</summary>
    public required string TipoOrigen { get; init; }

    /// <summary>
    /// Mapeo columna del origen → campo del catálogo. Ej. <c>{ "Codigo Producto" = "Codigo" }</c>.
    /// Las columnas que no estén en el mapeo se ignoran.
    /// <para><b>null = la vista ya trae los nombres esperados</b>: la fila se toma tal cual
    /// viene, sin proyección. Es el contrato que la UI promete con el mapeo vacío, y por eso no
    /// puede ser una lista fija de columnas: una lista fija obligaría a que la vista de cada
    /// catálogo distinto trajera exactamente las mismas columnas que la del primero.</para>
    /// </summary>
    public IReadOnlyDictionary<string, string>? MapeoColumnas { get; init; }

    /// <summary>
    /// La conexión ERP de la que salieron los datos, si el origen es un flujo de integración.
    /// Se escribe en las filas nuevas (<c>ErpConexionId</c>) para distinguir el dato sincronizado
    /// del ERP del dato inicial de Excel o corregido a mano. <c>null</c> en cargas manuales.
    /// </summary>
    public Guid? ErpConexionId { get; init; }

    /// <summary>
    /// <c>true</c> = no escribir nada: correr la carga entera y devolver lo que
    /// <em>haría</em>, sin hacerlo.
    /// <para>El cargador decide insert / actualizar / omitir con la misma lógica en los dos
    /// modos; lo único que cambia es si aplica la decisión. Por eso una simulación no es una
    /// función aparte que se parece a la carga: es la carga con el último paso cortado, y por
    /// eso sirve para prever el resultado en vez de trabajarlo dos veces.</para>
    /// <para>⚠️ <b>El nombre importa.</b> Simular no es un "cargar en seco" que a veces
    /// escribe: es la garantía de que no se escribe. Por eso el proceso de carga, en este modo,
    /// no llama a <c>SaveChanges</c> y además verifica que el rastreador de cambios esté limpio
    /// al terminar. Si algún cargador futuro se olvida de la bandera, la simulación falla con un
    /// error que lo dice, en vez de dejar medio catálogo cargado en la base del cliente.</para>
    /// </summary>
    public bool EsSimulacion { get; init; }
}

/// <summary>
/// Resultado de una carga: qué se insertó, qué se actualizó, qué se omitió porque no cambió,
/// y qué se rechazó con su motivo.
/// </summary>
public sealed record ResultadoCarga
{
    public required string Catalogo { get; init; }
    public required string TipoOrigen { get; init; }
    public int Insertados { get; init; }
    public int Actualizados { get; init; }

    /// <summary>Filas cuyo HashContenido no cambió: no se escriben, no cuentan como error.</summary>
    public int Omitidos { get; init; }

    /// <summary>Filas que no se pudieron cargar. El detalle está en <see cref="Rechazos"/>.</summary>
    public int Rechazados { get; init; }

    public IReadOnlyList<RechazoCarga> Rechazos { get; init; } = [];

    /// <summary>
    /// Qué haría esta carga, fila por fila, con el detalle de los campos que tocaría. Solo se
    /// llena en simulación (<c>ConfiguracionCarga.EsSimulacion</c>); en una carga real va vacío,
    /// porque lo que la carga real hizo ya está en la base y en la bitácora.
    /// </summary>
    public IReadOnlyList<CambioSimulado> Cambios { get; init; } = [];

    public DateTime EjecutadoUtc { get; init; } = DateTime.UtcNow;

    public int Total => Insertados + Actualizados + Omitidos + Rechazados;

    /// <summary>
    /// ¿La carga tocó el catálogo? Insertar, actualizar o rechazar cuentan; omitir no.
    /// <para>Un rechazo cuenta como cambio aunque no escriba nada, porque una fila que el cargador
    /// no pudo aceptar es un dato que alguien tiene que mirar, aunque el resto de la carga haya
    /// ido bien. Omitir no cuenta: la fila estaba en el catálogo y sigue igual, y eso es
    /// exactamente el «no pasó nada» que la bitácora no necesita registrar.</para>
    /// <para>Existe como una sola definición porque dos lugares la usan —la política de registro de
    /// la bitácora y la que decide si el motor sigue insistiendo con el ERP— y si cada uno contara
    /// los campos por su cuenta acabarían discrepando justo en el caso límite.</para>
    /// </summary>
    public bool HayCambios => Insertados + Actualizados + Rechazados > 0;
}

/// <summary>Qué haría una fila en la base, sin tocarla.</summary>
public enum AccionSimulada
{
    /// <summary>La fila no existe: se crearía.</summary>
    Insertar,

    /// <summary>La fila existe y algunos campos cambiarían.</summary>
    Actualizar,

    /// <summary>La fila existe y el hash coincide: no se tocaría.</summary>
    Omitir,

    /// <summary>
    /// La fila ya existía por Excel o a mano, y el ERP trae ese mismo código: se le pondría la
    /// conexión y el origen del precio del ERP. Va aparte de <see cref="Actualizar"/> porque es
    /// el cambio que más sorprende: una fila que alguien escribió a mano pasa a ser de otra
    /// fuente, y eso no se ve en un contador.
    /// </summary>
    Adoptar
}

/// <summary>Un campo que una simulación cambiaría, con el valor de antes y el de después.</summary>
public sealed record CampoSimulado(string Campo, string? Anterior, string? Nuevo);

/// <summary>Lo que una simulación haría con una fila del origen.</summary>
public sealed record CambioSimulado(
    int NumeroFila,
    string? CodigoExterno,
    AccionSimulada Accion,
    IReadOnlyList<CampoSimulado> Campos);

/// <summary>Una fila que no se pudo cargar, con la fila original y el motivo.</summary>
public sealed record RechazoCarga(
    int NumeroFila,
    string? CodigoExterno,
    string Motivo,
    string? FilaJson);
