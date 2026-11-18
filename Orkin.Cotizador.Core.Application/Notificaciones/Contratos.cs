namespace Orkin.Cotizador.Core.Application.Notificaciones;

/// <summary>Pedido de alta en la cola. El <c>TipoCodigo</c> se resuelve a un catálogo real; no se acepta un Id suelto.</summary>
/// <param name="TipoCodigo">Código de <c>dbo.TipoNotificacionCatalogo</c>.</param>
/// <param name="Destinatario">Correo real del destinatario.</param>
/// <param name="Asunto">Asunto ya armado con datos del ERP.</param>
/// <param name="CuerpoHtml">Cuerpo HTML ya armado con datos del ERP.</param>
/// <param name="CotizacionId">Relación opcional con una cotización propia.</param>
/// <param name="ClaveNegocio">Clave determinista del origen; evita duplicados entre vueltas del job.</param>
public sealed record SolicitudNotificacion(
    string TipoCodigo,
    string Destinatario,
    string Asunto,
    string CuerpoHtml,
    Guid? CotizacionId = null,
    string? ClaveNegocio = null);

/// <summary>Mensaje que se entrega al proveedor de correo.</summary>
public sealed record CorreoSaliente(string Destinatario, string Asunto, string CuerpoHtml);

/// <summary>Resultado de un intento de envío. <see cref="Error"/> se propaga tal cual al guardar.</summary>
public sealed record ResultadoEnvioCorreo(bool Exito, string? IdExterno = null, string? Error = null)
{
    public static ResultadoEnvioCorreo Fallido(string error) => new(false, null, error);
}

/// <summary>Resultado de una vuelta del job. Los contadores alimentan <c>int.JobEjecucion</c>.</summary>
public sealed record ResultadoCola(int Encolados, int Enviados, int Fallidos, string? Mensaje = null);

/// <summary>Una fila de la bandeja de la campana.</summary>
public sealed record NotificacionResumen(
    Guid Id,
    string TipoCodigo,
    string TipoNombre,
    string Asunto,
    string Destinatario,
    string EstadoCodigo,
    DateTime? EnviadoUtc,
    DateTime? LeidoUtc,
    int Intentos,
    string? Error);

/// <summary>Bandeja completa: contador de no leídas para la campana y la lista reciente.</summary>
public sealed record BandejaNotificaciones(
    int NoLeidas,
    IReadOnlyList<NotificacionResumen> Recientes);

/// <summary>Resultado de revisar la definición y los permisos de un SP legacy antes de invocarlo.</summary>
public sealed record ResultadoValidacionSp(
    string Nombre,
    bool Valido,
    bool PuedeEjecutar,
    bool SoloLectura,
    string? Motivo);

/// <summary>Frontera del sistema con el servidor de correo. No sabe nada de la base ni de la cola.</summary>
public interface IProveedorCorreo
{
    Task<ResultadoEnvioCorreo> EnviarAsync(CorreoSaliente correo, CancellationToken cancellationToken = default);
}

/// <summary>Cola de correos, bandeja y recogida de avisos de los SPs legacy (Fase 8).</summary>
public interface INotificacionService
{
    /// <summary>Alta en la cola. Devuelve <c>null</c> si la clave de negocio ya estaba encolada.</summary>
    Task<Guid?> EncolarAsync(SolicitudNotificacion solicitud, CancellationToken cancellationToken = default);

    /// <summary>Recoge avisos de los SPs legacy de solo lectura. No escribe tablas del ERP.</summary>
    Task<ResultadoCola> RecogerLegacyAsync(CancellationToken cancellationToken = default);

    /// <summary>Intenta enviar los correos vencidos, con reintentos y auditoría.</summary>
    Task<ResultadoCola> ProcesarColaAsync(CancellationToken cancellationToken = default);

    /// <summary>Bandeja de la campana.</summary>
    Task<BandejaNotificaciones> ObtenerBandejaAsync(CancellationToken cancellationToken = default);

    /// <summary>Marca un aviso como leído. Devuelve false si no existe o ya estaba leído.</summary>
    Task<bool> MarcarLeidaAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marca todos los avisos enviados como leídos.</summary>
    Task<int> MarcarTodasLeidasAsync(CancellationToken cancellationToken = default);

    /// <summary>Vuelve a poner en cola un envío fallido. Lo dispara un usuario con CONFIGURAR.</summary>
    Task<bool> ReenviarAsync(Guid id, CancellationToken cancellationToken = default);
}
