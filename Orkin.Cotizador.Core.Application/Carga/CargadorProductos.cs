using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Carga el catálogo de productos desde un origen externo (Excel, SQL, lo que sea).
/// Hace upsert idempotente por <c>CodigoExterno</c>: inserta lo nuevo, actualiza lo que
/// cambió, omite lo que tiene el mismo hash.
/// </summary>
public sealed class CargadorProductos : ICargadorCatalogo
{
    private readonly CotizadorDbContext _contexto;

    public CargadorProductos(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public string Catalogo => "PRODUCTOS";

    public async Task<ResultadoCarga> CargarAsync(
        IReadOnlyList<Dictionary<string, object?>> filas,
        ConfiguracionCarga configuracion,
        CancellationToken cancellationToken = default)
    {
        var insertados = 0;
        var actualizados = 0;
        var omitidos = 0;
        var rechazados = new List<RechazoCarga>();

        // Índice de unidades de medida por código, para resolver la FK sin consultar por cada fila.
        var unidades = await _contexto.UnidadMedida
            .AsNoTracking()
            .Where(u => u.IsActive)
            .ToDictionaryAsync(u => u.Codigo, u => u.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var categorias = await _contexto.CategoriaProducto
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToDictionaryAsync(c => c.Codigo, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Filas existentes por CodigoExterno, para decidir insert vs update vs omit.
        var codigosExternos = new List<string>();
        foreach (var fila in filas)
        {
            var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
            var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"));
            if (codigo is not null) codigosExternos.Add(codigo);
        }

        var existentes = await _contexto.Producto
            .Where(p => p.CodigoExterno != null && codigosExternos.Contains(p.CodigoExterno))
            .ToDictionaryAsync(p => p.CodigoExterno!, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var numeroFila = 1;

        foreach (var fila in filas)
        {
            try
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);

                // ── Validar campos obligatorios ─────────────────────────────────────────
                var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"))
                    ?? throw new FormatException("El campo 'Codigo' es obligatorio.");

                var nombre = Normalizacion.AString(mapeada.GetValueOrDefault("Nombre"))
                    ?? throw new FormatException("El campo 'Nombre' es obligatorio.");

                var costo = Normalizacion.ADecimal(mapeada.GetValueOrDefault("CostoActual"), "CostoActual");

                var unidadCodigo = Normalizacion.AString(mapeada.GetValueOrDefault("UnidadMedidaCodigo"))
                    ?? throw new FormatException("El campo 'UnidadMedidaCodigo' es obligatorio.");

                if (!unidades.TryGetValue(unidadCodigo, out var unidadId))
                {
                    throw new FormatException($"La unidad de medida '{unidadCodigo}' no existe. Cárguela primero.");
                }

                var categoriaCodigo = Normalizacion.AString(mapeada.GetValueOrDefault("CategoriaCodigo"));
                Guid? categoriaId = null;
                if (categoriaCodigo is not null)
                {
                    if (!categorias.TryGetValue(categoriaCodigo, out var catId))
                    {
                        throw new FormatException($"La categoría '{categoriaCodigo}' no existe. Cárguela primero.");
                    }
                    categoriaId = catId;
                }

                var precioVenta = Normalizacion.ADecimalOpcional(mapeada.GetValueOrDefault("PrecioVentaActual")) ?? 0m;
                var descripcion = Normalizacion.AString(mapeada.GetValueOrDefault("Descripcion"));
                var vidaUtil = Normalizacion.AEnteroOpcional(mapeada.GetValueOrDefault("VidaUtilMeses"));

                // ── Hash para detectar cambios ────────────────────────────────────────
                var hash = Normalizacion.HashContenido(mapeada);

                // ── Decidir: insert / update / omit ────────────────────────────────────
                if (existentes.TryGetValue(codigo, out var existente))
                {
                    var hashExistente = existente.HashContenido is { Length: > 0 }
                        ? Convert.ToHexString(existente.HashContenido)
                        : null;

                    if (hashExistente == hash)
                    {
                        omitidos++;
                    }
                    else
                    {
                        existente.Nombre = nombre;
                        existente.Descripcion = descripcion;
                        existente.CategoriaId = categoriaId;
                        existente.UnidadMedidaId = unidadId;
                        existente.CostoActual = costo;
                        existente.PrecioVentaActual = precioVenta;
                        existente.VidaUtilMeses = vidaUtil;
                        existente.HashContenido = Convert.FromHexString(hash);
                        existente.UltimaSyncUtc = DateTime.UtcNow;
                        existente.UpdatedAt = DateTime.UtcNow;
                        actualizados++;
                    }
                }
                else
                {
                    var nuevo = new Producto
                    {
                        Id = Guid.NewGuid(),
                        Codigo = codigo,
                        Nombre = nombre,
                        Descripcion = descripcion,
                        CategoriaId = categoriaId,
                        UnidadMedidaId = unidadId,
                        CostoActual = costo,
                        PrecioVentaActual = precioVenta,
                        VidaUtilMeses = vidaUtil,
                        OrigenPrecioId = 2, // EXCEL (del seed OrigenPrecioCatalogo)
                        CodigoExterno = codigo,
                        HashContenido = Convert.FromHexString(hash),
                        UltimaSyncUtc = DateTime.UtcNow,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _contexto.Producto.Add(nuevo);
                    existentes[codigo] = nuevo;
                    insertados++;
                }
            }
            catch (Exception ex)
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
                var codigoExterno = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"));
                rechazados.Add(new RechazoCarga(
                    numeroFila,
                    codigoExterno,
                    ex.Message,
                    null));
            }

            numeroFila++;
        }

        await _contexto.SaveChangesAsync(cancellationToken);

        return new ResultadoCarga
        {
            Catalogo = Catalogo,
            TipoOrigen = configuracion.TipoOrigen,
            Insertados = insertados,
            Actualizados = actualizados,
            Omitidos = omitidos,
            Rechazados = rechazados.Count,
            Rechazos = rechazados
        };
    }
}
