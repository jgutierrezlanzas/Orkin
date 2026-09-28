using System.Net.Http.Json;
using Microsoft.JSInterop;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Acceso a los endpoints de autenticación de la API. El token se guarda en <c>localStorage</c> para
/// sobrevivir recargas de página.
/// </summary>
public sealed class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private const string TokenKey = "orkin_cotizador_token";

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task<string?> GetTokenAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);

    public async Task SetTokenAsync(string token)
        => await _js.InvokeAsync<string>("localStorage.setItem", TokenKey, token);

    public async Task RemoveTokenAsync()
        => await _js.InvokeAsync<string>("localStorage.removeItem", TokenKey);

    public async Task<ApiResponse<LoginResponse>?> LoginAsync(LoginRequest request)
        => await _http.PostAsJsonAsync("api/auth/ingresar", request)
            .ContinueWith(t => t.Result.IsSuccessStatusCode
                ? t.Result.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>().Result
                : null);

    public async Task<bool> LogoutAsync()
    {
        try
        {
            await _http.PostAsync("api/auth/salir", null);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
