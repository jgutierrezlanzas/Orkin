namespace Orkin.Cotizador.UI.Web.Client.Models;

/// <summary>
/// Colores de los calendarios por técnico. Si el técnico no tiene <c>ColorCalendario</c>
/// configurado en el catálogo compartido, se deriva uno determinístico de su código: así el mismo
/// técnico conserva siempre el mismo color sin necesidad de tablas ni JSON adicionales (D-21).
/// </summary>
public static class ColorCalendario
{
    /// <summary>Paleta legible sobre fondo claro, con el rojo corporativo como primer color.</summary>
    public static readonly string[] Paleta =
    [
        "#E31837", "#1976D2", "#00897B", "#F9A825", "#7B1FA2",
        "#388E3C", "#D84315", "#455A64", "#C2185B", "#5D4037"
    ];

    public static string Para(string? codigo, string? configurado = null)
    {
        if (!string.IsNullOrWhiteSpace(configurado) && configurado.StartsWith('#') && configurado.Length == 7)
        {
            return configurado.ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Paleta[0];
        }

        var hash = 0;
        foreach (var c in codigo.Trim().ToUpperInvariant())
        {
            hash = (hash * 31 + c) & 0x7FFFFFFF;
        }

        return Paleta[hash % Paleta.Length];
    }
}
