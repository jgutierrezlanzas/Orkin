namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Proyecciones de solo lectura sobre las vistas <c>vv*</c> del ERP. No copian catálogos (D-20):
/// solo permiten validar, desde la API, los códigos que el WinForms validaba contra esas mismas
/// vistas. Se mapean <c>HasNoKey</c> y <c>ExcludeFromMigrations</c>: EF las consulta y nunca las
/// crea ni altera.
/// </summary>

public class VistaClienteErp
{
    public string Cliente { get; set; } = null!;
    public string? NombreCliente { get; set; }
    public string? Activo { get; set; }
}

public class VistaLocacionErp
{
    public string Codigo { get; set; } = null!;
    public string? Cliente { get; set; }
    public string? Activo { get; set; }
}

public class VistaTipoServicioErp
{
    public string Codigo { get; set; } = null!;
    public string? Activo { get; set; }
}

public class VistaVehiculoErp
{
    public string Codigo { get; set; } = null!;
    public string? Activo { get; set; }
}

public class VistaTecnicoErp
{
    public string Codigo { get; set; } = null!;
    public string? Iniciales { get; set; }
    public string? NombreCompleto { get; set; }
    public string? Activo { get; set; }
}
