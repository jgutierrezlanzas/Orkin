namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>
/// Modelos del visor de reportes (Fase 7). El catálogo y las firmas vienen de la API; la web no
/// conoce los nombres de los SPs salvo lo que el propio catálogo le devuelve para mostrar.
/// </summary>

public sealed class ReporteParametroModel
{
    public string Clave { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public bool Requerido { get; set; }
    public string? ValorPorDefecto { get; set; }
}

public sealed class ReporteColumnaModel
{
    public string Clave { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
}

public sealed class ReporteDefinicionModel
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public IReadOnlyList<ReporteParametroModel> Parametros { get; set; } = [];
    public IReadOnlyList<ReporteColumnaModel> Columnas { get; set; } = [];
    public bool Landscape { get; set; }
}

public sealed class FirmaReporteModel
{
    public string Nombre { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public int Orden { get; set; }
}

/// <summary>
/// Respuesta de <c>GET api/reportes/{clave}/datos</c>. Las filas son diccionarios porque el SP
/// legacy decide las columnas: el visor pinta exactamente lo que vinieron, con sus códigos y sus
/// descripciones del ERP (D-20).
/// </summary>
public sealed class ReporteDatosModel
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public IReadOnlyList<ReporteColumnaModel> Columnas { get; set; } = [];
    public IReadOnlyList<Dictionary<string, object?>> Filas { get; set; } = [];
    public int Total { get; set; }
}
