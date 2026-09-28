using System.Data;
using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Orkin.Cotizador.Core.Infrastructure.Persistence;

/// <summary>
/// Ejecuta un <c>SELECT</c> y devuelve las filas como pares columna/valor, sin materializar
/// entidades.
/// <para><b>Por qué existe:</b> hay tres cosas que EF Core 10 no deja hacer y que el sistema
/// necesita. Una, leer una tabla cuyo tipo solo se conoce en tiempo de ejecución —que es lo que
/// hace la siembra, donde el <c>Codigo</c> de un catálogo se resuelve a su <c>Id</c> sin tener la
/// entidad en compilación—. Dos, reservar un correlativo con un <c>UPDATE</c> que devuelva el valor
/// ya incrementado, porque <c>SaveChanges</c> no devuelve nada y porque la fila tiene que quedar
/// bloqueada mientras dura la operación. Y tres, participar de la transacción vigente sin
/// reproducirla a mano.</para>
/// <para><b>Por qué los nombres de tabla y de columna salen del modelo y no de constantes:</b>
/// <c>IModel</c> es la fuente de verdad de cómo se llaman las cosas. Un nombre escrito a mano es un
/// nombre que se desincroniza en la primera vez que se renombra una tabla, y que en esa ocasión
/// falla en producción y no en la compilación.</para>
/// <para><b>Por qué SQL crudo y no entidades:</b> en ninguno de los tres usos interesa el
/// <c>ChangeTracker</c>, ni <c>rowversion</c>, ni las cascadas. Interesa leer un par de columnas,
/// bloquear una fila e insertar. Y las lecturas son de tablas de catálogo, que caben en memoria
/// sin problema.</para>
/// <para><b>Transparente a la transacción.</b> Si hay una abierta, el comando se une a ella; si no
/// la hay, se abre y se cierra una propia. En el segundo caso la operación es atómica por sí
/// sola, porque una sola sentencia corre en su propia transacción: no queda una ventana entre la
/// lectura y la escritura.</para>
/// </summary>
public sealed class EjecutorSql
{
    private readonly CotizadorDbContext _contexto;

    public EjecutorSql(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>
    /// Ejecuta un <c>SELECT</c> de forma síncrona. La usan las lecturas de la siembra, que corren
    /// una sola vez al arrancar y dentro de una transacción ya abierta.
    /// </summary>
    public List<Dictionary<string, object?>> Consultar(
        string sql,
        params (string Nombre, object? Valor)[] parametros)
    {
        var conexion = _contexto.Database.GetDbConnection();
        var propia = conexion.State != ConnectionState.Open;

        if (propia)
        {
            conexion.Open();
        }

        try
        {
            using var comando = Preparar(conexion, sql, parametros);

            // El lector se dispone acá y no se deja al using del comando. Un SqlCommand libera su
            // lector al eliminarse, así que el using del comando bastaría en el camino feliz, pero
            // no cuando algo falla: si Leer tira, el lector queda vivo y el comando se elimina
            // igual, y la transacción que se iba a deshacer después no puede ni ejecutarse porque
            // hay un lector abierto. El efecto visible es que el error real del trabajo que falló
            // desaparece y queda un "ya hay un DataReader abierto" que no dice nada del problema.
            using var lector = comando.ExecuteReader();
            return Leer(lector);
        }
        finally
        {
            if (propia)
            {
                conexion.Close();
            }
        }
    }

    /// <summary>
    /// Ejecuta un <c>SELECT</c> de forma asíncrona, para el camino de una petición.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> ConsultarAsync(
        string sql,
        CancellationToken cancellationToken = default,
        params (string Nombre, object? Valor)[] parametros)
    {
        var conexion = _contexto.Database.GetDbConnection();
        var propia = conexion.State != ConnectionState.Open;

        if (propia)
        {
            await conexion.OpenAsync(cancellationToken);
        }

        try
        {
            await using var comando = Preparar(conexion, sql, parametros);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            return await LeerAsync(lector, cancellationToken);
        }
        finally
        {
            if (propia)
            {
                conexion.Close();
            }
        }
    }

    private DbCommand Preparar(
        DbConnection conexion,
        string sql,
        IReadOnlyList<(string Nombre, object? Valor)> parametros)
    {
        var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.Transaction = _contexto.Database.CurrentTransaction?.GetDbTransaction();

        foreach (var (nombre, valor) in parametros)
        {
            var parametro = comando.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor ?? DBNull.Value;
            comando.Parameters.Add(parametro);
        }

        return comando;
    }

    private static List<Dictionary<string, object?>> Leer(DbDataReader lector)
    {
        var filas = new List<Dictionary<string, object?>>();

        while (lector.Read())
        {
            filas.Add(Fila(lector));
        }

        return filas;
    }

    private static async Task<List<Dictionary<string, object?>>> LeerAsync(
        DbDataReader lector,
        CancellationToken cancellationToken)
    {
        var filas = new List<Dictionary<string, object?>>();

        while (await lector.ReadAsync(cancellationToken))
        {
            filas.Add(Fila(lector));
        }

        return filas;
    }

    private static Dictionary<string, object?> Fila(DbDataReader lector)
    {
        // OrdinalIgnoreCase porque el nombre de la columna y la clave que se le busca no siempre
        // coinciden en cómo los escribió quien lo escribió: SQL Server no distingue mayúsculas en
        // los identificadores, y el código no tiene por qué hacerlo.
        var fila = new Dictionary<string, object?>(lector.FieldCount, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lector.FieldCount; i++)
        {
            fila[lector.GetName(i)] = lector.IsDBNull(i) ? null : lector.GetValue(i);
        }

        return fila;
    }
}
