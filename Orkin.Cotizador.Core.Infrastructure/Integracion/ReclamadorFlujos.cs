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
public sealed record FlujoReclamado(Guid ErpFlujoId, Guid ErpConexionId, DateTime? ProximaEjecucionUtc);

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
    /// Devuelve los flujos vencidos que esta instancia se adjudica, ya con la ventana siguiente
    /// escrita. Volver vacío es lo normal la mayor parte de las veces.
    /// </summary>
    public async Task<List<FlujoReclamado>> ReclamarAsync(
        int maximoPorVuelta,
        CancellationToken cancellationToken)
    {
        var ahora = DateTime.UtcNow;

        // Dos pasos y no uno, a propósito. Primero se leen los candidatos sin bloquear: si se
        // bloqueara todo el conjunto, una vuelta lenta frenaría a la otra instancia. Después cada
        // candidato se intenta tomar uno por uno, y el que otra instancia ya tomó simplemente no
        // devuelve filas.
        var candidatos = await _contexto.ErpFlujo
            .AsNoTracking()
            .Where(f => f.EsPausado == false
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
            // No lleva OUTPUT: no hace falta. Lo que se necesita —Id, conexión y ventana— ya se
            // sabe en C#, y un OUTPUT sin INTO devuelve un conjunto de resultados que
            // ExecuteNonQuery no puede digerir.
            var filas = await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE int.ErpFlujo WITH (UPDLOCK, READPAST)
                SET ProximaEjecucionUtc = {ventana}
                WHERE Id = {candidato.Id}
                  AND ProximaEjecucionUtc <= {ahora}
                  AND EsPausado = 0
                  AND Estado <> 1
                """, cancellationToken);

            if (filas == 1)
            {
                reclamado.Add(new FlujoReclamado(candidato.Id, candidato.ErpConexionId, ventana));
            }
            else
            {
                _log.LogDebug(
                    "El flujo {FlujoId} ya lo tomó otra instancia; esta sigue de largo.",
                    candidato.Id);
            }
        }

        return reclamado;
    }
}
