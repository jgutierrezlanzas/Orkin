using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orkin.Cotizador.Core.Application.Notificaciones;

/// <summary>
/// Job de notificaciones: recoge avisos de los SPs legacy y drena la cola de correo.
/// <para><b>Apagado por defecto.</b> Sin <c>Notificaciones__HabilitadoJob=true</c> el servicio
/// arranca, dice una vez por qué no va a hacer nada y se queda quieto. Encender un job que envía
/// correo a destinatarios reales no puede ser efecto de desplegar.</para>
/// <para>Un ámbito por vuelta, igual que el motor de sincronización: el <c>DbContext</c> del job no
/// debe compartir ChangeTracker con una petición ni con la vuelta siguiente.</para>
/// </summary>
public sealed class NotificacionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _reloj;
    private readonly NotificacionesOpciones _opciones;
    private readonly ILogger<NotificacionBackgroundService> _log;

    public NotificacionBackgroundService(
        IServiceScopeFactory scopes,
        TimeProvider reloj,
        IOptions<NotificacionesOpciones> opciones,
        ILogger<NotificacionBackgroundService> log)
    {
        _scopes = scopes;
        _reloj = reloj;
        _opciones = opciones.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.HabilitadoJob)
        {
            _log.LogInformation(
                "Job de notificaciones deshabilitado (Notificaciones__HabilitadoJob=false). " +
                "La cola y la bandeja siguen disponibles por API; no se envía ni se recoge nada.");
            return;
        }

        var intervalo = TimeSpan.FromSeconds(Math.Clamp(_opciones.IntervaloSegundos, 5, 3600));

        using var timer = new PeriodicTimer(intervalo, _reloj);

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
                // Una vuelta fallida no puede tumbar el job: al día siguiente tiene que seguir
                // intentando, y el error ya quedó en int.JobEjecucion cuando vino del servicio.
                _log.LogError(ex, "La vuelta del job de notificaciones falló; se sigue.");
            }
        }
    }

    private async Task EjecutarVueltaAsync(CancellationToken cancellationToken)
    {
        using (var alcanceRecogida = _scopes.CreateScope())
        {
            var servicio = alcanceRecogida.ServiceProvider.GetRequiredService<INotificacionService>();
            var recogida = await servicio.RecogerLegacyAsync(cancellationToken);

            if (recogida.Encolados > 0)
            {
                _log.LogInformation("Avisos legacy encolados: {Cantidad}.", recogida.Encolados);
            }
        }

        using (var alcanceEnvio = _scopes.CreateScope())
        {
            var servicio = alcanceEnvio.ServiceProvider.GetRequiredService<INotificacionService>();
            var envio = await servicio.ProcesarColaAsync(cancellationToken);

            if (envio.Enviados > 0 || envio.Fallidos > 0)
            {
                _log.LogInformation(
                    "Cola de correo: {Enviados} enviado(s), {Fallidos} fallido(s).",
                    envio.Enviados, envio.Fallidos);
            }
        }
    }
}
