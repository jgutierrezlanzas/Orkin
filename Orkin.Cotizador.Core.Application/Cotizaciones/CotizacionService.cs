using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Domain.Cotizacion;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Domain.MotorCalculo;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Cotizaciones;

/// <summary>
/// Servicio de cotizaciones. Arma la entrada del motor, calcula y (en próximas iteraciones) persiste.
/// </summary>
public sealed class CotizacionService
{
    private readonly CotizadorDbContext _contexto;
    private readonly IConstructorConfiguracionMotor _configuracion;
    private readonly TipoCambioService _tipoCambio;

    public CotizacionService(
        CotizadorDbContext contexto,
        IConstructorConfiguracionMotor configuracion,
        TipoCambioService tipoCambio)
    {
        _contexto = contexto;
        _configuracion = configuracion;
        _tipoCambio = tipoCambio;
    }

    /// <summary>
    /// Calcula una cotización en memoria sin persistirla. Útil para el preview antes de guardar.
    /// </summary>
    /// <summary>
    /// Calcula una cotización en memoria. El cliente puede venir del catálogo
    /// (<paramref name="clienteId"/>) o ser alguien nuevo que todavía no existe: en ese caso
    /// quien cotizó eligió el tipo de cliente (<paramref name="tipoClienteId"/>) porque el
    /// tipo es lo que decide el método de costeo, y sin método no hay precio.
    /// </summary>
    public async Task<ResultadoCalculo> CalcularAsync(
        Guid? clienteId,
        byte? tipoClienteId,
        DateOnly fecha,
        decimal margen,
        IReadOnlyList<(Guid ProductoId, decimal Cantidad, byte? SemestreId)> insumos,
        IReadOnlyList<(Guid ActivoId, decimal Cantidad, int? MesesVidaUtil)>? amortizables = null,
        CancellationToken cancellationToken = default)
    {
        byte tipo;

        if (clienteId is { } id)
        {
            // El cliente del catálogo manda sobre el tipo que traiga la solicitud: la ficha es
            // la verdad y permitir "cotizarle a ACME con margen residencial" es cotizar con el
            // método de otro.
            var cliente = await _contexto.Cliente
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("El cliente no existe o está inactivo.");

            tipo = cliente.TipoClienteId;
        }
        else
        {
            if (tipoClienteId is not { } tipoElegido)
            {
                throw new InvalidOperationException(
                    "Para un cliente nuevo hace falta el tipo de cliente: es lo que decide el método de costeo.");
            }

            var existe = await _contexto.TipoClienteCatalogo
                .AsNoTracking()
                .AnyAsync(t => t.Id == tipoElegido && t.EsActivo, cancellationToken);

            if (!existe)
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

        var (tipoCambioId, tipoCambioValor, tipoCambioFecha) = await _tipoCambio.ObtenerAsync(fecha, cancellationToken);

        var lineasInsumo = new List<LineaInsumo>();
        foreach (var (productoId, cantidad, semestreId) in insumos)
        {
            var producto = await _contexto.Producto
                .AsNoTracking()
                .Include(p => p.UnidadMedida)
                .FirstOrDefaultAsync(p => p.Id == productoId && p.IsActive, cancellationToken)
                ?? throw new InvalidOperationException($"El producto '{productoId}' no existe o está inactivo.");

            lineasInsumo.Add(new LineaInsumo(
                producto.Nombre,
                producto.UnidadMedida?.Abreviatura ?? string.Empty,
                cantidad,
                producto.CostoActual,
                semestreId));
        }

        var lineasAmortizables = new List<LineaAmortizable>();
        if (amortizables is not null)
        {
            foreach (var (activoId, cantidad, mesesVidaUtil) in amortizables)
            {
                var activo = await _contexto.ActivoAmortizable
                    .AsNoTracking()
                    .Include(a => a.UnidadMedida)
                    .FirstOrDefaultAsync(a => a.Id == activoId && a.IsActive, cancellationToken)
                    ?? throw new InvalidOperationException($"El activo '{activoId}' no existe o está inactivo.");

                // La vida útil de la cotización pisa la del catálogo: el catálogo trae el valor
                // típico (12 meses), pero una trampa a la intemperie dura 6 y la misma bajo
                // techo dura 24 — quien cotiza lo sabe. Si la cotización no trae el dato, se
                // usa el del catálogo.
                var vidaUtil = mesesVidaUtil is > 0 ? mesesVidaUtil.Value : activo.MesesVidaUtil;

                lineasAmortizables.Add(new LineaAmortizable(
                    activo.Nombre,
                    activo.UnidadMedida?.Abreviatura ?? string.Empty,
                    cantidad,
                    activo.CostoActual,
                    vidaUtil));
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