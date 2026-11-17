using System.Net.Http.Json;
using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Services;

/// <summary>Acceso a la API de plantillas de orden de trabajo (Fase 2).</summary>
public sealed class PlantillaService
{
    private readonly HttpClient _http;

    public PlantillaService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<PlantillaModel>> ListarAsync(
        string? texto = null, Guid? clienteId = null, Guid? tecnicoId = null, bool soloActivas = true)
    {
        var parametros = new List<string>();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            parametros.Add($"texto={Uri.EscapeDataString(texto.Trim())}");
        }

        if (clienteId is { } idCliente)
        {
            parametros.Add($"clienteId={idCliente}");
        }

        if (tecnicoId is { } idTecnico)
        {
            parametros.Add($"tecnicoId={idTecnico}");
        }

        parametros.Add($"soloActivas={soloActivas.ToString().ToLowerInvariant()}");

        var url = "api/plantillas?" + string.Join('&', parametros);
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<List<PlantillaModel>>>(url);
        return respuesta?.Datos ?? [];
    }

    public async Task<PlantillaModel?> ObtenerAsync(Guid id)
    {
        var respuesta = await _http.GetFromJsonAsync<ApiResponse<PlantillaModel>>($"api/plantillas/{id}");
        return respuesta?.Datos;
    }

    public async Task<ApiResponse<PlantillaModel>?> GuardarAsync(PlantillaModel modelo, bool esEdicion)
    {
        var request = new
        {
            modelo.Codigo,
            modelo.Nombre,
            modelo.ClienteId,
            modelo.LocacionCodigo,
            modelo.Direccion,
            modelo.TecnicoId,
            modelo.Contacto,
            modelo.Telefono,
            modelo.VehiculoCodigo,
            modelo.DescripcionTrabajo,
            modelo.Notas,
            modelo.EsActivo,
            Detalles = modelo.Detalles.Select(d => new
            {
                d.Id,
                d.Consecutivo,
                d.ProductoId,
                d.DescripcionArticulo,
                d.TipoPlagaCodigo,
                d.LugarTratarCodigo,
                d.Area,
                d.Concentracion,
                d.DosificacionCodigo,
                d.MetodoAplicacionCodigo,
                d.Cantidad,
                d.UnidadMedidaId,
                d.CostoUnitario,
                d.CostoTotal,
                d.MaximoPermitido
            }).ToList(),
            Servicios = modelo.Servicios.Select(s => new
            {
                s.Id,
                s.Consecutivo,
                s.TipoServicioId,
                s.FrecuenciaCodigo
            }).ToList(),
            Equipos = modelo.Equipos.Select(e => new
            {
                e.Id,
                e.Consecutivo,
                e.EquipoCodigo,
                e.Utilizado
            }).ToList()
        };

        var response = esEdicion
            ? await _http.PutAsJsonAsync($"api/plantillas/{modelo.Id}", request)
            : await _http.PostAsJsonAsync("api/plantillas", request);

        return await response.Content.ReadFromJsonAsync<ApiResponse<PlantillaModel>>();
    }

    public async Task<ApiResponse<PlantillaModel>?> CopiarAsync(Guid id, string? codigo, string? nombre)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/plantillas/{id}/copiar", new { Codigo = codigo, Nombre = nombre });
        return await response.Content.ReadFromJsonAsync<ApiResponse<PlantillaModel>>();
    }

    public async Task<bool> DarDeBajaAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/plantillas/{id}");
        var resultado = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        return resultado?.Exito == true;
    }
}
