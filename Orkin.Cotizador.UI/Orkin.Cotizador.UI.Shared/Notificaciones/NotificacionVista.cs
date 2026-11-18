namespace Orkin.Cotizador.UI.Shared.Notificaciones;

/// <summary>
/// Vista de la bandeja que comparten la campana del layout y la página <c>/notificaciones</c>.
/// <para>Vive en el proyecto compartido para que el badge y la bandeja lean exactamente el mismo
/// contrato. Dos DTO con los mismos campos en dos proyectos es la forma clásica de que un día el
/// badge cuente una cosa y la tabla muestre otra.</para>
/// </summary>
public sealed class BandejaNotificacionesVista
{
    public int NoLeidas { get; set; }
    public IReadOnlyList<NotificacionResumenVista> Recientes { get; set; } = [];
}

public sealed class NotificacionResumenVista
{
    public Guid Id { get; set; }
    public string TipoCodigo { get; set; } = string.Empty;
    public string TipoNombre { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string Destinatario { get; set; } = string.Empty;
    public string EstadoCodigo { get; set; } = string.Empty;
    public DateTime? EnviadoUtc { get; set; }
    public DateTime? LeidoUtc { get; set; }
    public int Intentos { get; set; }
    public string? Error { get; set; }
}

/// <summary>Envelope mínimo de la API para la campana; el mensaje se muestra tal cual si falla.</summary>
public sealed class RespuestaBandeja
{
    public bool Exito { get; set; }
    public string? Mensaje { get; set; }
    public string? Codigo { get; set; }
    public BandejaNotificacionesVista? Datos { get; set; }
}
