using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>
/// Acceso a los endpoints de administración (usuarios, roles, permisos, parámetros) y a la
/// bitácora de auditoría.
/// </summary>
public sealed class AdminService
{
    private readonly HttpClient _http;

    public AdminService(HttpClient http)
    {
        _http = http;
    }

    // ── Usuarios ────────────────────────────────────────────────────────────

    public async Task<List<UsuarioModel>> ListarUsuariosAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<UsuarioModel>>>("api/usuarios");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<UsuarioModel>?> CrearUsuarioAsync(UsuarioModel modelo, string contrasena)
    {
        var request = new
        {
            NombreCuenta = modelo.NombreCuenta,
            modelo.NombreCompleto,
            modelo.Correo,
            Contrasena = contrasena,
            modelo.Documento,
            modelo.Cargo,
            modelo.EsActivo,
            Roles = modelo.Roles
        };
        var response = await _http.PostAsJsonAsync("api/usuarios", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<UsuarioModel>>();
    }

    public async Task<ApiResponse<UsuarioModel>?> ActualizarUsuarioAsync(UsuarioModel modelo)
    {
        var request = new
        {
            modelo.NombreCompleto,
            modelo.Correo,
            modelo.Documento,
            modelo.Cargo,
            modelo.EsActivo,
            Roles = modelo.Roles
        };
        var response = await _http.PutAsJsonAsync($"api/usuarios/{modelo.Id}", request);
        return await response.Content.ReadFromJsonAsync<ApiResponse<UsuarioModel>>();
    }

    public async Task<ApiResponse<object>?> CambiarContrasenaAsync(Guid id, string contrasena, bool obligarCambio)
    {
        var response = await _http.PutAsJsonAsync($"api/usuarios/{id}/contrasena",
            new { Contrasena = contrasena, ObligarCambio = obligarCambio });
        return await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
    }

    // ── Roles y permisos ─────────────────────────────────────────────────────

    public async Task<List<RolModel>> ListarRolesAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<RolModel>>>("api/roles");
        return respuesta?.Datos ?? [];
    }

    public async Task<List<PermisoModel>> ListarPermisosAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<PermisoModel>>>("api/permisos");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<RolModel>?> AsignarPermisosAsync(Guid rolId, IReadOnlyList<string> permisos)
    {
        var response = await _http.PutAsJsonAsync($"api/roles/{rolId}/permisos", new { Permisos = permisos });
        return await response.Content.ReadFromJsonAsync<ApiResponse<RolModel>>();
    }

    public async Task<ApiResponse<RolModel>?> CrearRolAsync(RolModel modelo)
    {
        var response = await _http.PostAsJsonAsync("api/roles", new
        {
            Nombre = modelo.Nombre,
            modelo.Descripcion
        });
        return await response.Content.ReadFromJsonAsync<ApiResponse<RolModel>>();
    }

    public async Task<ApiResponse<RolModel>?> ActualizarRolAsync(RolModel modelo)
    {
        var response = await _http.PutAsJsonAsync($"api/roles/{modelo.Id}", new
        {
            Nombre = modelo.Nombre,
            modelo.Descripcion
        });
        return await response.Content.ReadFromJsonAsync<ApiResponse<RolModel>>();
    }

    public async Task<ApiResponse<object>?> QuitarRolAsync(Guid rolId)
    {
        var response = await _http.DeleteAsync($"api/roles/{rolId}");
        return await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
    }

    // ── Parámetros ──────────────────────────────────────────────────────────

    public async Task<List<ParametroModel>> ListarParametrosAsync()
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<ParametroModel>>>("api/parametros");
        return respuesta?.Datos ?? [];
    }

    public async Task<ApiResponse<ParametroModel>?> ActualizarParametroAsync(ParametroModel modelo)
    {
        var response = await _http.PutAsJsonAsync($"api/parametros/{modelo.Id}", new
        {
            Valor = modelo.Valor,
            modelo.Descripcion
        });
        return await response.Content.ReadFromJsonAsync<ApiResponse<ParametroModel>>();
    }

    // ── Auditoría ────────────────────────────────────────────────────────────

    public async Task<PaginaAuditoria> ListarAuditoriaAsync(
        int pagina = 1, int tamanio = 20, string? entidad = null)
    {
        var url = $"api/auditoria?pagina={pagina}&tamanio={tamanio}";
        if (!string.IsNullOrWhiteSpace(entidad))
        {
            url += $"&entidad={Uri.EscapeDataString(entidad)}";
        }

        var respuesta = await _http.GetFromJsonAsync<ApiResponse<PaginaAuditoria>>(url);
        return respuesta?.Datos ?? new PaginaAuditoria();
    }
}
