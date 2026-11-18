using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Domain.Cotizacion;
using Orkin.Cotizador.Core.Domain.MotorCalculo;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Cotizaciones;

/// <summary>
/// Servicio de cotizaciones. Arma la entrada del motor, calcula y (en próximas iteraciones) persiste.
/// <para><b>D-24:</b> no consulta <c>dbo.Cliente</c>, <c>dbo.Producto</c> ni
/// <c>dbo.ActivoAmortizable</c>. Todo código se valida contra las vistas <c>vv*</c> a través de
/// <see cref="CatalogoErpService"/>, y el nombre/unidad/costo resultante se congela en la entrada
/// del motor.</para>
/// </summary>
public sealed class CotizacionService
{
    private readonly IConstructorConfiguracionMotor _configuracion;
    private readonly TipoCambioService _tipoCambio;
    private readonly CatalogoErpService _catalogoErp;
    private readonly CotizadorDbContext _contexto;

    public CotizacionService(
        CotizadorDbContext contexto,
        IConstructorConfiguracionMotor configuracion,
        TipoCambioService tipoCambio,
        CatalogoErpService catalogoErp)
    {
        _contexto = contexto;
        _configuracion = configuracion;
        _tipoCambio = tipoCambio;
        _catalogoErp = catalogoErp;
    }

    /// <summary>
    /// Calcula una cotización en memoria. El cliente puede venir del ERP
    /// (<paramref name="clienteCodigo"/>) o ser alguien nuevo que todavía no existe: en ese caso
    /// quien cotizó eligió el tipo de cliente (<paramref name="tipoClienteId"/>) porque el tipo es
    /// lo que decide el método de costeo, y sin método no hay precio.
    /// </summary>
    public async Task<ResultadoCalculo> CalcularAsync(
        string? clienteCodigo,
        byte? tipoClienteId,
        DateOnly fecha,
        decimal margen,
        IReadOnlyList<(string ProductoCodigo, decimal Cantidad, byte? SemestreId)> insumos,
        IReadOnlyList<(string ActivoCodigo, decimal Cantidad, int? MesesVidaUtil)>? amortizables = null,
        CancellationToken cancellationToken = default)
    {
        byte tipo;

        if (!string.IsNullOrWhiteSpace(clienteCodigo))
        {
            // El cliente del ERP manda sobre el tipo que traiga la solicitud: la ficha es la verdad
            // y permitir "cotizarle a ACME con margen residencial" es cotizar con el método de otro.
            tipo = (await _catalogoErp.ClienteAsync(clienteCodigo, cancellationToken)).TipoClienteId;
        }
        else
        {
            if (tipoClienteId is not { } tipoElegido)
            {
                throw new InvalidOperationException(
                    "Para un cliente nuevo hace falta el tipo de cliente: es lo que decide el método de costeo.");
            }

            if (!await _contexto.TipoClienteCatalogo
                    .AsNoTracking()
                    .AnyAsync(t => t.Id == tipoElegido && t.EsActivo, cancellationToken))
            {
                throw new InvalidOperationException("El tipo de cliente no existe o está inactivo.");
            }

            tipo = tipoElegido;
        }

        var config = await _configuracion.ConstruirAsync(cancellationToken);

        if (!config.MetodoPorSegmento.TryGetValue(tipo, out var metodoCosteoId))
        {
            throw new InvalidOperationException(
                $"El tipo de cliente '{tipo}' no tiene un método de costeo asignado.");
        }

        var (_, tipoCambioValor, _) = await _tipoCambio.ObtenerAsync(fecha, cancellationToken);

        var articulosInsumo = await _catalogoErp.ArticulosAsync(
            insumos.Select(i => i.ProductoCodigo), soloAmortizables: false, cancellationToken);

        var lineasInsumo = new List<LineaInsumo>(insumos.Count);
        foreach (var (productoCodigo, cantidad, semestreId) in insumos)
        {
            var codigo = productoCodigo?.Trim() ?? string.Empty;
            if (!articulosInsumo.TryGetValue(codigo, out var articulo))
            {
                throw new InvalidOperationException(
                    $"Producto: el código '{codigo}' no existe o está inactivo en el catálogo ERP.");
            }

            lineasInsumo.Add(new LineaInsumo(
                articulo.Nombre,
                articulo.UnidadMedida,
                cantidad,
                articulo.CostoUnitario,
                semestreId));
        }

        var lineasAmortizables = new List<LineaAmortizable>();
        if (amortizables is { Count: > 0 })
        {
            var articulosActivo = await _catalogoErp.ArticulosAsync(
                amortizables.Select(a => a.ActivoCodigo), soloAmortizables: true, cancellationToken);

            foreach (var (activoCodigo, cantidad, mesesVidaUtil) in amortizables)
            {
                var codigo = activoCodigo?.Trim() ?? string.Empty;
                if (!articulosActivo.TryGetValue(codigo, out var articulo))
                {
                    throw new InvalidOperationException(
                        $"Activo amortizable: el código '{codigo}' no existe, está inactivo o no es amortizable en el ERP.");
                }

                // vvArticulos no trae vida útil. A diferencia del catálogo local, acá no hay un
                // valor típico al que caer: quien cotiza lo ingresa y 0 sigue prohibido (H-04).
                if (mesesVidaUtil is not > 0)
                {
                    throw new CalculoInvalidoException(
                        $"El activo \"{articulo.Nombre}\" necesita meses de vida útil mayores a cero. " +
                        "El ERP no trae ese dato: ingréselos en la línea del activo.",
                        "vida útil no definida");
                }

                lineasAmortizables.Add(new LineaAmortizable(
                    articulo.Nombre,
                    articulo.UnidadMedida,
                    cantidad,
                    articulo.CostoUnitario,
                    mesesVidaUtil.Value));
            }
        }

        var entrada = new EntradaCalculo
        {
            MetodoCosteoId = metodoCosteoId,
            Margen = margen,
            TipoCambio = tipoCambioValor,
            Insumos = lineasInsumo,
            Amortizables = lineasAmortizables
        };

        var motor = new CotizadorCalculo(config.MetodosPorId, config.Semestres);
        return motor.Calcular(entrada);
    }
}
