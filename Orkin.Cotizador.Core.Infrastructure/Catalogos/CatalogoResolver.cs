using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Infrastructure.Catalogos;

/// <summary>
/// Implementación de <see cref="ICatalogoResolver"/> sobre <see cref="CotizadorDbContext"/>.
/// <para><b>Por qué se resuelve con un scope propio</b> en vez de inyectar el contexto directamente:
/// este servicio se registra como singleton (la caché tiene que sobrevivir a las peticiones) y un
/// <see cref="DbContext"/> no puedevivir en un singleton. Se abre un scope corto por lectura en frío
/// y se suelta; mientras la caché esté caliente no se vuelve a tocar la base.</para>
/// <para><b>Por qué hay un semáforo y no un <c>ConcurrentDictionary.GetOrAdd</c> con fábrica:</b>
/// <c>GetOrAdd</c> puede invocar la fábrica varias veces a la vez y devolver la primera, dejando las
/// demás executing. Aquí eso significa N <c>SELECT</c> idénticos, y además el constructor de un
/// <see cref="SemaphoreSlim"/> que se pierde ensuciaría la liberación. El semáforo serializa la
/// primera carga por tabla y las demás esperan.</para>
/// </summary>
public sealed class CatalogoResolver : ICatalogoResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogoResolver> _logger;

    /// <summary>Caché por tipo de entidad. El valor es inmutable: se construye una vez y no se toca.</summary>
    private readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, ValorCatalogo>> _cache = new();

    private static readonly ConcurrentDictionary<Type, SemaphoreSlim> Semaforos = new();

    public CatalogoResolver(IServiceScopeFactory scopeFactory, ILogger<CatalogoResolver> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<byte> IdAsync<T>(string codigo, CancellationToken cancellationToken = default)
        where T : CatalogoDominioBase
    {
        var valor = await ObtenerAsync<T>(codigo, cancellationToken);
        return valor.Id;
    }

    public async Task<ValorCatalogo> ObtenerAsync<T>(string codigo, CancellationToken cancellationToken = default)
        where T : CatalogoDominioBase
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        var tabla = await CargarAsync<T>(cancellationToken);

        if (!tabla.TryGetValue(codigo, out var valor))
        {
            // Deliberadamente una excepción y no un 0: un 0 silencioso se pierde en la base y
            // termina en una cotización calculada con el valor equivocado (ADR-018).
            throw new CatalogoNoResueltoException(typeof(T).Name, codigo);
        }

        return valor;
    }

    public async Task<IReadOnlyList<ValorCatalogo>> ListarAsync<T>(
        bool soloActivos = true,
        CancellationToken cancellationToken = default)
        where T : CatalogoDominioBase
    {
        var tabla = await CargarAsync<T>(cancellationToken);

        return tabla.Values
            .Where(v => !soloActivos || v.EsActivo)
            .OrderBy(v => v.Orden)
            // Desempate por Codigo: sin él, dos valores con el mismo Orden pueden salir en
            // cualquier orden y la UI "temblaría" entre dos peticiones idénticas.
            .ThenBy(v => v.Codigo, StringComparer.Ordinal)
            .ToArray();
    }

    public void InvalidarCache()
    {
        var cantidad = _cache.Count;
        _cache.Clear();

        _logger.LogInformation(
            "Caché de catálogos invalidada: {Cantidad} tablas. Se releen de la base en la siguiente consulta.",
            cantidad);
    }

    /// <summary>
    /// Devuelve el catálogo completo indexado por <c>Codigo</c>, leyéndolo de la base solo la primera
    /// vez. Incluye los inactivos: <see cref="ObtenerAsync{T}"/> decide si un inactivo sirve, y
    /// <see cref="ListarAsync{T}"/> los filtra.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, ValorCatalogo>> CargarAsync<T>(
        CancellationToken cancellationToken)
        where T : CatalogoDominioBase
    {
        var tipo = typeof(T);

        if (_cache.TryGetValue(tipo, out var existente))
        {
            return existente;
        }

        var semaforo = Semaforos.GetOrAdd(tipo, _ => new SemaphoreSlim(1, 1));
        await semaforo.WaitAsync(cancellationToken);

        try
        {
            //otra carrera pudo cargar la tabla mientras este esperaba el semáforo.
            if (_cache.TryGetValue(tipo, out existente))
            {
                return existente;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CotizadorDbContext>();

            var filas = await context.Set<T>()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var tabla = filas.ToDictionary(
                f => f.Codigo,
                f => new ValorCatalogo(f.Id, f.Codigo, f.Nombre, f.Descripcion, f.Orden, f.EsActivo),
                StringComparer.OrdinalIgnoreCase);

            _cache[tipo] = tabla;

            _logger.LogInformation(
                "Catálogo {Catalogo} cargado: {Cantidad} valores.",
                tipo.Name,
                tabla.Count);

            return tabla;
        }
        finally
        {
            semaforo.Release();
        }
    }
}
