using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Domain.Integracion;
using Orkin.Cotizador.Core.Infrastructure.Integracion;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Integracion;

/// <summary>
/// El motor de sincronización programada: el proceso que hace que la sincronización sea
/// «en vivo» sin que nadie le dé ejecutar.
/// <para>El bucle es deliberadamente tonto. Cada vuelta pide los flujos vencidos, se queda con
/// los que nadie más tomó, corre cada uno en su propio ámbito de inyección y se duerme. No lleva
/// cron, ni cola, ni <c>Timer</c>: la pregunta «¿qué toca?» la contesta la base, en
/// <c>ProximaEjecucionUtc</c>, y por eso agregar un flujo no pide reiniciar nada.</para>
/// <para><b>Por qué un ámbito por flujo y no uno compartido:</b> correr un flujo lee del ERP del
/// cliente y escribe en el catálogo. Si dos flujos compartieran un <c>CotizadorDbContext</c>, el
/// <c>ChangeTracker</c> del primero se mezclaría con las entidades del segundo y los
/// <c>SaveChanges</c> se pisarían. El ámbito es lo que aísla cada corrida; el precio es un
/// <c>DbContext</c> por flujo, y con dos o tres flujos eso no es un precio.</para>
/// </summary>
public sealed class SincronizacionBackgroundService : BackgroundService
{
    /// <summary>
    /// Cuántos flujos se toman por vuelta. Es un tope de seguridad, no una política: si alguien
    /// dejara 500 flujos programados de golpe, una vuelta no debe quedarse colgada horas. Lo que
    /// no se alcance en esta vuelta entra en la siguiente, y como a cada uno el reclamo ya le
    /// avanzó su ventana, nada se pierde ni se duplica.
    /// </summary>
    private const int MaximoFlujosPorVuelta = 25;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SincronizacionBackgroundService> _log;
    private readonly TimeProvider _reloj;

    public SincronizacionBackgroundService(
        IServiceScopeFactory scopes,
        ILogger<SincronizacionBackgroundService> log,
        TimeProvider reloj)
    {
        _scopes = scopes;
        _log = log;
        _reloj = reloj;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "Motor de sincronización arrancado. Despierta cada {Espera}s; cada flujo corre a su propio intervalo.",
            PlanificadorSincronizacion.EsperaEntreVueltasSegundos);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(PlanificadorSincronizacion.EsperaEntreVueltasSegundos),
            _reloj);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await EjecutarVueltaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Una vuelta que se cae no puede tumbar el motor. Si lo hiciera, el proceso
                // seguiría vivo (BackgroundService lo marca como fallido pero no muere) y ningún
                // flujo volvería a sincronizarse nunca, sin que nadie se entere: el síntoma sería
                // «los precios están viejos» y la causa estaría a horas de distancia.
                _log.LogError(ex, "La vuelta del motor de sincronización falló; se sigue.");
            }
        }
    }

    private async Task EjecutarVueltaAsync(CancellationToken cancellationToken)
    {
        using var alcanceLectura = _scopes.CreateScope();
        var reclamador = alcanceLectura.ServiceProvider.GetRequiredService<ReclamadorFlujos>();

        var flujos = await reclamador.ReclamarAsync(MaximoFlujosPorVuelta, cancellationToken);

        if (flujos.Count == 0)
        {
            return;
        }

        _log.LogDebug("Venta del motor: {Cantidad} flujo(s) vencido(s).", flujos.Count);

        // Uno por uno y secuencial. En paralelo el ganho no existe —el cuello de botella es la
        // latencia de red al ERP del cliente, no la CPU— y el coste sí: N conexiones simultáneas
        // contra la base de producción de un cliente es justo lo que no se le pide a un ERP.
        foreach (var flujo in flujos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EjecutarFlujoAsync(flujo, cancellationToken);
        }
    }

    private async Task EjecutarFlujoAsync(FlujoReclamado flujo, CancellationToken cancellationToken)
    {
        // Ámbito propio por flujo: es el que aísla el contexto de EF de esta corrida de todas las
        // demás, y el que hace que lo que escriba este flujo no aparezca en el ChangeTracker del
        // siguiente.
        using var alcance = _scopes.CreateScope();
        var integracion = alcance.ServiceProvider.GetRequiredService<IntegracionService>();
        var contexto = alcance.ServiceProvider.GetRequiredService<CotizadorDbContext>();
        var reclamador = alcance.ServiceProvider.GetRequiredService<ReclamadorFlujos>();

        try
        {
            var resultado = await integracion.EjecutarFlujoProgramadoAsync(flujo.ErpFlujoId, cancellationToken);

            // El nivel es Debug y no Information a propósito. A 10 segundos por flujo, Information
            // son 8.640 líneas por flujo y por día en el log: el log tiene que poder leerse, y un
            // log donde todo es ruido no dice nada cuando algo importante pase.
            _log.LogDebug(
                "Flujo {FlujoId} sincronizado: {Insertados} creados, {Actualizados} actualizados, {Omitidos} sin cambio.",
                flujo.ErpFlujoId, resultado.Insertados, resultado.Actualizados, resultado.Omitidos);

            await RevisarPausaAsync(contexto, flujo, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Se está apagando el proceso. No es un fallo del flujo y no debe pausarse ni anotarse:
            // la corrida quedó a medias, no falló.
            throw;
        }
        catch (Exception ex)
        {
            // El fallo ya quedó anotado por IntegracionService en la bitácora. Aquí solo se decide
            // si el motor deja de insistir.
            _log.LogWarning(
                ex, "El flujo {FlujoId} falló en la vuelta programada: {Mensaje}", flujo.ErpFlujoId, ex.Message);

            await RevisarPausaAsync(contexto, flujo, cancellationToken);
        }
        finally
        {
            // El alquiler se suelta pase lo que pase, y con `CancellationToken.None` a propósito:
            // si se cancela justo al terminar, soltar con el token del apagado sería saltarse el
            // `finally` más interno y dejar el flujo marcado. While no se suelte, el flujo no se
            // vuelve a reclamar —y el último en soltar es el que gana, no el que llegó más tarde—,
            // así que la marca tiene que caerse siempre.
            await reclamador.LiberarCorridaAsync(flujo.ErpFlujoId, flujo.MarcaAlquiler, CancellationToken.None);
        }
    }

    /// <summary>
    /// Mira los fallos seguidos del flujo y lo pausa si llegó al corte.
    /// <para>No toca la ventana de la próxima corrida: esa ya la dejó escrita el reclamo, antes
    /// de que la carga empezara, y tocarla aquí abriría la puerta a que dos instancias se
    /// adjudiquen el mismo flujo. Al pausar sí se borra, porque un flujo pausado no tiene próxima
    /// corrida que esperar.</para>
    /// </summary>
    private async Task RevisarPausaAsync(
        CotizadorDbContext contexto,
        FlujoReclamado flujo,
        CancellationToken cancellationToken)
    {
        // Una sola consulta, con seguimiento, porque el estado que decide la pausa tiene que ser
        // el que está en la base. `IntegracionService` acaba de guardar y este es el mismo
        // contexto, así que está limpio y la lectura ve lo que se acaba de escribir.
        //
        // No se decide con el `fallo` que trae este método porque ese dato ya está contabilizado
        // en la fila: `IntegracionService` suma un fallo si la carga falló y lo vuelve a cero si
        // terminó bien. Preguntarle además «¿falló esta vez?» sería tener la respuesta en dos
        // lugares, y basta con que uno de los dos se olvide de actualizar para que se pause un
        // flujo sano.
        var paraPausar = await contexto.ErpFlujo
            .FirstOrDefaultAsync(f => f.Id == flujo.ErpFlujoId, cancellationToken);

        if (paraPausar is null || !PlanificadorSincronizacion.DebePausar(
                paraPausar.FallosConsecutivos, paraPausar.MaximoFallosConsecutivos))
        {
            // Todavía no se llegó al corte. No se escribe nada: hacerlo sería un SaveChanges
            // vacío cada 5 segundos por flujo, que es la forma más cara de no hacer nada.
            return;
        }

        paraPausar.EsPausado = true;
        paraPausar.ProximaEjecucionUtc = null;
        paraPausar.MotivoPausa = PlanificadorSincronizacion.MotivoPausaPorFallos(
            paraPausar.FallosConsecutivos, paraPausar.MaximoFallosConsecutivos);
        paraPausar.UltimoError = paraPausar.MotivoPausa;

        await contexto.SaveChangesAsync(cancellationToken);

        _log.LogError(
            "El flujo {FlujoId} quedó pausado tras {Fallos} corridas fallidas seguidas. " +
            "Hay que revisarlo y reanudarlo a mano desde la pantalla de orígenes.",
            flujo.ErpFlujoId, paraPausar.FallosConsecutivos);
    }
}
