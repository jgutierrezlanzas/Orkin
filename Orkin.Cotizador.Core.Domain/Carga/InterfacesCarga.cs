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
    /// </summary>
    public required IReadOnlyDictionary<string, string> MapeoColumnas { get; init; }
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

    public DateTime EjecutadoUtc { get; init; } = DateTime.UtcNow;

    public int Total => Insertados + Actualizados + Omitidos + Rechazados;
}

/// <summary>Una fila que no se pudo cargar, con la fila original y el motivo.</summary>
public sealed record RechazoCarga(
    int NumeroFila,
    string? CodigoExterno,
    string Motivo,
    string? FilaJson);
