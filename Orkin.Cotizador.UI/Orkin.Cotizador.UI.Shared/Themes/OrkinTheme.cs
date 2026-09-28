using MudBlazor;

namespace Orkin.Cotizador.UI.Shared.Themes;

/// <summary>
/// Paleta de colores del Prototipo D. MudBlazor 9 usa <see cref="MudTheme"/>; esta clase centraliza
/// la paleta para que no aparezcan colores sueltos en cada componente.
/// </summary>
public static class OrkinTheme
{
    public static MudTheme Crear()
    {
        var primario = "#E31837";       // rojo corporativo Orkin
        var secundario = "#B7142D";     // rojo oscuro para acentos
        var fondo = "#FAFAFA";          // gris muy claro
        var superficie = "#FFFFFF";
        var texto = "#212121";
        var error = "#C62828";

        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = primario,
                Secondary = secundario,
                Background = fondo,
                Surface = superficie,
                AppbarBackground = primario,
                AppbarText = "#FFFFFF",
                DrawerBackground = superficie,
                DrawerText = texto,
                TextPrimary = texto,
                TextSecondary = "#616161",
                Error = error,
                ActionDefault = primario,
                ActionDisabled = "#BDBDBD",
                Divider = "#E0E0E0",
            },
            LayoutProperties = new LayoutProperties
            {
                AppbarHeight = "64px",
                DrawerWidthLeft = "260px",
                DefaultBorderRadius = "8px",
            },
            Typography = new Typography
            {
                Default = new DefaultTypography
                {
                    FontFamily = ["Roboto", "Helvetica", "Arial", "sans-serif"],
                    FontSize = "0.875rem",
                },
                H4 = new H4Typography
                {
                    FontSize = "1.75rem",
                    FontWeight = "600",
                },
            },
        };
    }
}
