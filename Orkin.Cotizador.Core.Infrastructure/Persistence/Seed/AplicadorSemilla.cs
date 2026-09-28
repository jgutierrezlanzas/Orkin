using System.Globalization;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Core.Infrastructure.Persistence.Seed;

/// <summary>
/// Aplica los archivos de <c>scripts/seed/</c> a la base con la idempotencia de §5.20:
/// <list type="bullet">
///   <item><description>La fila no existe → se inserta.</description></item>
///   <item><description>Existe con <c>EsSembrado = 1</c> → no se toca. Ni siquiera si el JSON
///   cambió: el seed es el piso, y corregir un catálogo a mano es una decisión que el JSON no
///   puede pisar.</description></item>
///   <item><description>Existe con <c>EsSembrado = 0</c> → no se toca y se reporta como
///   conflicto. La fila la creó o editó una persona, y el seed no tiene derecho a reclamarla.</description></item>
/// </list>
/// <para><b>Por qué las claves foráneas se escriben con el <c>Codigo</c> del catálogo y no con su
/// número:</b> el <c>tinyint</c> de un <c>IDENTITY</c> depende del orden en que se insertaron las
/// filas. Un seed con <c>"MetodoCosteoId": 2</c> funciona en la máquina de quien lo escribió y
/// apunta al método equivocado en la de otro, después de que alguien insertó un valor nuevo. Con
/// <c>"MetodoCosteoId": "SUMA_SIMPLE"</c> no hay nada que pueda quedar desalineado.</para>
/// <para><b>Por qué no hay un <c>switch</c> por tabla:</b> la estrategia de escritura se deduce del
/// modelo de EF —qué propiedad es la clave natural, cuáles son claves foráneas y hacia qué
/// principal—, así que agregar un catálogo es agregar un archivo, no tocar código. Un
/// <c>switch</c> por tabla sería una lista de catálogos compilada, que es exactamente lo que ADR-018
/// elimina.</para>
/// </summary>
public sealed class AplicadorSemilla
{
    private readonly CotizadorDbContext _context;
    private readonly EjecutorSql _consultor;
    private readonly ILogger<AplicadorSemilla> _logger;

    /// <summary>
    /// Caché de <c>Codigo → Id</c> de las tablas principales, para no repetir una lectura por cada
    /// clave foránea de cada fila. Vive solo durante la corrida de la siembra: la aplicación normal
    /// resuelve catálogos con <c>ICatalogoResolver</c>.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, object>> _cacheClaves =
        new(StringComparer.OrdinalIgnoreCase);

    public AplicadorSemilla(
        CotizadorDbContext context,
        EjecutorSql consultor,
        ILogger<AplicadorSemilla> logger)
    {
        _context = context;
        _consultor = consultor;
        _logger = logger;
    }

    /// <summary>
    /// Aplica los archivos en el orden en que se reciben. <b>Ese orden importa</b> y no es
    /// arbitrario: SQL Server valida las claves foráneas sentencia por sentencia, así que una fila
    /// que apunta a otra tabla tiene que insertarse después de que esa exista. Quien arma el orden
    /// es <see cref="OrdenarPorDependencias"/>.
    /// <para>Es <b>una sola transacción</b> para todos los archivos, no una por tabla. Con una por
    /// tabla, un error en el archivo número veinte dejaría los diecinueve anteriores escritos y el
    /// proceso muere; nadie vuelve a levantar la aplicación en un estado a medias sin saber
    /// exactamente qué quedó.</para>
    /// </summary>
    public IReadOnlyList<ResultadoSemilla> Aplicar(IReadOnlyList<ArchivoSemilla> archivos)
    {
        var resultados = new List<ResultadoSemilla>(archivos.Count);

        foreach (var archivo in archivos)
        {
            resultados.Add(AplicarArchivo(archivo));
        }

        return resultados;
    }

    /// <summary>
    /// El techo de filas de un catálogo con <c>Id tinyint</c>, y la razón de que exista.
    /// <para>EF no puede asignar una clave a una entidad que todavía no está en la base, así que le
    /// pone un valor temporal para poder seguirla en el <c>ChangeTracker</c> y lo cambia por el
    /// real cuando la base contesta. En una columna <c>tinyint</c> esos valores temporales van de
    /// 255 para abajo, así que la columna se queda sin margen apenas una fila los pisa. Con 255
    /// filas reales, el 255 es real y el temporal choca con él.</para>
    /// <para>Por eso el techo es 254 y no 255: una fila menos de margen para que un catálogo que
    /// llega justo al límite no falle en la primera siembra y sí en la segunda, que es la más
    /// difícil de reproducir.</para>
    /// </summary>
    public const int TechoFilasPorCatalogoTinyint = 254;

    /// <summary>
    /// Corta la siembra con un mensaje que dice qué hacer, en vez de dejar que reviente un
    /// <c>SaveChanges</c> con un error de clave duplicada que no menciona ni la tabla ni el
    /// archivo.
    /// <para>Solo mira catálogos de <c>tinyint</c>. Las tablas con clave <c>Guid</c> o
    /// <c>bigint</c> no tienen este techo, y no hay que inventarles uno.</para>
    /// </summary>
    private static void VerificarTechoDeFilas(IReadOnlyEntityType entidad, ArchivoSemilla archivo)
    {
        var tipoClave = entidad.FindPrimaryKey()?.Properties[0].ClrType;

        if (tipoClave != typeof(byte) || archivo.Filas.Count <= TechoFilasPorCatalogoTinyint)
        {
            return;
        }

        throw new SemillaInvalidaException(
            $"{archivo.Ruta}: {archivo.Filas.Count} filas para '{archivo.Entidad}', que tiene clave " +
            $"tinyint y admite {TechoFilasPorCatalogoTinyint} (ADR-018, §5.0). EF usa los valores " +
            "255 hacia abajo como clave temporal mientras la base no devuelve la real, así que un " +
            "catálogo más grande que eso termina con una clave repetida. La salida es pasar la " +
            "columna Id a int y con ella todas las claves foráneas que la apunten, o partir el " +
            "catálogo en dos.");
    }

    private ResultadoSemilla AplicarArchivo(ArchivoSemilla archivo)
    {
        var entidad = BuscarEntidad(archivo.Entidad, archivo.Ruta);

        if (entidad.FindProperty(archivo.ClaveNatural) is null)
        {
            throw new SemillaInvalidaException(
                $"{archivo.Ruta}: '{archivo.Entidad}' no tiene la propiedad " +
                $"'{archivo.ClaveNatural}', que el archivo declara como clave natural.");
        }

        VerificarTechoDeFilas(entidad, archivo);

        var existentes = LeerExistentes(entidad, archivo);

        var insertados = 0;
        var omitidos = 0;
        var conflictos = new List<string>();

        foreach (var fila in archivo.Filas)
        {
            var clave = fila.Valores[archivo.ClaveNatural].GetString()!;

            if (existentes.TryGetValue(clave, out var sembrada))
            {
                if (sembrada)
                {
                    omitidos++;
                }
                else
                {
                    conflictos.Add($"{archivo.Esquema}.{archivo.Entidad}[{clave}]");
                }

                continue;
            }

            var nuevo = Construir(entidad, fila, archivo);

            // Entry(object) en vez de Add: es la única vía de EF Core 10 para dar de alta una
            // entidad cuyo tipo no se conoce en tiempo de compilación, y hace exactamente lo mismo
            // que Add sin la diferencia de que Add ya no existe para este caso.
            _context.Entry(nuevo).State = EntityState.Added;

            insertados++;
        }

        if (insertados > 0)
        {
            // Un solo SaveChanges por archivo, no uno por fila: los parámetros vanUNA vez y la
            // sentencia es una. En un catálogo de veinte filas, veinte SaveChanges serían veinte
            // viajes de ida y vuelta a la base.
            _context.SaveChanges();
        }

        if (conflictos.Count > 0)
        {
            // No es un error: alguien corrigió o creó esas filas a mano y el seed las respetó. Se
            // reporta porque si aparece un conflicto donde nadie editó, alguien está escribiendo en
            // la base fuera de la aplicación, y eso hay que saberlo.
            _logger.LogWarning(
                "{Esquema}.{Entidad}: {Cantidad} fila(s) ya existían con EsSembrado = 0 y no se " +
                "tocaron. Si nadie las editó a mano, revise quién las está escribiendo: {Filas}.",
                archivo.Esquema,
                archivo.Entidad,
                conflictos.Count,
                string.Join(", ", conflictos));
        }

        _logger.LogInformation(
            "{Esquema}.{Entidad}: {Insertados} insertados, {Omitidos} omitidos (ya sembrados), " +
            "{Conflictos} conflictos.",
            archivo.Esquema,
            archivo.Entidad,
            insertados,
            omitidos,
            conflictos.Count);

        return new ResultadoSemilla
        {
            Tabla = $"{archivo.Esquema}.{archivo.Entidad}",
            Insertados = insertados,
            Omitidos = omitidos,
            Conflictos = conflictos
        };
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Orden por dependencias
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ordena los archivos para que ninguna tabla se inserte antes que algo de lo que depende.
    /// <para>Es un orden topológico con detección de ciclos: parte de los archivos sin dependencias
    /// y va tomando, en cada pasada, los que ya tienen todo lo que necesitan resuelto. Si en una
    /// pasada completa no avanza, hay un ciclo —por ejemplo A apunta a B y B a A— y eso se reporta
    /// por nombre, en vez de dejar que la base falle con un error de clave foránea en la mitad de la
    /// transacción.</para>
    /// </summary>
    public static IReadOnlyList<ArchivoSemilla> OrdenarPorDependencias(
        IReadOnlyList<ArchivoSemilla> archivos,
        IReadOnlyModel modelo)
    {
        var porEntidad = archivos.ToDictionary(a => a.Entidad, StringComparer.OrdinalIgnoreCase);

        // dependencias[tabla] = tablas de las que depende
        var dependencias = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var archivo in archivos)
        {
            var entidad = BuscarEntidad(modelo, archivo.Entidad, archivo.Ruta);
            var necesarias = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fk in entidad.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType.ClrType.Name;

                // Una autorreferencia no es un problema: dentro de la misma tabla el orden no
                // importa, porque todas las filas se insertan en la misma sentencia.
                if (principal != archivo.Entidad && porEntidad.ContainsKey(principal))
                {
                    necesarias.Add(principal);
                }
            }

            dependencias[archivo.Entidad] = necesarias;
        }

        var pendientes = new HashSet<string>(porEntidad.Keys, StringComparer.OrdinalIgnoreCase);
        var orden = new List<ArchivoSemilla>(archivos.Count);

        while (pendientes.Count > 0)
        {
            var listos = pendientes
                .Where(e => !dependencias[e].Any(d => pendientes.Contains(d)))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (listos.Length == 0)
            {
                throw new SemillaInvalidaException(
                    "Las tablas de la semilla dependen unas de otras en círculo: " +
                    string.Join(", ", pendientes.Order(StringComparer.OrdinalIgnoreCase)) +
                    ". No hay orden posible; revise las claves foráneas de esos archivos.");
            }

            foreach (var listo in listos)
            {
                orden.Add(porEntidad[listo]);
                pendientes.Remove(listo);
            }
        }

        return orden;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Construcción de entidades
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Localiza la entidad a la que escribe un archivo de semilla.
    /// <para><b>Por qué busca por <see cref="IReadOnlyEntityType.ClrType"/> y no por
    /// <c>FindEntityType(nombre)</c>:</b> el nombre del archivo ya se partió en
    /// <c>{esquema}.{entidad}.json</c>, y la parte de la entidad es el nombre corto del tipo de C#.
    /// Buscarlo así y no por el nombre de entidad del modelo hace que la regla sea la misma en las
    /// dos etapas de la siembra —la que valida y la que escribe—, y deja de depender de cómo resuelve
    /// EF los nombres de entidad. Cuando las dos etapas usan reglas distintas, la validación pasa y
    /// la escritura falla, que es la peor forma de descubrir que el problema era de emparejamiento.</para>
    /// <para><b>Por qué el mensaje lista los nombres disponibles:</b> el nombre del archivo es la
    /// parte del contrato que alguien escribe a mano, así que el error útil es "estas son las
    /// opciones", no "no encontré nada".</para>
    /// </summary>
    private IReadOnlyEntityType BuscarEntidad(string nombre, string ruta)
        => BuscarEntidad(_context.Model, nombre, ruta);

    /// <summary>
    /// La regla única de emparejamiento entre el nombre del archivo y la entidad del modelo.
    /// <para><b>Por qué no <c>FindEntityType(nombre)</c>:</b> se comprobó contra este modelo y
    /// devuelve <c>null</c> para estos tipos, mientras que la búsqueda por
    /// <see cref="IReadOnlyEntityType.ClrType"/> los encuentra. Ninguna de las dos etapas de la
    /// siembra puede depender de una de esas dos reglas: si la que ordena y la que escribe usan
    /// distintas, la validación pasa, el orden sale alfabético y el fallo aparece mucho después,
    /// como un "código no existe" en una tabla que sí se sembró. Con una sola función, esa
    /// discrepancia no se puede expresar.</para>
    /// <para>Es el mismo criterio que usa <see cref="LectorSemilla"/>, que además valida el
    /// esquema. Que los tres puntos de la siembra emparejen igual es lo que permite que el orden
    /// topológico y la resolución de claves hablen del mismo modelo.</para>
    /// <para><b>Por qué el mensaje lista los nombres disponibles:</b> el nombre del archivo es la
    /// parte del contrato que alguien escribe a mano, así que el error útil es "estas son las
    /// opciones", no "no encontré nada".</para>
    /// </summary>
    private static IReadOnlyEntityType BuscarEntidad(
        IReadOnlyModel modelo,
        string nombre,
        string ruta)
    {
        var entidad = modelo.GetEntityTypes()
            .FirstOrDefault(e => e.ClrType.Name == nombre);

        if (entidad is not null)
        {
            return entidad;
        }

        var disponibles = string.Join(
            ", ",
            modelo.GetEntityTypes()
                .Where(e => e.ClrType.Name.StartsWith(nombre[..Math.Min(4, nombre.Length)],
                    StringComparison.OrdinalIgnoreCase))
                .Select(e => e.ClrType.Name)
                .OrderBy(n => n, StringComparer.Ordinal));

        throw new SemillaInvalidaException(
            $"El nombre del archivo '{ruta}' no corresponde a ninguna entidad mapeada. El nombre " +
            "del archivo de semilla tiene que coincidir con una entidad del modelo." +
            (disponibles.Length > 0
                ? $" Candidatos parecidos a '{nombre}': {disponibles}."
                : $" La entidad '{nombre}' no existe en el modelo."));
    }

    private object Construir(IReadOnlyEntityType entidad, FilaSemilla fila, ArchivoSemilla archivo)
    {
        var instancia = Activator.CreateInstance(entidad.ClrType)
                        ?? throw new SemillaInvalidaException(
                            $"No se pudo crear una instancia de {entidad.ClrType.Name}. Las entidades " +
                            "deben tener constructor sin parámetros para poder sembrarse.");

        foreach (var propiedad in entidad.GetProperties())
        {
            var info = propiedad.PropertyInfo;

            if (info is null || !info.CanWrite)
            {
                continue;
            }

            if (!fila.Valores.TryGetValue(propiedad.Name, out var valor))
            {
                continue;
            }

            info.SetValue(instancia, Convertir(entidad, propiedad, valor, archivo));
        }

        return instancia;
    }

    private object? Convertir(
        IReadOnlyEntityType entidad,
        IReadOnlyProperty propiedad,
        JsonElement valor,
        ArchivoSemilla archivo)
    {
        // Una FK escrita con el Codigo del catálogo, nunca con su número (ver el comentario de la
        // clase). La principal tiene que tener columna Codigo; si no la tiene, es un error de diseño
        // del seed y se dice cuál.
        if (propiedad.IsForeignKey())
        {
            if (valor.ValueKind != JsonValueKind.String)
            {
                throw new SemillaInvalidaException(
                    $"{archivo.Ruta}: la clave foránea '{propiedad.Name}' se escribió como número. " +
                    "Las claves foráneas van con el Codigo del catálogo, entre comillas.");
            }

            var fk = entidad.GetForeignKeys()
                .FirstOrDefault(f => f.Properties.Contains(propiedad))
                ?? throw new SemillaInvalidaException(
                    $"{archivo.Ruta}: '{propiedad.Name}' es clave foránea pero no se encontró su " +
                    "definición en el modelo. Es un error del contexto, no del archivo.");

            return ResolverClaveDePrincipal(
                fk.PrincipalEntityType,
                valor.GetString()!,
                entidad.ClrType.Name,
                propiedad.Name,
                archivo);
        }

        var tipo = Nullable.GetUnderlyingType(propiedad.ClrType) ?? propiedad.ClrType;

        if (tipo == typeof(string))
        {
            return valor.ValueKind == JsonValueKind.Null ? null : valor.GetString();
        }

        if (valor.ValueKind == JsonValueKind.Null)
        {
            if (!propiedad.IsNullable)
            {
                throw new SemillaInvalidaException(
                    $"{archivo.Ruta}: '{propiedad.Name}' no admite null y la fila lo trae sin valor.");
            }

            return null;
        }

        if (tipo == typeof(bool))
        {
            // Se acepta true/false y también 1/0, que es como aparecen en algunos exportadores.
            return valor.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => valor.GetDecimal() != 0m,
                JsonValueKind.String when bool.TryParse(valor.GetString(), out var b) => b,
                _ => throw Error(archivo, propiedad, "un booleano (true/false o 1/0)")
            };
        }

        if (tipo == typeof(DateOnly))
        {
            return DateOnly.ParseExact(
                Texto(valor, archivo, propiedad),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }

        if (tipo == typeof(DateTime))
        {
            return DateTime.Parse(
                Texto(valor, archivo, propiedad),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        if (tipo == typeof(Guid))
        {
            return Guid.ParseExact(Texto(valor, archivo, propiedad), "D");
        }

        if (tipo == typeof(byte[]))
        {
            return Convert.FromBase64String(Texto(valor, archivo, propiedad));
        }

        if (EsNumerico(tipo))
        {
            return ConvertirNumero(valor, tipo, archivo, propiedad);
        }

        throw Error(archivo, propiedad, $"un valor de tipo {tipo.Name}, que el aplicador no sabe convertir");
    }

    private static bool EsNumerico(Type tipo) =>
        tipo == typeof(byte) || tipo == typeof(short) || tipo == typeof(int)
        || tipo == typeof(long) || tipo == typeof(decimal) || tipo == typeof(double);

    private static object ConvertirNumero(
        JsonElement valor,
        Type tipo,
        ArchivoSemilla archivo,
        IReadOnlyProperty propiedad)
    {
        // InvariantCulture en todo: un 5,5 en el JSON no es un error de redondeo, es un cinco con
        // coma que alguien copió de una hoja de cálculo en configuración regional española.
        if (valor.ValueKind == JsonValueKind.String)
        {
            var texto = valor.GetString()!;

            if (long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entero))
            {
                return Convert.ChangeType(entero, tipo, CultureInfo.InvariantCulture);
            }

            if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
            {
                return Convert.ChangeType(dec, tipo, CultureInfo.InvariantCulture);
            }

            throw Error(archivo, propiedad, $"un número (se leyó '{texto}')");
        }

        return Convert.ChangeType(valor.GetDecimal(), tipo, CultureInfo.InvariantCulture);
    }

    private static string Texto(JsonElement valor, ArchivoSemilla archivo, IReadOnlyProperty propiedad)
        => valor.ValueKind == JsonValueKind.String
            ? valor.GetString()!
            : throw Error(archivo, propiedad, "un texto entre comillas");

    private static SemillaInvalidaException Error(
        ArchivoSemilla archivo,
        IReadOnlyProperty propiedad,
        string esperado)
        => new($"{Path.GetFileName(archivo.Ruta)}: '{propiedad.Name}' no es {esperado}.");

    // ═════════════════════════════════════════════════════════════════════════════
    //  Resolución de claves foráneas
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Traduce el <c>Codigo</c> de una fila principal al valor de su clave primaria. Se consulta
    /// <c>{esquema}.{tabla}</c> leyendo el nombre del esquema, de la tabla y de las columnas del
    /// modelo de EF, sin ninguna lista de tablas en el código.
    /// </summary>
    private object ResolverClaveDePrincipal(
        IReadOnlyEntityType principal,
        string codigo,
        string entidadDependiente,
        string propiedadDependiente,
        ArchivoSemilla archivo)
    {
        var claveNatural = principal.FindProperty("Codigo");

        if (claveNatural is null)
        {
            throw new SemillaInvalidaException(
                $"{entidadDependiente}.{propiedadDependiente} apunta a {principal.ClrType.Name}, que " +
                "no tiene columna 'Codigo'. En el seed las claves foráneas se escriben con el Codigo " +
                "del catálogo; si esa tabla no lo tiene, hay que darle uno, no inventar un número.");
        }

        var clavePrimaria = principal.FindPrimaryKey()?.Properties.FirstOrDefault()
            ?? throw new SemillaInvalidaException(
                $"{principal.ClrType.Name} no tiene clave primaria, así que no se puede resolver su " +
                "clave desde el Codigo.");

        var mapa = CargarClaves(principal, claveNatural, clavePrimaria);

        if (!mapa.TryGetValue(codigo, out var id))
        {
            // Se listan los códigos disponibles porque el error más común es un error de tipeo en el
            // JSON, y ver la lista hace que se encuentre en un segundo en vez de buscar en el
            // catálogo a ojo.
            throw new SemillaInvalidaException(
                $"{archivo.Ruta}: {entidadDependiente}.{propiedadDependiente} = '{codigo}' no existe " +
                $"en {principal.GetSchema()}.{principal.GetTableName()}. Códigos disponibles: " +
                $"{string.Join(", ", mapa.Keys.Order(StringComparer.OrdinalIgnoreCase))}.");
        }

        return id;
    }

    private Dictionary<string, object> CargarClaves(
        IReadOnlyEntityType principal,
        IReadOnlyProperty natural,
        IReadOnlyProperty primaria)
    {
        var esquema = principal.GetSchema();
        var tabla = principal.GetTableName();

        if (esquema is null || tabla is null)
        {
            throw new SemillaInvalidaException(
                $"{principal.ClrType.Name} no tiene esquema ni tabla definidos en el modelo.");
        }

        var cacheKey = $"{esquema}.{tabla}";

        if (_cacheClaves.TryGetValue(cacheKey, out var existente))
        {
            return existente;
        }

        // Los nombres entre corchetes los pone el modelo; el ORDER BY no, y no hace falta.
        var filas = _consultor.Consultar(
            $"SELECT [{natural.GetColumnName()}], [{primaria.GetColumnName()}] " +
            $"FROM [{esquema}].[{tabla}]");

        var mapa = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in filas)
        {
            if (fila.TryGetValue(natural.GetColumnName(), out var codigo) && codigo is string clave
                && fila.TryGetValue(primaria.GetColumnName(), out var id) && id is not null)
            {
                mapa[clave] = id;
            }
        }

        _cacheClaves[cacheKey] = mapa;

        return mapa;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Permisos de los roles
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Asigna a cada rol sus permisos, en la tabla puente <c>seg.RolPermiso</c>.
    /// <para>Es idempotente por diseño: se agrega la relación que falta y no se quita ninguna. Un
    /// rol es de sistema, y un administrador puede haberle quitado un permiso a mano sin querer
    /// dejarlo así para siempre; volver a ponerlo en cada arranque haría imposible corregir un
    /// permiso equivocado. Cambiar el conjunto de permisos de un rol de sistema, entonces, es un
    /// cambio de despliegue: se edita <c>scripts/seed/seg.Rol.json</c> y se corre con
    /// <c>RUN_SEED=true</c>, o se cambia desde la UI y ya.</para>
    /// <para>Devuelve cuántas asignaciones nuevas hizo, para que el resumen de la siembra lo diga.</para>
    /// </summary>
    public async Task<int> AsignarPermisosDeRolAsync(
        IReadOnlyList<ArchivoSemilla> archivos,
        CancellationToken cancellationToken = default)
    {
        var conPermisos = archivos
            .SelectMany(a => a.Filas.Where(f => f.Permisos.Count > 0)
                                   .Select(f => (Archivo: a, Fila: f)))
            .ToArray();

        if (conPermisos.Length == 0)
        {
            return 0;
        }

        // Rol y Permiso tienen Id Guid asignado por la base, así que aquí sí hace falta leerlos por
        // su clave natural una vez y tener el mapa en memoria.
        var roles = await CargarRolesAsync(cancellationToken);
        var permisos = await CargarPermisosAsync(cancellationToken);

        var agregados = 0;

        foreach (var (archivo, fila) in conPermisos)
        {
            var nombreRol = fila.Valores[archivo.ClaveNatural].GetString()!;

            if (!roles.TryGetValue(nombreRol, out var rolId))
            {
                throw new SemillaInvalidaException(
                    $"El rol '{nombreRol}' en {archivo.Ruta} lista permisos pero no se insertó. " +
                    "Sin fila de rol no hay a quién asignárselos.");
            }

            var yaAsignados = await _context.Set<RolPermiso>()
                .Where(rp => rp.RolId == rolId)
                .Select(rp => rp.PermisoId)
                .ToListAsync(cancellationToken);

            foreach (var codigo in fila.Permisos)
            {
                if (!permisos.TryGetValue(codigo, out var permisoId))
                {
                    throw new SemillaInvalidaException(
                        $"El rol '{nombreRol}' lista el permiso '{codigo}', que no existe en " +
                        "seg.Permiso. Códigos disponibles: " +
                        $"{string.Join(", ", permisos.Keys.Order(StringComparer.OrdinalIgnoreCase))}.");
                }

                if (yaAsignados.Contains(permisoId))
                {
                    continue;
                }

                _context.Set<RolPermiso>().Add(new RolPermiso { RolId = rolId, PermisoId = permisoId });
                yaAsignados.Add(permisoId);
                agregados++;
            }
        }

        if (agregados > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Asignados {Cantidad} permiso(s) a roles desde la semilla.", agregados);

        return agregados;
    }

    private async Task<Dictionary<string, Guid>> CargarRolesAsync(CancellationToken cancellationToken)
    {
        var mapa = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var roles = await _context.Rol
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var rol in roles)
        {
            if (rol.Name is { } nombre)
            {
                mapa[nombre] = rol.Id;
            }
        }

        return mapa;
    }

    private async Task<Dictionary<string, Guid>> CargarPermisosAsync(CancellationToken cancellationToken)
    {
        var mapa = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var permisos = await _context.Permiso
            .AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var permiso in permisos)
        {
            mapa[permiso.Codigo] = permiso.Id;
        }

        return mapa;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Lectura de lo que ya existe
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Devuelve, para cada fila que ya existe en la tabla, si fue sembrada o la creó una persona.
    /// <para>Un valor ausente en el diccionario significa que la fila no existe todavía, y es la
    /// razón por la que esta lectura y la del bucle de inserción tienen que ver la misma tabla en el
    /// mismo instante: por eso se escribe una sola vez por archivo y no se vuelve a leer en medio.</para>
    /// </summary>
    private Dictionary<string, bool> LeerExistentes(IReadOnlyEntityType entidad, ArchivoSemilla archivo)
    {
        var natural = entidad.FindProperty(archivo.ClaveNatural)!;
        var esquema = entidad.GetSchema();
        var tabla = entidad.GetTableName();

        if (esquema is null || tabla is null)
        {
            throw new SemillaInvalidaException(
                $"{entidad.ClrType.Name} no tiene esquema ni tabla definidos en el modelo.");
        }

        // Catálogos de dominio y catálogos maestros llevan EsSembrado; seg.Rol, seg.Permiso y las
        // tablas puente son de Identity y no lo llevan. La ausencia de la columna se traduce en
        // "sembrada", que es lo correcto: son filas propiedad del seed, y el seed nunca las modifica.
        var columnaSembrado = entidad.FindProperty("EsSembrado")?.GetColumnName();

        var columnaNatural = natural.GetColumnName();

        var sql = columnaSembrado is null
            ? $"SELECT [{columnaNatural}] FROM [{esquema}].[{tabla}]"
            : $"SELECT [{columnaNatural}], [{columnaSembrado}] FROM [{esquema}].[{tabla}]";

        var mapa = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in _consultor.Consultar(sql))
        {
            if (fila.TryGetValue(columnaNatural, out var clave) && clave is string texto)
            {
                var sembrado = true;

                if (columnaSembrado is not null
                    && fila.TryGetValue(columnaSembrado, out var valor)
                    && valor is not null)
                {
                    sembrado = Convert.ToBoolean(valor, CultureInfo.InvariantCulture);
                }

                mapa[texto] = sembrado;
            }
        }

        return mapa;
    }
}
