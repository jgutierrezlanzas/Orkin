using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>Acceso a las tablas legacy de plantillas a través de la API única (D-22).</summary>
public sealed class PlantillaService
{
    private readonly HttpClient _http;

    public PlantillaService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<PlantillaModel>> ListarAsync(
        string? texto = null, string? cliente = null, bool soloActivas = true)
    {
        var parametros = new List<string>();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            parametros.Add($"texto={Uri.EscapeDataString(texto.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(cliente))
        {
            parametros.Add($"cliente={Uri.EscapeDataString(cliente.Trim())}");
        }

        parametros.Add($"soloActivas={soloActivas.ToString().ToLowerInvariant()}");

        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<PlantillaModel>>>(
            "api/plantillas?" + string.Join('&', parametros));
        return respuesta?.Datos ?? [];
    }

    public async Task<PlantillaModel?> ObtenerAsync(long plantilla)
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<PlantillaModel>>($"api/plantillas/{plantilla}");
        return respuesta?.Datos;
    }

    public async Task<ApiResponse<PlantillaModel>?> GuardarAsync(PlantillaModel modelo, bool esEdicion)
    {
        var request = new
        {
            modelo.Cliente,
            modelo.Locacion,
            modelo.Direccion,
            modelo.Tecnico,
            modelo.Contacto,
            modelo.Telefono,
            modelo.Vehiculo,
            modelo.DescripcionTrabajo,
            modelo.Notas,
            modelo.Activo,
            Detalles = modelo.Detalles.Select(d => new
            {
                d.CodigoArticulo,
                d.DescripArticulo,
                d.TipoPlaga,
                d.LugarTratar,
                d.Area,
                d.Concentracion,
                d.CodigoDosificacion,
                d.MetodoAplicacion,
                d.Cantidad,
                d.Umedida,
                d.CostoUnitario,
                d.MaximoPermitido
            }).ToList(),
            Servicios = modelo.Servicios.Select(s => new
            {
                s.CodigoServicio,
                s.CodigoFrecuencia
            }).ToList(),
            Equipos = modelo.Equipos.Select(e => new
            {
                e.CodigoEquipo,
                e.Utilizado
            }).ToList()
        };

        var response = esEdicion
            ? await _http.PutAsJsonAsync($"api/plantillas/{modelo.Plantilla}", request)
            : await _http.PostAsJsonAsync("api/plantillas", request);

        return await response.Content.ReadFromJsonAsync<ApiResponse<PlantillaModel>>();
    }

    public async Task<ApiResponse<PlantillaModel>?> CopiarAsync(long plantilla, string? descripcionTrabajo)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/plantillas/{plantilla}/copiar", new { DescripcionTrabajo = descripcionTrabajo });
        return await response.Content.ReadFromJsonAsync<ApiResponse<PlantillaModel>>();
    }

    public async Task<bool> DarDeBajaAsync(long plantilla)
    {
        var response = await _http.DeleteAsync($"api/plantillas/{plantilla}");
        var resultado = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        return resultado?.Exito == true;
    }
}
