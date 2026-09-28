using Microsoft.Extensions.Configuration;

namespace Orkin.Cotizador.Core.Infrastructure.Configuration;

/// <summary>
/// La <b>única</b> forma de construir la configuración, compartida por la API y por el design-time
/// factory de EF Core.
/// <para>Que haya una sola es la razón de existir de esta clase. Si el <c>dotnet ef migrations
/// add</c> y el arranque de la API resolvieran los secretos de forma distinta, la migración se
/// generaría mirando una base y se aplicaría contra otra, y el error aparecería weeks después, en el
/// despliegue.</para>
/// <para><b>Orden de precedencia</b> (de mayor a menor):</para>
/// <list type="number">
///   <item><description><b>Variables de ambiente, y los archivos <c>.env</c> en el mismo
///   nivel.</b> Los <c>.env</c> se cargan _antes_ de construir la configuración y entran por
///   <c>AddEnvironmentVariables</c>, así que comparten la precedencia de las variables de ambiente.
///   Esto es deliberado: <c>.env</c> existe para guardar lo que no debe estar en el repositorio
///   —una cadena de conexión, una clave de firma— y para eso tiene que poder pisar el valor vacío
///   o de ejemplo que dejó el <c>appsettings.json</c> versionado. Entre los archivos <c>.env</c> sí
///   hay orden, y es el de <see cref="ArchivosEnv"/>.</description></item>
///   <item><description><c>appsettings.{Entorno}.json</c>.</description></item>
///   <item><description><c>appsettings.json</c>.</description></item>
/// </list>
/// <para>La consecuencia práctica: para <b>forzar</b> un valor en un despliegue hay que usar una
/// variable de ambiente; un <c>appsettings.json</c> no puede ganarle. Para <b>no</b> publicar un
/// secreto, la clave va en el <c>.env</c> y el <c>appsettings.json</c> no la menciona.</para>
/// </summary>
public static class ConfiguracionBootstrap
{
    /// <summary>
    /// Archivos <c>.env</c> en orden de <b>menor</b> a mayor prioridad: se cargan en este orden y
    /// cada uno pisa al anterior.
    /// </summary>
    private static readonly string[] ArchivosEnv = [".env", ".env.{ENTORNO}", ".env.local"];

    /// <summary>
    /// Carga los archivos <c>.env</c> en las variables de ambiente. Idempotente: llamarla dos veces
    /// no cambia nada, porque una variable ya presente no se pisa.
    /// <para>Se expone aparte para que el host pueda registrarla en el log <b>antes</b> de construir
    /// nada, que es cuando todavía se puede avisar que se están usando secretos de un archivo en vez
    /// de un gestor.</para>
    /// </summary>
    public static IReadOnlyList<string> CargarArchivosEnv(string entorno, string? directorioInicial = null)
    {
        var nombres = ArchivosEnv
            .Select(n => n.Replace("{ENTORNO}", entorno, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return CargadorArchivoEnv.CargarEnCascada(nombres, directorioInicial);
    }

    /// <summary>
    /// Construye la configuración completa. No lee ni escribe nada más.
    /// <para><b>Por qué recibe los argumentos de la línea de comandos:</b> la API reemplaza su
    /// configuración por esta, y si acá no se sumaran los argumentos, un <c>--urls</c> o un
    /// <c>--migrate-only</c> que se pase al arrancar se perdería sin aviso. Pasan últimos en la
    /// lista, o sea con la precedencia más alta, que es lo que espera cualquiera que los escriba:
    /// lo que se escribe en la consola gana sobre lo que está en un archivo.</para>
    /// </summary>
    /// <param name="entorno">Nombre del entorno: decide qué <c>.env</c> y qué
    /// <c>appsettings.{entorno}.json</c> se cargan.</param>
    /// <param name="directorioBase">Dónde buscar los archivos. Si se omite, el directorio del
    /// ensamblado, que es donde el SDK copia el <c>appsettings.json</c> al compilar.</param>
    /// <param name="argumentos">Argumentos de la línea de comandos, si los hay.</param>
    public static IConfigurationRoot Construir(
        string entorno,
        string? directorioBase = null,
        IEnumerable<string>? argumentos = null)
    {
        CargarArchivosEnv(entorno, directorioBase);

        return new ConfigurationBuilder()
            .SetBasePath(directorioBase ?? AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{entorno}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(argumentos?.ToArray() ?? [])
            .Build();
    }
}
