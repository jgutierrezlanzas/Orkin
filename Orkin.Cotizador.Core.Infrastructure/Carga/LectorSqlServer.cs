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

    /// <param name="cadenaConexion">La cadena de conexión al servidor del cliente. Debe ser de solo lectura.</param>
    /// <param name="consulta">
    /// Una tabla (<c>dbo.MisProductos</c>), una vista (<c>dbo.vwProductosActivos</c>) o una
    /// consulta (<c>SELECT Codigo, Nombre, Precio FROM dbo.Productos WHERE Activo = 1</c>).
    /// </param>
    public LectorSqlServer(string cadenaConexion, string consulta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);
        ArgumentException.ThrowIfNullOrWhiteSpace(consulta);

        _cadenaConexion = cadenaConexion;
        _consulta = consulta;
    }

    public string TipoOrigen => "SQL_SERVER";

    public async Task<IReadOnlyList<Dictionary<string, object?>>> LeerAsync(CancellationToken cancellationToken = default)
    {
        var filas = new List<Dictionary<string, object?>>();

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);

        await using var comando = new SqlCommand(_consulta, conexion);
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
