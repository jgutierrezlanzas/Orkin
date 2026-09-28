using System.Text;

namespace Orkin.Cotizador.Core.Infrastructure.Configuration;

/// <summary>
/// Lector de archivos <c>.env</c>. Existe en lugar de usar DotNetEnv para que el
/// <b>design-time factory</b> de EF Core pueda leer los mismos secretos que la aplicación: si
/// <c>dotnet ef migrations add</c> y la API tuvieran dos reglas distintas para encontrar el
/// <c>.env</c>, la migración se generaría contra una base y se aplicaría contra otra.
/// <para>El formato es el de siempre: <c>CLAVE=valor</c>, se ignoran las líneas en blanco y las que
/// arrancan con <c>#</c>, y el valor puede ir entre comillas dobles o simples para poder tener
/// espacios y <c>#</c>.</para>
/// <para>No hace nada con las variables que ya existen en el proceso: un <c>.env</c> es el valor por
/// defecto, y lo que llegó por variable de ambiente manda. Así el contenedor de producción no
/// necesita el archivo.</para>
/// </summary>
public static class CargadorArchivoEnv
{
    /// <summary>
    /// Busca el archivo subiendo desde <paramref name="directorioInicial"/> hasta la raíz, para que
    /// funcione igual ejecutando desde la raíz de la solución, desde <c>bin\Debug</c> o desde la
    /// carpeta del proyecto de pruebas.
    /// <para>Devuelve <c>null</c> si no lo encuentra. No es un error: la configuración puede venir
    /// completa por variables de ambiente.</para>
    /// </summary>
    public static string? BuscarArchivo(string nombreArchivo, string? directorioInicial = null)
    {
        var directorio = new DirectoryInfo(
            directorioInicial ?? AppContext.BaseDirectory);

        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, nombreArchivo);
            if (File.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        return null;
    }

    /// <summary>
    /// Carga el archivo en las variables de ambiente del proceso, sin pisar las que ya existen.
    /// Devuelve las claves que efectivamente se establecieron, para poder registrarlas en el log
    /// <b>solo por su nombre</b>.
    /// </summary>
    public static IReadOnlyList<string> Cargar(string rutaArchivo)
    {
        var aplicadas = new List<string>();

        if (!File.Exists(rutaArchivo))
        {
            return aplicadas;
        }

        foreach (var linea in File.ReadAllLines(rutaArchivo, Encoding.UTF8))
        {
            var clave = LeerClave(linea, out var valor);

            if (clave is null)
            {
                continue;
            }

            // Una variable ya presente gana: es el que llegó de Kubernetes, de Docker o de la
            // consola, y es más específico que un archivo del repositorio.
            if (Environment.GetEnvironmentVariable(clave) is not null)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(clave, valor);
            aplicadas.Add(clave);
        }

        return aplicadas;
    }

    /// <summary>
    /// Carga, si existe, el primero de los archivos indicados. El orden de la lista es la
    /// prioridad: <c>.env.dev</c> pisa a <c>.env</c> porque se carga después.
    /// </summary>
    public static IReadOnlyList<string> CargarEnCascada(
        IReadOnlyList<string> nombresArchivo,
        string? directorioInicial = null)
    {
        var aplicadas = new List<string>();

        foreach (var nombre in nombresArchivo)
        {
            var ruta = BuscarArchivo(nombre, directorioInicial);
            if (ruta is not null)
            {
                aplicadas.AddRange(Cargar(ruta));
            }
        }

        return aplicadas;
    }

    /// <summary>
    /// Extrae <c>CLAVE=valor</c> de una línea. Devuelve <c>null</c> si la línea no es una
    /// asignación: comentario, blanco, o algo que no se parece a un par.
    /// <para>Acepta <c>export</c> al inicio porque es la convención de los <c>.env</c> de Linux y
    /// alguien va a pegarlo desde un servidor.</para>
    /// </summary>
    private static string? LeerClave(string linea, out string? valor)
    {
        valor = null;

        var texto = linea.Trim();

        if (texto.Length == 0 || texto[0] == '#')
        {
            return null;
        }

        if (texto.StartsWith("export ", StringComparison.Ordinal))
        {
            texto = texto["export ".Length..].TrimStart();
        }

        var separador = texto.IndexOf('=');
        if (separador <= 0)
        {
            return null;
        }

        var clave = texto[..separador].Trim();
        if (clave.Length == 0)
        {
            return null;
        }

        var bruto = texto[(separador + 1)..].Trim();

        valor = DespojarComillas(bruto);
        return clave;
    }

    /// <summary>
    /// Quita las comillas envolventes y, si son dobles, interpreta los escapes. Las comillas son
    /// lo que permite que un valor lleve <c>#</c>, espacios o saltos de línea sin que el parser se
    /// confunda.
    /// </summary>
    private static string? DespojarComillas(string texto)
    {
        if (texto.Length >= 2 && texto[0] == '"' && texto[^1] == '"')
        {
            return texto[1..^1]
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal)
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        if (texto.Length >= 2 && texto[0] == '\'' && texto[^1] == '\'')
        {
            return texto[1..^1];
        }

        // Sin comillas, un # abre un comentario en mitad del valor: es el comportamiento de shell.
        var comentario = texto.IndexOf(" #", StringComparison.Ordinal);
        return comentario >= 0 ? texto[..comentario].TrimEnd() : texto;
    }
}
