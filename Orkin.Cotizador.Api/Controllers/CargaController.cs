using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Infrastructure.Carga;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Dispara el proceso de carga de catálogos desde un origen externo.
/// Hoy soporta archivos Excel (.xlsx); los demás orígenes se agregan implementando
/// <see cref="ILectorOrigen"/>.
/// </summary>
[ApiController]
[Route("api/carga")]
[Produces("application/json")]
[Authorize("CATALOGOS.EDITAR")]
public sealed class CargaController : ControllerBase
{
    private readonly ProcesoCargaService _proceso;

    public CargaController(ProcesoCargaService proceso)
    {
        _proceso = proceso;
    }

    /// <summary>
    /// Carga un catálogo desde un archivo Excel (.xlsx). El archivo se sube y se procesa
    /// en el momento; no queda guardado en el servidor.
    /// </summary>
    /// <param name="catalogo">PRODUCTOS, CLIENTES, etc.</param>
    /// <param name="hoja">Nombre de la hoja dentro del archivo. Opcional: si no se envía, se lee la primera.</param>
    /// <param name="filaEncabezado">
    /// Fila (1-based) donde están los nombres de columna. Opcional: si no se envía, se toma la
    /// primera fila usada de la hoja, que es lo correcto en un archivo que empieza con los
    /// encabezados. Hace falta cuando el archivo tiene un título arriba: sin esto el título se
    /// lee como el nombre de una columna y todas las filas se rechazan por el mismo campo.
    /// </param>
    [HttpPost("excel/{catalogo}")]
    [Consumes("multipart/form-data")]
    public async Task<IResult> CargarDesdeExcel(
        string catalogo,
        [FromForm] IFormFile archivo,
        [FromQuery] string? hoja = null,
        [FromQuery] int filaEncabezado = 0,
        CancellationToken cancellationToken = default)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return ApiResponse<ResultadoCarga>
                .Error("ARCHIVO_VACIO", "Adjunte un archivo Excel (.xlsx o .xlsm).")
                .AHttp();
        }

        // Se aceptan los dos formatos a propósito, y no por generosidad. El archivo del cliente
        // es un `.xlsm` —tiene macros, y el cotizador de Excel las usa para calcular—, así que
        // rechazar `.xlsm` por la extensión le negaría a ORKIN la carga de su propio histórico.
        // Los dos son el mismo formato: un paquete OOXML (zip) con una hoja XML adentro, y
        // ClosedXML los abre igual. Lo que no se puede es un `.xls` viejo (binario, formato
        // BIFF8): no es el mismo archivo con otro nombre, es otro formato, y por eso queda fuera.
        var esExcel = archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                   || archivo.FileName.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase);

        if (!esExcel)
        {
            return ApiResponse<ResultadoCarga>
                .Error("FORMATO_NO_SOPORTADO", "El archivo debe ser .xlsx o .xlsm.")
                .AHttp();
        }

        var mapeo = MapeosPorDefecto.Obtener(catalogo);

        if (mapeo is null)
        {
            return ApiResponse<ResultadoCarga>
                .Error("CATALOGO_NO_SOPORTADO",
                    $"El catálogo '{catalogo}' no tiene un mapeo configurado. " +
                    $"Los disponibles son: {string.Join(", ", MapeosPorDefecto.Disponibles)}.")
                .AHttp();
        }

        var configuracion = new ConfiguracionCarga
        {
            Catalogo = catalogo.ToUpperInvariant(),
            TipoOrigen = "EXCEL",
            MapeoColumnas = mapeo
        };

        try
        {
            await using var stream = archivo.OpenReadStream();
            var lector = new LectorExcel(stream, hoja, filaEncabezado);

            var resultado = await _proceso.EjecutarAsync(configuracion, lector, cancellationToken);

            return ApiResponse<ResultadoCarga>.Ok(resultado).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ResultadoCarga>
                .Error("ERROR_LECTURA", ex.Message)
                .AHttp();
        }
    }
}

/// <summary>
/// Mapeos de columnas por defecto para los orígenes más comunes. Cada cliente puede tener
/// su propio formato de Excel; estos son los nombres de columna que el proceso espera
/// encontrar en la primera fila del archivo.
/// </summary>
public static class MapeosPorDefecto
{
    public static readonly string[] Disponibles = ["PRODUCTOS"];

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Mapeos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PRODUCTOS"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Codigo"] = "Codigo",
                ["Código"] = "Codigo",
                ["CODIGO"] = "Codigo",
                ["Nombre"] = "Nombre",
                ["NOMBRE"] = "Nombre",

                // El Excel de ORKIN llama a la columna de descripción "PRODUCTO", no "Nombre".
                // Sin esta entrada, un archivo correcto se rechaza entero con "El campo 'Nombre'
                // es obligatorio" en las 134 filas, que manda al soporte a suspectar del
                // archivo en vez del mapeo. La comparación es OrdinalIgnoreCase, así que esta
                // entrada cubre PRODUCTO, Producto y producto.
                ["Producto"] = "Nombre",
                ["Descripción del producto"] = "Nombre",
                ["Descripcion del producto"] = "Nombre",
                ["Descripción Producto"] = "Nombre",
                ["Descripción"] = "Descripcion",
                ["Descripcion"] = "Descripcion",
                ["Categoria"] = "CategoriaCodigo",
                ["Categoría"] = "CategoriaCodigo",
                ["Unidad"] = "UnidadMedidaCodigo",
                ["Unidad de Medida"] = "UnidadMedidaCodigo",
                ["UnidadMedida"] = "UnidadMedidaCodigo",
                ["UM"] = "UnidadMedidaCodigo",
                ["Costo"] = "CostoActual",
                ["Costo Actual"] = "CostoActual",
                ["Precio"] = "PrecioVentaActual",
                ["Precio Venta"] = "PrecioVentaActual",
                ["Precio de Venta"] = "PrecioVentaActual",
                ["Vida Util"] = "VidaUtilMeses",
                ["VidaÚtil"] = "VidaUtilMeses",
                ["VidaUtilMeses"] = "VidaUtilMeses"
            }
        };

    public static IReadOnlyDictionary<string, string>? Obtener(string catalogo)
        => Mapeos.TryGetValue(catalogo, out var mapeo) ? mapeo : null;
}
