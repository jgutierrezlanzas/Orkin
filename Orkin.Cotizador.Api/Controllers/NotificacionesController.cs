using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Orkin.Cotizador.Core.Application.Notificaciones;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Bandeja de la campana y control de la cola de correo (Fase 8).
/// <para>La configuración que se devuelve está sanada: indica si hay credenciales, pero nunca las
/// devuelve. Un endpoint de configuración que filtrara la clave del SMTP sería una puerta abierta
/// para cualquiera con el permiso de ver la bandeja.</para>
/// </summary>
[ApiController]
[Route("api/notificaciones")]
public sealed class NotificacionesController : ControllerBase
{
    private readonly INotificacionService _notificaciones;
    private readonly CorreoOpciones _correo;
    private readonly NotificacionesOpciones _opciones;

    public NotificacionesController(
        INotificacionService notificaciones,
        IOptions<CorreoOpciones> correo,
        IOptions<NotificacionesOpciones> opciones)
    {
        _notificaciones = notificaciones;
        _correo = correo.Value;
        _opciones = opciones.Value;
    }

    /// <summary>Bandeja que alimenta la campana del sidebar.</summary>
    [HttpGet("bandeja")]
    [Authorize("NOTIFICACIONES.VER")]
    public async Task<IResult> Bandeja(CancellationToken cancellationToken)
    {
        var bandeja = await _notificaciones.ObtenerBandejaAsync(cancellationToken);
        return Results.Ok(ApiResponse<BandejaNotificaciones>.Ok(bandeja));
    }

    /// <summary>Estado de la configuración, sin secretos. Solo para quien puede configurar.</summary>
    [HttpGet("configuracion")]
    [Authorize("NOTIFICACIONES.CONFIGURAR")]
    public IResult Configuracion() =>
        Results.Ok(ApiResponse<object>.Ok(new
        {
            correoHabilitado = _correo.Habilitado,
            servidor = _correo.Servidor,
            puerto = _correo.Puerto,
            usarSsl = _correo.UsarSsl,
            de = _correo.De,
            nombreDe = _correo.NombreDe,
            tieneCredenciales = !string.IsNullOrWhiteSpace(_correo.Usuario),
            jobHabilitado = _opciones.HabilitadoJob,
            intervaloSegundos = _opciones.IntervaloSegundos,
            maximoIntentos = _opciones.MaximoIntentos,
            backoffBaseSegundos = _opciones.BackoffBaseSegundos,
            validarDefinicionesSp = _opciones.ValidarDefinicionesSp,
            correoInterno = _opciones.CorreoInterno
        }));

    /// <summary>Recoge avisos de los SPs legacy. Operación manual; el job la hace sola.</summary>
    [HttpPost("recoger")]
    [Authorize("NOTIFICACIONES.CONFIGURAR")]
    public async Task<IResult> Recoger(CancellationToken cancellationToken)
    {
        var resultado = await _notificaciones.RecogerLegacyAsync(cancellationToken);
        return Results.Ok(ApiResponse<ResultadoCola>.Ok(resultado,
            $"{resultado.Encolados} aviso(s) encolado(s)."));
    }

    /// <summary>Drena la cola ahora, sin esperar al job.</summary>
    [HttpPost("procesar")]
    [Authorize("NOTIFICACIONES.CONFIGURAR")]
    public async Task<IResult> Procesar(CancellationToken cancellationToken)
    {
        var resultado = await _notificaciones.ProcesarColaAsync(cancellationToken);
        return Results.Ok(ApiResponse<ResultadoCola>.Ok(resultado,
            $"{resultado.Enviados} enviado(s), {resultado.Fallidos} fallido(s)."));
    }

    /// <summary>Marca un aviso como leído al abrirlo en la campana.</summary>
    [HttpPost("{id:guid}/leida")]
    [Authorize("NOTIFICACIONES.VER")]
    public async Task<IResult> MarcarLeida(Guid id, CancellationToken cancellationToken)
    {
        var marcada = await _notificaciones.MarcarLeidaAsync(id, cancellationToken);

        return marcada
            ? Results.Ok(ApiResponse<object>.SinContenido())
            : Results.NotFound(ApiResponse<object>.Error(
                "NOTIFICACION_NO_ENCONTRADA", "El aviso no existe o ya estaba leído."));
    }

    /// <summary>Marca todos los avisos enviados como leídos.</summary>
    [HttpPost("leidas-todas")]
    [Authorize("NOTIFICACIONES.VER")]
    public async Task<IResult> MarcarTodas(CancellationToken cancellationToken)
    {
        var cantidad = await _notificaciones.MarcarTodasLeidasAsync(cancellationToken);
        return Results.Ok(ApiResponse<object>.Ok(new { marcadas = cantidad }));
    }

    /// <summary>Vuelve a poner en cola un envío fallido. Requiere confirmación en la UI.</summary>
    [HttpPost("{id:guid}/reenviar")]
    [Authorize("NOTIFICACIONES.CONFIGURAR")]
    public async Task<IResult> Reenviar(Guid id, CancellationToken cancellationToken)
    {
        var reenviada = await _notificaciones.ReenviarAsync(id, cancellationToken);

        return reenviada
            ? Results.Ok(ApiResponse<object>.SinContenido())
            : Results.NotFound(ApiResponse<object>.Error(
                "NOTIFICACION_NO_ENCONTRADA", "El aviso no existe."));
    }
}
