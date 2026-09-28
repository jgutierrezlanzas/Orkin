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
        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri(builder.Configuration["ApiUrl"] ?? builder.HostEnvironment.BaseAddress)
        });

        builder.Services.AddMudServices();
        builder.Services.AddAuthorizationCore();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<CatalogoService>();
        builder.Services.AddScoped<CotizacionService>();
        builder.Services.AddScoped<CustomAuthStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());

        await builder.Build().RunAsync();
    }
}
