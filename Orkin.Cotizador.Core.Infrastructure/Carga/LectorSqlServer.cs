using Microsoft.Data.SqlClient;

using Orkin.Cotizador.Core.Domain.Carga;

namespace Orkin.Cotizador.Core.Infrastructure.Carga;

/// <summary>
/// Lee filas de una tabla o vista de SQL Server, o de una consulta arbitraria. Devuelve
/// diccionarios clave-valor para que el proceso de carga no sepa que el origen fue SQL.
/// </summary>
public sealed class LectorSqlServer : ILectorOrigen
{
    private readonly string _cadenaConexion;
    private readonly string _consulta;
    private readonly int _tiempoEspera;

    /// <param name="cadenaConexion">La cadena de conexión al servidor del cliente. Debe ser de solo lectura.</param>
    /// <param name="consulta">
    /// Una tabla (<c>dbo.MisProductos</c>), una vista (<c>dbo.vwProductosActivos</c>) o una
    /// consulta (<c>SELECT Codigo, Nombre, Precio FROM dbo.Productos WHERE Activo = 1</c>).
    /// </param>
    /// <param name="tiempoEsperaSegundos">Espera de la consulta, tomada de la conexión configurada.</param>
    public LectorSqlServer(string cadenaConexion, string consulta, int tiempoEsperaSegundos = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);
        ArgumentException.ThrowIfNullOrWhiteSpace(consulta);

        _cadenaConexion = cadenaConexion;
        _consulta = ConstruirConsulta(consulta);
        _tiempoEspera = tiempoEsperaSegundos is >= 1 and <= 600 ? tiempoEsperaSegundos : 30;
    }

    public string TipoOrigen => "SQL_SERVER";

    /// <summary>
    /// Un nombre de objeto suelto no es una consulta: para SQL Server es una llamada a
    /// procedimiento, y al ejecutarla contra una vista contesta "the request for procedure
    /// 'X' failed because 'X' is a view object" (error 2809). El mensaje no dice "olvidé el
    /// SELECT", dice "esa cosa es una vista", que manda a buscarla donde no está el problema.
    /// <para>Por eso, cuando el texto es un solo identificador —puede traer corchetes, como
    /// <c>[dbo].[vw_Productos]</c>— se envuelve en <c>SELECT * FROM</c>. Todo lo demás se
    /// manda tal cual: si alguien escribe una consulta con <c>WHERE</c>, <c>JOIN</c> o
    /// <c>UNION</c>, el texto ya es una consulta y perderle el envoltorio la rompería.</para>
    /// </summary>
    private static string ConstruirConsulta(string consulta)
    {
        var texto = consulta.Trim().TrimEnd(';').Trim();

        var esUnSoloObjeto = texto.Length > 0
            && !texto.Contains(' ')
            && !texto.Contains('\t')
            && !texto.Contains('\n')
            && !texto.Contains('\r')
            && (texto.Contains('.') || texto.StartsWith('['));

        return esUnSoloObjeto ? $"SELECT * FROM {texto}" : texto;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> LeerAsync(CancellationToken cancellationToken = default)
    {
        var filas = new List<Dictionary<string, object?>>();

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);

        await using var comando = new SqlCommand(_consulta, conexion)
        {
            // El tiempo de espera va aquí y no solo en la apertura de la conexión: una consulta
            // contra el ERP del cliente que se cuelgue tiene que soltarse sola, o el flujo queda
            // colgado esperando y la corrida nunca llega a la bitácora.
            CommandTimeout = _tiempoEspera
        };

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        while (await lector.ReadAsync(cancellationToken))
        {
            var fila = new Dictionary<string, object?>(lector.FieldCount, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < lector.FieldCount; i++)
            {
                var nombre = lector.GetName(i);
                var valor = lector.IsDBNull(i) ? null : lector.GetValue(i);
                fila[nombre] = valor;
            }

            filas.Add(fila);
        }

        return filas;
    }
}
