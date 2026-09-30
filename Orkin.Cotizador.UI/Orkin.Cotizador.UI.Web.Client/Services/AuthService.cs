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

    /// <summary>
    /// El token, del almacén donde esté. Se miran los dos porque el usuario pudo entrar con
    /// «recordarme» (localStorage) o sin él (sessionStorage), y la app al arrancar no sabe cuál
    /// fue —tiene que encontrar la sesión donde la haya dejado.
    /// </summary>
    public async Task<string?> GetTokenAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey)
           ?? await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);

    /// <summary>
    /// Guarda el token. Con <paramref name="recordar"/> va a <c>localStorage</c> y la sesión
    /// sobrevive al cierre del navegador; sin él, a <c>sessionStorage</c>, que muere con la
    /// pestaña. Es todo lo que «recordarme» promete: el token dura lo mismo en ambos casos, y
    /// no se alarga la vida del token porque la revocación vive en memoria de la API y un token
    /// largo que no se puede revocar tras un reinicio es una puerta abierta.
    /// </summary>
    public async Task SetTokenAsync(string token, bool recordar = true)
        => await _js.InvokeVoidAsync(
            recordar ? "localStorage.setItem" : "sessionStorage.setItem",
            TokenKey, token);

    /// <summary>Quita el token de donde esté. Se limpian los dos almacenes para que cerrar
    /// sesión cierre sesión, sin importar cómo se entró.</summary>
    public async Task RemoveTokenAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
    }

    public async Task<ApiResponse<LoginResponse>?> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
    }

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

    public async Task<ApiResponse<CambioContrasenaResponse>?> CambiarContrasenaAsync(CambioContrasenaRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/cambiar-contrasena", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<CambioContrasenaResponse>>();
    }
}