using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Estado de autenticación de la aplicación. Mantiene el token en <c>localStorage</c> y notifica a
/// los componentes cuando cambia.
/// </summary>
public sealed class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly AuthService _authService;

    private ClaimsPrincipal _anonimo = new(new ClaimsIdentity());

    public CustomAuthStateProvider(HttpClient http, IJSRuntime js, AuthService authService)
    {
        _http = http;
        _js = js;
        _authService = authService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _authService.GetTokenAsync();

        if (string.IsNullOrWhiteSpace(token))
        {
            return new AuthenticationState(_anonimo);
        }

        // Un token corrupto o malformado no debe matar la aplicación: se trata como si
        // no hubiera sesión, y el siguiente paso (el 401 de la API o la redirección al
        // login) hace el resto.
        try
        {
            var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(_anonimo);
        }
    }

    public async Task MarkUserAsAuthenticated(string token, bool recordar = true)
    {
        await _authService.SetTokenAsync(token, recordar);
        var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))));
    }

    public async Task MarkUserAsLoggedOut()
    {
        await _authService.RemoveTokenAsync();
        _http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonimo)));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var partes = jwt.Split('.');

        // Un token que no es un JWT de tres partes no tiene claims que leer. Sin esta guarda,
        // un token corrupto en localStorage revienta con IndexOutOfRangeException en el Split
        // y la app entera muere en el ErrorBoundary antes de que el 401 llegue a redirigir.
        if (partes.Length < 2)
        {
            return [];
        }

        var payload = partes[1];

        var jsonBytes = Convert.FromBase64String(PadBase64(payload));
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes);

        if (keyValuePairs is null)
        {
            return [];
        }

        var claims = new List<Claim>();

        foreach (var (clave, valor) in keyValuePairs)
        {
            switch (valor.ValueKind)
            {
                // Un valor simple (string, número, bool) → un claim
                case JsonValueKind.String:
                    claims.Add(new Claim(clave, valor.GetString() ?? string.Empty));
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    claims.Add(new Claim(clave, valor.ToString()));
                    break;

                // Un array (como "orkin:permiso": ["A","B","C"]) → un claim por elemento,
                // para que FindAll("orkin:permiso") funcione como en el servidor.
                case JsonValueKind.Array:
                    foreach (var elemento in valor.EnumerateArray())
                    {
                        if (elemento.ValueKind == JsonValueKind.String)
                        {
                            claims.Add(new Claim(clave, elemento.GetString() ?? string.Empty));
                        }
                    }
                    break;
            }
        }

        return claims;
    }

    private static string PadBase64(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: return base64 + "==";
            case 3: return base64 + "=";
            default: return base64;
        }
    }
}
