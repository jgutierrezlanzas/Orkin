using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Orkin.Cotizador.Core.Infrastructure.Configuration;
using Orkin.Cotizador.Core.Infrastructure.Options;

namespace Orkin.Cotizador.Core.Infrastructure.Persistence;

/// <summary>
/// Permite que <c>dotnet ef migrations add</c> y <c>dotnet ef database update</c> construyan el
/// contexto.
/// <para><b>Por qué existe una factory y no basta con que la API registre el contexto:</b> EF Core
/// necesita construir el contexto en su propio proceso, sin pasar por <c>Program.cs</c>, y lo hace
/// buscando primero esta factory. Sin ella, la tooling tendría que adivinar la cadena de conexión, y
/// adivinar una cadena de conexión significa que la migración se genera mirando una base y después
/// se aplica contra otra.</para>
/// <para><b>Por qué usa <see cref="ConfiguracionBootstrap"/> y no lee el <c>.env</c> por su cuenta:</b>
/// la factory tiene que resolver los secretos exactamente igual que la API. Si el
/// <c>dotnet ef</c> leyera el <c>.env</c> con una precedencia distinta a la del arranque, el día que
/// un desarrollador tenga <c>.env.local</c> con otra base, la migración se generaría contra la de
/// desarrollo y se aplicaría en la de pruebas sin que nadie lo note.</para>
/// <para><b>Por qué busca la raíz de la solución subiendo directorios:</b> cuando <c>dotnet ef</c> se
/// lanza desde la raíz, el directorio actual es el de la solución; cuando se lanza desde la carpeta
/// del proyecto, es uno más abajo. Subir hasta encontrar <c>Orkin.Cotizador.slnx</c> cubre los dos
/// casos y también cuando la herramienta se ejecuta desde otra terminal.</para>
/// <para><b>Por qué los <c>.env</c> viven en la raíz de la solución y no en la carpeta de cada
/// proyecto:</b> <see cref="CargadorArchivoEnv.BuscarArchivo"/> sube directorios pero no baja, así que
/// un <c>.env</c> en la raíz lo encuentra quien arranca desde la API, desde las pruebas y desde
/// estas herramientas por igual. Repartidos por proyecto, cada proceso leería un archivo distinto y
/// la divergencia aparecería justo en el día en que alguien cambia la base en un solo lugar.</para>
/// </summary>
public sealed class CotizadorDbContextFactory : IDesignTimeDbContextFactory<CotizadorDbContext>
{
    private const string NombreSolucion = "Orkin.Cotizador.slnx";

    public CotizadorDbContext CreateDbContext(string[] args)
    {
        var directorioBase = BuscarRaizSolucion(AppContext.BaseDirectory)
                              ?? BuscarRaizSolucion(Directory.GetCurrentDirectory())
                              ?? Directory.GetCurrentDirectory();

        // El entorno solo decide qué .env.{Entorno} entra en la cascada. EF no tiene ASPNETCORE_ENVIRONMENT
        // de forma fiable, así que se respeta si viene y se usa Development si no, que es el caso
        // normal de un `dotnet ef migrations add` en una máquina de desarrollo.
        var entorno = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                      ?? "Development";

        var configuracion = ConfiguracionBootstrap.Construir(entorno, directorioBase);

        // Se lee exactamente la misma clave que lee el binder del arranque. Que sea la misma no es
        // una coincidencia: si aquí se leyera otra —y la tentación es usar la que trae la plantilla
        // de EF, ConnectionStrings__AlgunaCosa— la migración se generaría mirando una base y
        // después el arranque miraría otra, sin que nada lo delate hasta que una migración intente
        // tocar una tabla que no existe.
        var connectionString = configuracion[$"{DatabaseOptions.Seccion}:{nameof(DatabaseOptions.ConnectionString)}"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Falla con el mensaje completo en vez de dejar que EF diga "no se pudo abrir la
            // conexión": el error útil es "falta esto", y se escribe una vez, no tres.
            throw new InvalidOperationException(
                $"No se encontró la cadena de conexión en " +
                $"'{DatabaseOptions.Seccion}:{nameof(DatabaseOptions.ConnectionString)}'. " +
                $"Los archivos .env se buscan subiendo desde {directorioBase}, y el lugar que " +
                "cubre tanto a la API como a estas herramientas es la raíz de la solución. " +
                "Copie .env.Development.example a .env.Development en la raíz y complete los " +
                "valores, o defina la variable de ambiente " +
                $"'{DatabaseOptions.Seccion}__ConnectionString'.");
        }

        var opciones = new DbContextOptionsBuilder<CotizadorDbContext>()
            // Sin reintentos ni timeout: para generar una migración solo hace falta leer el modelo,
            // y una conexión reintentable a propósito no tiene sentido aquí.
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(CotizadorDbContext).Assembly.FullName))
            .Options;

        return new CotizadorDbContext(opciones);
    }

    /// <summary>Sube directorios buscando el archivo de solución. Devuelve <c>null</c> si no aparece.</summary>
    private static string? BuscarRaizSolucion(string directorioInicial)
    {
        var directorio = new DirectoryInfo(directorioInicial);

        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, NombreSolucion)))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        return null;
    }
}
