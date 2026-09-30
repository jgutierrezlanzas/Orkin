using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Carga el catálogo de activos amortizables desde el ERP del cliente. El activo amortizable es
/// la trampa, la lámpara UV, el cebadero: a diferencia del insumo, <b>se descuenta por meses</b>
/// dentro de la cotización (RN-02: costo / meses de vida útil).
/// <para>Misma mecánica de <see cref="CargadorProductos"/>: upsert por <c>CodigoExterno</c>,
/// comparación por hash, adopción de lo capturado a mano con el mismo código. La vida útil es
/// obligatoria y mayor que cero (H-04: el Excel la traía en cero y los amortizables aportaban
/// nada a la cotización empresarial).</para>
/// </summary>
public sealed class CargadorActivosAmortizables : ICargadorCatalogo
{
    private readonly CotizadorDbContext _contexto;

    public CargadorActivosAmortizables(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public string Catalogo => "ACTIVOS_AMORTIZABLES";

    public IReadOnlyList<CampoEsperado> CamposEsperados =>
    [
        new("Codigo", "texto", true, "El código del activo en el ERP del cliente"),
        new("Nombre", "texto", true, "El nombre del activo (trampa, lámpara, cebadero...)"),
        new("CostoActual", "número", true, "El costo del activo. Acepta decimales."),
        new("MesesVidaUtil", "entero", true, "Vida útil en meses. Debe ser mayor que cero — es lo que permite calcular el valor mensual."),
        new("UnidadMedidaCodigo", "texto", true, "El código de la unidad en la plataforma (UND, PAR...)."),
        new("CategoriaCodigo", "texto", false, "El código de la categoría del activo."),
        new("ValorResidual", "número", false, "Valor residual al final de la vida útil. Normalmente 0."),
        new("Cantidad", "número", false, "Cuántos de este activo hay. Normalmente 1."),
        new("Descripcion", "texto", false, "Descripción del activo."),
    ];

    public async Task<ResultadoCarga> CargarAsync(
        IReadOnlyList<Dictionary<string, object?>> filas,
        ConfiguracionCarga configuracion,
        CancellationToken cancellationToken = default)
    {
        var insertados = 0;
        var actualizados = 0;
        var omitidos = 0;
        var rechazados = new List<RechazoCarga>();

        // Índice de unidades y categorías por código, para resolver las FK sin consultar por fila.
        var unidades = await _contexto.UnidadMedida
            .AsNoTracking()
            .Where(u => u.IsActive)
            .ToDictionaryAsync(u => u.Codigo, u => u.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var categorias = await _contexto.CategoriaProducto
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToDictionaryAsync(c => c.Codigo, u => u.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var origenPrecioId = await _contexto.OrigenPrecioCatalogo
            .AsNoTracking()
            .Where(o => o.Codigo == "ERP")
            .Select(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (origenPrecioId == 0)
        {
            throw new InvalidOperationException(
                "No está sembrado el catálogo OrigenPrecio (falta ERP). Corra la semilla.");
        }

        var codigosExternos = new List<string>();
        foreach (var fila in filas)
        {
            var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
            var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"));
            if (codigo is not null) codigosExternos.Add(codigo);
        }

        var existentes = await _contexto.ActivoAmortizable
            .Where(a => a.ErpConexionId == configuracion.ErpConexionId
                     && a.CodigoExterno != null
                     && codigosExternos.Contains(a.CodigoExterno))
            .ToDictionaryAsync(a => a.CodigoExterno!, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var huerfanas = await _contexto.ActivoAmortizable
            .Where(a => a.ErpConexionId == null && codigosExternos.Contains(a.Codigo))
            .ToDictionaryAsync(a => a.Codigo, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var numeroFila = 1;
        var simulados = new List<CambioSimulado>();
        var hashesSimulados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in filas)
        {
            try
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);

                var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"))
                    ?? throw new FormatException("El campo 'Codigo' es obligatorio.");

                var nombre = Normalizacion.AString(mapeada.GetValueOrDefault("Nombre"))
                    ?? throw new FormatException("El campo 'Nombre' es obligatorio.");

                var costo = Normalizacion.ADecimal(mapeada.GetValueOrDefault("CostoActual"), "CostoActual");

                var vidaUtil = Normalizacion.AEnteroOpcional(mapeada.GetValueOrDefault("MesesVidaUtil"))
                    ?? throw new FormatException("El campo 'MesesVidaUtil' es obligatorio (H-04: sin vida útil, el amortizable aporta nada a la cotización).");

                if (vidaUtil <= 0)
                {
                    throw new FormatException($"La vida útil debe ser mayor que cero (llegó {vidaUtil}).");
                }

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

                var valorResidual = Normalizacion.ADecimalOpcional(mapeada.GetValueOrDefault("ValorResidual")) ?? 0m;
                var cantidad = Normalizacion.ADecimalOpcional(mapeada.GetValueOrDefault("Cantidad")) ?? 1m;
                var descripcion = Normalizacion.AString(mapeada.GetValueOrDefault("Descripcion"));

                var hash = Normalizacion.HashContenido(mapeada);

                if (existentes.TryGetValue(codigo, out var existente))
                {
                    var hashExistente = hashesSimulados.TryGetValue(codigo, out var yaSimulado)
                        ? yaSimulado
                        : existente?.HashContenido is { Length: > 0 }
                            ? Convert.ToHexString(existente.HashContenido)
                            : null;

                    if (hashExistente == hash)
                    {
                        omitidos++;
                        if (configuracion.EsSimulacion)
                        {
                            simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Omitir, []));
                        }
                    }
                    else
                    {
                        var cambios = new List<CampoSimulado>();
                        if (existente.Nombre != nombre)
                            cambios.Add(new CampoSimulado("Nombre", existente.Nombre, nombre));
                        if (existente.CostoActual != costo)
                            cambios.Add(new CampoSimulado("Costo", existente.CostoActual.ToString("N2"), costo.ToString("N2")));
                        if (existente.MesesVidaUtil != vidaUtil)
                            cambios.Add(new CampoSimulado("Vida útil (meses)", existente.MesesVidaUtil.ToString(), vidaUtil.ToString()));

                        if (configuracion.EsSimulacion)
                        {
                            simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Actualizar, cambios));
                            hashesSimulados[codigo] = hash;
                        }
                        else
                        {
                            existente.Nombre = nombre;
                            existente.Descripcion = descripcion;
                            existente.CategoriaId = categoriaId;
                            existente.UnidadMedidaId = unidadId;
                            existente.CostoActual = costo;
                            existente.MesesVidaUtil = vidaUtil;
                            existente.ValorResidual = valorResidual;
                            existente.Cantidad = cantidad;
                            existente.HashContenido = Convert.FromHexString(hash);
                            existente.UltimaSyncUtc = DateTime.UtcNow;
                            existente.UpdatedAt = DateTime.UtcNow;
                        }

                        actualizados++;
                    }
                }
                else if (huerfanas.Remove(codigo, out var huerfana))
                {
                    if (configuracion.EsSimulacion)
                    {
                        simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Adoptar,
                            [new CampoSimulado("Procedencia", "Ninguna (capturada a mano)",
                                $"La conexión «{configuracion.ErpConexionId}»")]));
                        hashesSimulados[codigo] = hash;
                    }
                    else
                    {
                        huerfana.CodigoExterno = codigo;
                        huerfana.ErpConexionId = configuracion.ErpConexionId;
                        huerfana.OrigenPrecioId = origenPrecioId;
                        huerfana.Nombre = nombre;
                        huerfana.Descripcion = descripcion;
                        huerfana.CategoriaId = categoriaId;
                        huerfana.UnidadMedidaId = unidadId;
                        huerfana.CostoActual = costo;
                        huerfana.MesesVidaUtil = vidaUtil;
                        huerfana.ValorResidual = valorResidual;
                        huerfana.Cantidad = cantidad;
                        huerfana.HashContenido = Convert.FromHexString(hash);
                        huerfana.UltimaSyncUtc = DateTime.UtcNow;
                        huerfana.UpdatedAt = DateTime.UtcNow;
                    }

                    existentes[codigo] = huerfana;
                    actualizados++;
                }
                else
                {
                    var nuevo = new ActivoAmortizable
                    {
                        Id = Guid.NewGuid(),
                        Codigo = codigo,
                        Nombre = nombre,
                        Descripcion = descripcion,
                        CategoriaId = categoriaId,
                        UnidadMedidaId = unidadId,
                        CostoActual = costo,
                        MesesVidaUtil = vidaUtil,
                        ValorResidual = valorResidual,
                        Cantidad = cantidad,
                        OrigenPrecioId = origenPrecioId,
                        ErpConexionId = configuracion.ErpConexionId,
                        CodigoExterno = codigo,
                        HashContenido = Convert.FromHexString(hash),
                        UltimaSyncUtc = DateTime.UtcNow,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    if (configuracion.EsSimulacion)
                    {
                        simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Insertar, []));
                    }
                    else
                    {
                        _contexto.ActivoAmortizable.Add(nuevo);
                    }

                    existentes[codigo] = nuevo;
                    insertados++;
                }
            }
            catch (Exception ex)
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
                rechazados.Add(new RechazoCarga(
                    numeroFila,
                    Normalizacion.AString(mapeada.GetValueOrDefault("Codigo")),
                    ex.Message,
                    null));
            }

            numeroFila++;
        }

        if (configuracion.EsSimulacion)
        {
            var pendientes = _contexto.ChangeTracker.Entries().Count(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached);
            if (pendientes > 0)
            {
                throw new InvalidOperationException(
                    $"El cargador de {Catalogo} dejó {pendientes} cambios sin guardar en una simulación. " +
                    "Un cargador en modo simulación no debe modificar entidades: debe reportar el cambio, no aplicarlo.");
            }
        }
        else
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }

        return new ResultadoCarga
        {
            Catalogo = Catalogo,
            TipoOrigen = configuracion.TipoOrigen,
            Insertados = insertados,
            Actualizados = actualizados,
            Omitidos = omitidos,
            Rechazados = rechazados.Count,
            Rechazos = rechazados,
            Cambios = simulados
        };
    }
}
