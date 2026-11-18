namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>Resultado de una vuelta manual de la cola (recoger o procesar).</summary>
public sealed class ResultadoColaVista
{
    public int Encolados { get; set; }
    public int Enviados { get; set; }
    public int Fallidos { get; set; }
    public string? Mensaje { get; set; }
}

/// <summary>
/// Estado del SMTP que devuelve la API. La clave nunca viaja: solo se dice si existe.
/// </summary>
public sealed class NotificacionConfiguracionVista
{
    public bool CorreoHabilitado { get; set; }
    public string Servidor { get; set; } = string.Empty;
    public int Puerto { get; set; }
    public bool UsarSsl { get; set; }
    public string De { get; set; } = string.Empty;
    public string NombreDe { get; set; } = string.Empty;
    public bool TieneCredenciales { get; set; }
    public bool JobHabilitado { get; set; }
    public int IntervaloSegundos { get; set; }
    public int MaximoIntentos { get; set; }
    public int BackoffBaseSegundos { get; set; }
    public bool ValidarDefinicionesSp { get; set; }
    public string CorreoInterno { get; set; } = string.Empty;
}
