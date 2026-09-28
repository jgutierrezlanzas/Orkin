using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Orkin.Cotizador.Core.Infrastructure.Persistence.Seed;

/// <summary>
/// Lee y valida los archivos de <c>scripts/seed/</c> antes de tocar la base (§5.20).
/// <para><b>Por qué toda la validación es aquí y no al escribir:</b> una siembra a medias deja
/// catálogos incompletos, y un catálogo incompleto no falla: el formulario simplemente no ofrece una
/// opción que ayer sí ofrecía. Esa clase de fallo se descubre cuando un cliente se queja, que es la
/// forma más cara de descubrirla. Validando el archivo entero antes de abrir la transacción, un
/// error de tipeo en un nombre de columna o en un código es un fallo de arranque con el nombre del
/// archivo y el número de fila.</para>
/// <para><b>Por qué el nombre del archivo es parte del contrato:</b> <c>{esquema}.{entidad}.json</c>.
/// Un archivo que nombra una tabla inexistente, o que le pone el esquema equivocado, casi siempre es
/// una falta de ortografía, y una falta de ortografía en el seed se descubre tarde.</para>
/// </summary>
public static class LectorSemilla
{
    /// <summary>
    /// Lee todos los <c>*.json</c> de la carpeta. No toca la base: solo archivos y modelo de EF.
    /// </summary>
    /// <exception cref="SemillaInvalidaException">Con el detalle de todo lo que está mal.</exception>
    public static IReadOnlyList<ArchivoSemilla> Leer(string ruta, IReadOnlyModel modelo)
    {
        if (!Directory.Exists(ruta))
        {
            throw new SemillaInvalidaException(
                $"No existe la carpeta de semilla '{ruta}'. Con RUN_SEED=true el proceso necesita " +
                "leer los catálogos de ahí: sin ellos la aplicación arranca sin ningún estado, " +
                "modalidad ni permiso, y fallaría en la primera pantalla.");
        }

        var archivos = Directory.GetFiles(ruta, "*.json", SearchOption.TopDirectoryOnly);

        if (archivos.Length == 0)
        {
            throw new SemillaInvalidaException(
                $"La carpeta de semilla '{ruta}' no tiene ningún archivo .json. Revise que los " +
                "archivos se hayan copiado al contenedor.");
        }

        var errores = new List<string>();
        var resultado = new List<ArchivoSemilla>();

        foreach (var rutaArchivo in archivos.OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
        {
            var nombre = Path.GetFileName(rutaArchivo);

            try
            {
                resultado.Add(LeerUno(nombre, rutaArchivo, modelo));
            }
            catch (SemillaInvalidaException ex)
            {
                errores.Add(ex.Message);
            }
        }

        if (errores.Count > 0)
        {
            throw new SemillaInvalidaException(
                $"La semilla tiene {errores.Count} problema(s). Ningún archivo se aplicó; la base " +
                "queda como estaba:" + Environment.NewLine + string.Join(Environment.NewLine, errores));
        }

        return resultado;
    }

    private static ArchivoSemilla LeerUno(
        string nombreArchivo,
        string rutaArchivo,
        IReadOnlyModel modelo)
    {
        var (esquema, entidad) = SepararNombre(nombreArchivo);

        var tipoEntidad = modelo.GetEntityTypes()
            .FirstOrDefault(e => e.ClrType.Name == entidad
                                 && string.Equals(e.GetSchema(), esquema, StringComparison.OrdinalIgnoreCase));

        if (tipoEntidad is null)
        {
            // Se busca por las dos partes a la vez para que el mensaje diga cuál de las dos está mal.
            var existeOtroEsquema = modelo.GetEntityTypes()
                .Any(e => e.ClrType.Name == entidad);

            var pista = existeOtroEsquema
                ? $"La entidad {entidad} sí existe en el modelo, pero en el esquema " +
                  $"'{modelo.GetEntityTypes().First(e => e.ClrType.Name == entidad).GetSchema()}', " +
                  $"no en '{esquema}'."
                : $"No existe ninguna entidad llamada {entidad} en el modelo.";

            throw new SemillaInvalidaException(
                $"{nombreArchivo}: {pista} El nombre del archivo debe ser " +
                $"'{{esquema}}.{{entidad}}.json' tal como se llama la tabla.");
        }

        using var documento = JsonDocument.Parse(
            File.ReadAllText(rutaArchivo),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var raiz = documento.RootElement;

        if (raiz.ValueKind != JsonValueKind.Object)
        {
            throw new SemillaInvalidaException(
                $"{nombreArchivo}: la raíz del JSON debe ser un objeto con 'claveNatural' y 'filas'.");
        }

        var claveNatural = LeerCadena(raiz, "claveNatural") ?? "Codigo";

        if (tipoEntidad.FindProperty(claveNatural) is null)
        {
            var claves = tipoEntidad.FindPrimaryKey()?.Properties.Select(p => p.Name).ToArray() ?? ["Id"];

            throw new SemillaInvalidaException(
                $"{nombreArchivo}: 'claveNatural' dice '{claveNatural}', pero {entidad} no tiene esa " +
                $"propiedad. Propiedades de clave disponibles: {string.Join(", ", claves)}.");
        }

        if (!raiz.TryGetProperty("filas", out var filas) || filas.ValueKind != JsonValueKind.Array)
        {
            throw new SemillaInvalidaException(
                $"{nombreArchivo}: falta la propiedad 'filas', o no es un arreglo.");
        }

        if (filas.GetArrayLength() == 0)
        {
            throw new SemillaInvalidaException(
                $"{nombreArchivo}: 'filas' está vacío. Un archivo de semilla sin filas casi siempre " +
                "es un archivo que se creó en blanco y se olvidó; si la intención era dejar el " +
                "catálogo vacío, bórralo en lugar de dejarlo vacío.");
        }

        var resultado = new List<FilaSemilla>();
        var clavesVistas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var indice = 0;

        foreach (var fila in filas.EnumerateArray())
        {
            indice++;

            if (fila.ValueKind != JsonValueKind.Object)
            {
                throw new SemillaInvalidaException(
                    $"{nombreArchivo}: la fila {indice} no es un objeto JSON.");
            }

            var valores = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            foreach (var propiedad in fila.EnumerateObject())
            {
                // Las claves que empiezan con "//" son comentarios del archivo. Se permiten porque
                // un JSON estándar no admite comentarios y esta es la forma de dejar el porqué de
                // una fila escrita sin tocar el código.
                if (propiedad.Name.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                valores[propiedad.Name] = propiedad.Value.Clone();
            }

            if (!valores.TryGetValue(claveNatural, out var valorClave)
                || valorClave.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(valorClave.GetString()))
            {
                throw new SemillaInvalidaException(
                    $"{nombreArchivo}: la fila {indice} no tiene '{claveNatural}', o no es texto. " +
                    "La clave natural identifica la fila de forma estable: sin ella no se puede " +
                    "decidir si ya existe y la siembra insertaría duplicados en cada corrida.");
            }

            var clave = valorClave.GetString()!;

            if (clavesVistas.TryGetValue(clave, out var filaPrevia))
            {
                throw new SemillaInvalidaException(
                    $"{nombreArchivo}: '{claveNatural}' = '{clave}' aparece en las filas {filaPrevia} " +
                    $"y {indice}. Dentro de un mismo archivo no puede repetirse: la base tiene un " +
                    "índice único sobre esa columna y la segunda insercción fallaría a mitad de la " +
                    "transacción.");
            }

            clavesVistas[clave] = indice;

            var permisos = new List<string>();

            if (valores.TryGetValue("permisos", out var listaPermisos)
                && listaPermisos.ValueKind == JsonValueKind.Array)
            {
                valores.Remove("permisos");

                foreach (var permiso in listaPermisos.EnumerateArray())
                {
                    if (permiso.ValueKind == JsonValueKind.String && permiso.GetString() is { } codigo)
                    {
                        permisos.Add(codigo);
                    }
                }

                if (permisos.Count == 0 && listaPermisos.GetArrayLength() > 0)
                {
                    throw new SemillaInvalidaException(
                        $"{nombreArchivo}, fila {indice}: 'permisos' está presente pero ninguno de " +
                        "sus elementos es texto. Un rol sin permisos es un rol que no puede hacer " +
                        "nada; si es la intención, borre la propiedad.");
                }
            }

            resultado.Add(new FilaSemilla { Valores = valores, Permisos = permisos });
        }


        return new ArchivoSemilla
        {
            Ruta = rutaArchivo,
            Esquema = esquema,
            Entidad = entidad,
            ClaveNatural = claveNatural,
            Filas = resultado
        };
    }

    /// <summary>
    /// Parte <c>dbo.MetodoCosteoCatalogo.json</c> en esquema y entidad. Solo acepta exactamente dos
    /// partes: <c>dbo.MetodoCosteo.json</c> es un nombre plausible y equivocado, y aceptarlo
    /// convertiría el error en un fallo de arranque más adelante.
    /// </summary>
    private static (string Esquema, string Entidad) SepararNombre(string nombreArchivo)
    {
        var sinExtension = Path.GetFileNameWithoutExtension(nombreArchivo);
        var partes = sinExtension.Split('.');

        if (partes.Length != 2 || string.IsNullOrWhiteSpace(partes[0]) || string.IsNullOrWhiteSpace(partes[1]))
        {
            throw new SemillaInvalidaException(
                $"{nombreArchivo}: el nombre debe tener la forma '{{esquema}}.{{entidad}}.json', por " +
                "ejemplo 'dbo.MetodoCosteoCatalogo.json' o 'seg.Rol.json'.");
        }

        return (partes[0], partes[1]);
    }

    private static string? LeerCadena(JsonElement elemento, string nombre)
        => elemento.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
