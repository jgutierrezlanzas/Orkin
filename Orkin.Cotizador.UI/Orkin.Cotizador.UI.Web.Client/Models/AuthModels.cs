namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>
/// Lo que se manda a <c>POST /api/auth/login</c>. Los nombres coinciden con el DTO de la API
/// (<c>SolicitudIngreso</c>): <c>Credencial</c> acepta correo o nombre de cuenta.
/// </summary>
public sealed class LoginRequest
{
    public string Credencial { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    public bool Recordar { get; set; }
}

public sealed class DatosUsuarioModel
{
    public Guid Id { get; set; }
    public string NombreCuenta { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
}

/// <summary>
/// Lo que devuelve un ingreso exitoso. Los nombres coinciden con el DTO de la API
/// (<c>RespuestaIngreso</c>).
/// </summary>
public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraUtc { get; set; }
    public DatosUsuarioModel Usuario { get; set; } = new();
    public IReadOnlyList<string> Permisos { get; set; } = [];
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