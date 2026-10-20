using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Domain.Integracion;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Infrastructure.Integracion;

/// <summary>
/// Un flujo que esta instancia del motor se adjudicationó: ya es suyo, nadie más lo va a correr.
/// </summary>
/// <param name="ErpFlujoId">El flujo reclamado.</param>
/// <param name="ErpConexionId">Su conexión, para no tener que volver a buscarla.</param>
/// <param name="ProximaEjecucionUtc">
/// La ventana siguiente que quedó escrita al reclamar. Ya es responsabilidad de esta instancia
/// terminarla bien: si la corrida se cae a mitad, el flujo no debe quedarse vencido para siempre.
/// </param>
/// <param name="MarcaAlquiler">
/// La marca de <c>CorridaEnCursoUtc</c> que se escribió al reclamar. Hay que devolverla tal cual
/// al soltar el alquiler: liberar «poniendo null» sin comprobarla sería capaz de borrar la marca
/// de una corrida que se tomó después.
/// </param>
public sealed record FlujoReclamado(
    Guid ErpFlujoId,
    Guid ErpConexionId,
    DateTime? ProximaEjecucionUtc,
    DateTime MarcaAlquiler);

/// <summary>
/// Reclama los flujos vencidos de forma atómica.
/// <para>Es la pieza que hace que el motor sea seguro de correr en más de una instancia —dos
/// réplicas de la API, o una API y un proceso aparte— sin que un flujo se cargue dos veces a la
/// vez. «¿te toca?» y «te tomo» tienen que ser la misma operación: separadas, dos instancias
/// leen la misma fila vencida y las dos la corren.</para>
/// </summary>
public sealed class ReclamadorFlujos
{
    private readonly CotizadorDbContext _contexto;
    private readonly ILogger<ReclamadorFlujos> _log;

    public ReclamadorFlujos(CotizadorDbContext contexto, ILogger<ReclamadorFlujos> log)
    {
        _contexto = contexto;
        _log = log;
    }

    /// <summary>
    /// Recorta el instante a milisegundos, que es lo que guarda la columna
    /// <c>datetime2(3)</c>.
    /// <para>Sin esto la marca de la corrida en curso nunca se libera. Se escribe
    /// <c>DateTime.UtcNow</c> (con 7 decimales de fracción de segundo) en una columna que solo
    /// guarda 3; al soltar el alquiler se compara por igualdad exacta y nunca coincide, así que
    /// el flujo queda tomado durante los 15 minutos completos después de cada corrida. El
    /// síntoma es un flujo que se sincroniza cada 10 segundos y que además dice «sincronizando…»
    /// en la pantalla, sin que haya ninguna corrida en marcha.</para>
    /// </summary>
    private static DateTime TruncarAMilisegundos(DateTime fecha)
        => new(fecha.Ticks - fecha.Ticks % 10000, fecha.Kind);

    /// <summary>
    /// Devuelve los flujos vencidos que esta instancia se adjudica, ya con la ventana siguiente
    /// escrita. Volver vacío es lo normal la mayor parte de las veces.
    /// </summary>
    public async Task<List<FlujoReclamado>> ReclamarAsync(
        int maximoPorVuelta,
        CancellationToken cancellationToken)
    {
        var ahora = TruncarAMilisegundos(DateTime.UtcNow);
        var caducidad = ahora - PlanificadorSincronizacion.ToleranciaCorridaEnCurso;

        // Dos pasos y no uno, a propósito. Primero se leen los candidatos sin bloquear: si se
        // bloqueara todo el conjunto, una vuelta lenta frenaría a la otra instancia. Después cada
        // candidato se intenta tomar uno por uno, y el que otra instancia ya tomó simplemente no
        // devuelve filas.
        var candidatos = await _contexto.ErpFlujo
            .AsNoTracking()
            .Where(f => f.IsActive
                     && f.EsPausado == false
                     // `<>` BORRADOR y no `==` ACTIVO: un flujo que falló queda en CON_ERROR y
                     // tiene que seguir siendo reintentado, o un ERP que se cae un minuto no
                     // vuelve solo. Ver EsEjecutable, que explica el mismo criterio.
                     && f.Estado != IntegracionCatalogos.EstadoFlujoBorrador
                     && f.IntervaloSegundos != null
                     && f.TipoDisparador != IntegracionCatalogos.TipoDisparadorManual
                     && f.ProximaEjecucionUtc != null
                     && f.ProximaEjecucionUtc <= ahora)
            .OrderBy(f => f.ProximaEjecucionUtc)
            .Select(f => new { f.Id, f.ErpConexionId, f.IntervaloSegundos })
            .Take(maximoPorVuelta)
            .ToListAsync(cancellationToken);

        if (candidatos.Count == 0)
        {
            return [];
        }

        var reclamado = new List<FlujoReclamado>(candidatos.Count);

        foreach (var candidato in candidatos)
        {
            var ventana = PlanificadorSincronizacion.CalcularProximaEjecucion(ahora, candidato.IntervaloSegundos!.Value);

            // UPDLOCK + READPAST: «actualiza esta fila y sáltate la que esté bloqueada». Es el
            // patrón de cola de SQL Server, y READPAST es lo que hace de esto un arbitraje en vez
            // de una espera: si otra instancia tiene esa fila bloqueada, esta sentencia no espera,
            // no la toca y devuelve 0.
            //
            // El WHERE repite la condición de vencido aunque ya se haya filtrado en la consulta de
            // candidatos. Es lo que cierra la ventana entre la lectura y el UPDATE: si alguien la
            // reclamó mientras tanto, el UPDATE no hace nada. Ese es el arbitraje; sin el WHERE
            // repetido, las dos instancias se adjudicarían el mismo flujo.
            //
            // La condición del alquiler va aquí y no solo en el reclamo a mano, porque son las dos
            // puertas de entrada al mismo cuarto. Un flujo con la corrida a mano en curso sigue
            // teniendo su ventana vencida —la corrida a mano no la movió si aún no le tocaba— y
            // sin esta línea el motor lo reclamaría y las dos cargas escribirían las mismas filas
            // a la vez.
            //
            // No lleva OUTPUT: no hace falta. Lo que se necesita —Id, conexión, ventana y marca— ya
            // se sabe en C#, y un OUTPUT sin INTO devuelve un conjunto de resultados que
            // ExecuteNonQuery no puede digerir.
            var filas = await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE int.ErpFlujo WITH (UPDLOCK, READPAST)
                SET ProximaEjecucionUtc = {ventana},
                    CorridaEnCursoUtc = {ahora}
                WHERE Id = {candidato.Id}
                  AND ProximaEjecucionUtc <= {ahora}
                  AND EsPausado = 0
                  AND Estado <> 1
                  AND IsActive = 1
                  AND (CorridaEnCursoUtc IS NULL OR CorridaEnCursoUtc < {caducidad})
                """, cancellationToken);

            if (filas == 1)
            {
                reclamado.Add(new FlujoReclamado(candidato.Id, candidato.ErpConexionId, ventana, ahora));
            }
            else
            {
                _log.LogDebug(
                    "El flujo {FlujoId} ya lo tomó otra instancia, o tiene una corrida en curso; esta sigue de largo.",
                    candidato.Id);
            }
        }

        return reclamado;
    }

    /// <summary>
    /// Toma el alquiler de corrida de un flujo para correrlo a mano, sin moverle la ventana.
    /// <para>Es el hermano de <see cref="ReclamarAsync"/> para el otro camino de entrada: correr un
    /// flujo programado desde la pantalla. La diferencia —que aquí la ventana no se toca, porque no
    /// le tocaba todavía— es justamente el motivo de que haga falta el alquiler: sin él, ese
    /// «Ejecutar» y la corrida programada que se solapa se escriben encima.</para>
    /// <para>Un flujo manual también se puede reservar, y no estorba a nadie: no tiene ventana que
    /// mover y ningún motor lo reclama. Reservarlo evita que dos personas pulsen «Ejecutar» a la
    /// vez sobre el mismo flujo, que es la misma carrera con menos ruido para el usuario.</para>
    /// </summary>
    /// <returns>
    /// La marca de la reserva, o <c>null</c> si alguien más tiene el flujo. El valor devuelto hay
    /// que pasarlo tal cual a <see cref="LiberarCorridaAsync"/> al terminar.
    /// </returns>
    public async Task<DateTime?> ReservarCorridaManualAsync(
        Guid erpFlujoId,
        CancellationToken cancellationToken)
    {
        var ahora = TruncarAMilisegundos(DateTime.UtcNow);
        var caducidad = ahora - PlanificadorSincronizacion.ToleranciaCorridaEnCurso;

        // Sin `ProximaEjecucionUtc` en el SET a propósito: la ventana no le tocaba a una corrida
        // a mano, y adelantarla sería saltarse una vuelta de la programación. Se deja el
        // calendario del motor exactamente como estaba.
        var filas = await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE int.ErpFlujo WITH (UPDLOCK, READPAST)
            SET CorridaEnCursoUtc = {ahora}
            WHERE Id = {erpFlujoId}
              AND IsActive = 1
              AND Estado <> 1
              AND (CorridaEnCursoUtc IS NULL OR CorridaEnCursoUtc < {caducidad})
            """, cancellationToken);

        return filas == 1 ? ahora : null;
    }

    /// <summary>
    /// Devuelve el alquiler dejando el flujo como estaba.
    /// <para>Se comprueba la marca en el WHERE por una razón concreta: si la corrida tardó más que
    /// <see cref="PlanificadorSincronizacion.ToleranciaCorridaEnCurso"/>, el flujo ya pudo tomarlo otro proceso, y soltar a
    /// ciegas le borraría la marca al que está corriendo ahora —volviendo a dejarlo libre para un
    /// tercero, y perdiendo la única señal de que hay una carga en marcha. Con la marca en el
    /// WHERE, un liberate que llega tarde no hace nada, que es justo lo que corresponde.</para>
    /// <para>No propaga fallos: liberar el alquiler es housekeeping, y que falle no debe convertir
    /// una carga exitosa en un error. La marca se caduca sola.</para>
    /// </summary>
    public async Task LiberarCorridaAsync(
        Guid erpFlujoId,
        DateTime marcaAlquiler,
        CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE int.ErpFlujo
                SET CorridaEnCursoUtc = NULL
                WHERE Id = {erpFlujoId}
                  AND CorridaEnCursoUtc = {marcaAlquiler}
                """, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(
                ex, "No se pudo liberar el alquiler de corrida del flujo {FlujoId}; se caducará solo.", erpFlujoId);
        }
    }
}
