using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Notificaciones;

/// <summary>
/// Barrera antes de invocar cualquier <c>SINCO_NOTIFICAR_*</c> en la base real.
/// <para><b>Por qué existe:</b> el script versionado muestra SPs de solo lectura, pero la base de
/// producción puede haber cambiado desde entonces. Uno de esos SPs podría haberse convertido en un
/// procedimiento que borra el staging <c>dbo.NotificarProgramacionVisitaTemporal</c> y lo vuelve a
/// llenar. Llamarlo "para consultar" sería mutar datos del ERP sin querer. Acá se lee la definición
/// con <c>OBJECT_DEFINITION</c>, se comprueba el permiso con <c>HAS_PERMS_BY_NAME</c> y se rechaza
/// todo lo que no sea una lectura.</para>
/// <para>El SQL es fijo y el nombre del SP viaja siempre en <c>SqlParameter</c>: nunca se interpola.</para>
/// </summary>
public sealed partial class ValidadorSpNotificacion
{
    /// <summary>Nombres que se revisan. Los dos no invocados se validan igual: están en el mismo contrato.</summary>
    public static readonly string[] NombresConocidos =
    [
        "dbo.SINCO_NOTIFICAR_CLIENTES",
        "dbo.SINCO_NOTIFICAR_CORREOS_ENVIADOS",
        "dbo.SINCO_NOTIFICAR_PROXIMOS_SERVICIOS",
        "dbo.SINCO_NOTIFICAR_REQUISAS_ERRADAS"
    ];

    private const string SqlValidacion =
        "SELECT OBJECT_DEFINITION(OBJECT_ID(@nombre)) AS Definicion, " +
        "HAS_PERMS_BY_NAME(@nombre, 'OBJECT', 'EXECUTE') AS PuedeEjecutar";

    private static readonly ConcurrentDictionary<string, ResultadoValidacionSp> Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly ILectorSql _lector;

    public ValidadorSpNotificacion(ILectorSql lector) => _lector = lector;

    /// <summary>Limpia la caché de definiciones ya revisadas (la usan las pruebas y un cambio en caliente).</summary>
    public static void LimpiarCache() => Cache.Clear();

    public async Task<ResultadoValidacionSp> ValidarAsync(
        string nombre,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        if (Cache.TryGetValue(nombre, out var cached))
        {
            return cached;
        }

        var filas = await _lector.ConsultarAsync(SqlValidacion, cancellationToken, ("@nombre", nombre));
        var fila = filas.Count > 0 ? filas[0] : null;

        if (fila is null)
        {
            return new ResultadoValidacionSp(nombre, false, false, false,
                "SQL Server no devolvió definición para el SP; no se invoca.");
        }

        var definicion = Convert.ToString(fila.GetValueOrDefault("Definicion"));
        var puedeEjecutar = Convert.ToInt32(fila.GetValueOrDefault("PuedeEjecutar") ?? 0) != 0;
        var soloLectura = EsSoloLectura(definicion);

        string? motivo = null;
        if (string.IsNullOrWhiteSpace(definicion))
        {
            motivo = $"No existe {nombre} o el usuario no puede leer su definición; no se invoca.";
        }
        else if (!soloLectura)
        {
            motivo = $"La definición de {nombre} contiene escritura o ejecución dinámica; no se invoca.";
        }
        else if (!puedeEjecutar)
        {
            motivo = $"El usuario de la aplicación no tiene EXECUTE sobre {nombre}; no se invoca.";
        }

        var resultado = new ResultadoValidacionSp(nombre, motivo is null, puedeEjecutar, soloLectura, motivo);

        // Solo se cachea el resultado favorable: un rechazo puede deberse a un permiso que se
        // corrigió minutos después, y quedarse con él dejaría el job callado sin reason.
        if (resultado.Valido)
        {
            Cache[nombre] = resultado;
        }

        return resultado;
    }

    /// <summary>
    /// Comprueba que el texto de un SP no contenga sentencias de escritura ni ejecución dinámica.
    /// <para>Se quitan comentarios y literales antes de buscar: un SP legítimo puede decir
    /// <c>-- no hacer UPDATE aquí</c> o comparar un estado contra <c>'DELETE'</c>, y eso no es una
    /// escritura.</para>
    /// </summary>
    public static bool EsSoloLectura(string? definicion)
    {
        if (string.IsNullOrWhiteSpace(definicion))
        {
            return false;
        }

        var texto = SinComentariosNiLiterales(definicion);

        // OBJECT_DEFINITION devuelve el encabezado CREATE/ALTER PROCEDURE, que por definición
        // contiene CREATE. Solo se quita ese encabezado inicial: un CREATE TABLE dentro del SP
        // sigue siendo escritura y debe rechazarse.
        texto = CabeceraProcedimiento().Replace(texto, string.Empty, 1);

        return !PalabrasPeligrosas().IsMatch(texto);
    }

    private static string SinComentariosNiLiterales(string texto)
    {
        var salida = new System.Text.StringBuilder(texto.Length);
        var i = 0;

        while (i < texto.Length)
        {
            if (texto[i] == '-' && i + 1 < texto.Length && texto[i + 1] == '-')
            {
                while (i < texto.Length && texto[i] != '\n') i++;
                continue;
            }

            if (texto[i] == '/' && i + 1 < texto.Length && texto[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < texto.Length && !(texto[i] == '*' && texto[i + 1] == '/')) i++;
                i = Math.Min(i + 2, texto.Length);
                continue;
            }

            if (texto[i] == '\'')
            {
                // El prefijo N'...' se consume junto con la comilla; el nombre N queda afuera y
                // no puede formar una palabra peligrosa por sí solo.
                i++;
                while (i < texto.Length)
                {
                    if (texto[i] == '\'')
                    {
                        if (i + 1 < texto.Length && texto[i + 1] == '\'') { i += 2; continue; }
                        i++;
                        break;
                    }
                    i++;
                }
                salida.Append(' ');
                continue;
            }

            salida.Append(texto[i]);
            i++;
        }

        return salida.ToString();
    }

    [GeneratedRegex(
        @"^\s*(CREATE|ALTER)\s+(OR\s+ALTER\s+)?(PROCEDURE|PROC)\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CabeceraProcedimiento();

    [GeneratedRegex(
        @"\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|DROP|ALTER|CREATE|EXEC|EXECUTE|GRANT|REVOKE|OPENQUERY|OPENDATASOURCE|OPENROWSET|sp_executesql|xp_cmdshell)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PalabrasPeligrosas();
}
