namespace Orkin.Cotizador.UI.Web.Client.Models;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class CambioContrasenaRequest
{
    public string ContrasenaActual { get; set; } = string.Empty;
    public string ContrasenaNueva { get; set; } = string.Empty;
}

public sealed class CambioContrasenaResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraUtc { get; set; }
}

public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraUtc { get; set; }
}

public sealed class ApiResponse<T>
{
    public bool Exito { get; set; }
    public string? Mensaje { get; set; }
    public string? Codigo { get; set; }
    public T? Datos { get; set; }
    public List<ErrorCampo>? Errores { get; set; }

    public sealed class ErrorCampo
    {
        public string Campo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }
}
