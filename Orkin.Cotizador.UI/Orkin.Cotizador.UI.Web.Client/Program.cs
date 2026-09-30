using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Orkin.Cotizador.UI.Web.Client.Services;

namespace Orkin.Cotizador.UI.Web.Client;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // La base address apunta a la API. En desarrollo se lee de wwwroot/appsettings.json;
        // en producción el BFF (Orkin.Cotizador.UI.Web) puede servir en el mismo origen.
        //
        // El FalloTransporteHandler va en el medio para que un fallo de red (API caída, CORS)
        // se vea como un mensaje de error en la pantalla y no como la página muerta de Blazor.
        // El InnerHandler es HttpClientHandler a propósito: en WebAssembly el runtime lo cambia
        // por el navegador real (BrowserHttpMessageHandler) en cuanto arranca.
        var api = builder.Configuration["ApiUrl"] ?? builder.HostEnvironment.BaseAddress;
        builder.Services.AddScoped(_ => new HttpClient(
            new FalloTransporteHandler(api) { InnerHandler = new HttpClientHandler() })
        {
            BaseAddress = new Uri(api)
        });

        builder.Services.AddMudServices();

        // ── Autorización ────────────────────────────────────────────────────────────
        // El nombre de una política es el código del permiso (CATALOGOS.ORIGENES, …), y en el
        // cliente no hay catálogo de permisos: se resuelve cualquier nombre contra el claim
        // "orkin:permiso" del token. Ver PoliticaPermisoProvider para por qué y para qué caso
        // falla.
        builder.Services.AddAuthorizationCore(options =>
        {
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PoliticaPermisoProvider>();
        builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ManejadorPermiso>();

        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<CatalogoService>();
        builder.Services.AddScoped<CotizacionService>();
        builder.Services.AddScoped<IntegracionService>();
        builder.Services.AddScoped<AdminService>();
        builder.Services.AddScoped<CustomAuthStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());

        await builder.Build().RunAsync();
    }
}
