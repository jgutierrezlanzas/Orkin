using System.Text.Json;

namespace Orkin.Cotizador.Core.Infrastructure.Persistence.Seed;

/// <summary>
/// Un archivo de <c>scripts/seed/</c>. El formato es el mismo para <b>todas</b> las tablas, y por eso
/// el aplicador no necesita un <c>switch</c> por tabla: la estrategia se deduce del modelo de EF.
/// <para><b>El nombre del archivo es <c>{esquema}.{entidad}.json</c></b>, exactamente como se llama
/// la tabla. Un archivo que no exista en el modelo es un error de arranque, y un archivo cuyo
/// esquema no coincida con el de la entidad también: las dos cosas son casi siempre una falta de
/// Ortografía, y una falta de Ortografía en el seed se descubre cuando a un cliente no le aparece
/// una opción.</para>
/// </summary>
public sealed class ArchivoSemilla
{
    /// <summary>Ruta absoluta, solo para los mensajes de error.</summary>
    public string Ruta { get; init; } = null!;

    /// <summary>Esquema declarado en el nombre del archivo. <c>dbo</c>, <c>seg</c>…</summary>
    public string Esquema { get; init; } = null!;

    /// <summary>Nombre de la entidad, que debe coincidir con el <c>DbSet</c> del contexto.</summary>
    public string Entidad { get; init; } = null!;

    /// <summary>
    /// Propiedad que identifica la fila de forma natural y contra la que se decide si ya existe.
    /// <c>Codigo</c> en catálogos y permisos, <c>Name</c> en roles, <c>Nombre</c> en parámetros.
    /// <para>Se declara en el archivo y no se deduce en el código, porque la clave natural es una
    /// decisión de la tabla, no del mecanismo de siembra.</para>
    /// </summary>
    public string ClaveNatural { get; init; } = "Codigo";

    /// <summary>Las filas, tal como vinieron. Se mantienen en crudo y se convierten al aplicar.</summary>
    public IReadOnlyList<FilaSemilla> Filas { get; init; } = [];
}

/// <summary>
/// Una fila del seed. Todas las claves del JSON se asignan a propiedades de la entidad por nombre
/// (sin distinguir mayúsculas), así que el archivo no tiene que distinguir "columnas conocidas" de
/// "columnas extra".
/// <para><b>Las claves foráneas se escriben con el <c>Codigo</c> del catálogo, nunca con su
/// número.</b> Un seed con <c>"MetodoCosteoId": 2</c> se rompe en cuanto alguien inserta una fila
/// en el catálogo; con <c>"MetodoCosteoId": "SUMA_SIMPLE"</c> no.</para>
/// </summary>
public sealed class FilaSemilla
{
    /// <summary>Las columnas de la fila, con el nombre tal como lo entiende la entidad.</summary>
    public Dictionary<string, JsonElement> Valores { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Códigos de <c>seg.Permiso</c> que se asignan a este rol. Vive fuera de
    /// <see cref="Valores"/> porque no es una columna: es una relación de la tabla puente
    /// <c>seg.RolPermiso</c>, y esconderla en el diccionario haría que un error de tipeo se
    /// manifestara como "propiedad desconocida".
    /// <para>Solo tiene sentido en un archivo de <c>seg.Rol</c>.</para>
    /// </summary>
    public IReadOnlyList<string> Permisos { get; init; } = [];
}

/// <summary>Lo que hizo la siembra de una tabla. Es lo que se escribe en el log.</summary>
public sealed class ResultadoSemilla
{
    public required string Tabla { get; init; }

    public required int Insertados { get; init; }

    public required int Omitidos { get; init; }

    /// <summary>
    /// Filas que ya existían con <c>EsSembrado = 0</c>: las creó o editó un usuario, y el seed no
    /// tiene derecho a reclamarlas. No es un error, pero sí hay que verlo.
    /// </summary>
    public IReadOnlyList<string> Conflictos { get; init; } = [];

    public int Total => Insertados + Omitidos + Conflictos.Count;
}

/// <summary>
/// Falla de la siembra. Es <b>sempre</b> un problema de desarrollo o de despliegue, nunca algo que
/// deba resolverse en caliente: por eso aborta el arranque en vez de registrar un aviso.
/// </summary>
public sealed class SemillaInvalidaException : Exception
{
    public SemillaInvalidaException(string mensaje) : base(mensaje)
    {
    }

    public SemillaInvalidaException(string mensaje, Exception interna) : base(mensaje, interna)
    {
    }
}
