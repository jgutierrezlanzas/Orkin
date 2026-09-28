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
    public async Task<ResultadoCalculo> CalcularAsync(
        Guid clienteId,
        DateOnly fecha,
        decimal margen,
        IReadOnlyList<(Guid ProductoId, decimal Cantidad, byte? SemestreId)> insumos,
        IReadOnlyList<(Guid ActivoId, decimal Cantidad)>? amortizables = null,
        CancellationToken cancellationToken = default)
    {
        var cliente = await _contexto.Cliente
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteId && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("El cliente no existe o está inactivo.");

        var config = await _configuracion.ConstruirAsync(cancellationToken);

        if (!config.MetodoPorSegmento.TryGetValue(cliente.TipoClienteId, out var metodoCosteoId))
        {
            throw new InvalidOperationException(
                $"El tipo de cliente '{cliente.TipoClienteId}' no tiene un método de costeo asignado.");
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
            foreach (var (activoId, cantidad) in amortizables)
            {
                var activo = await _contexto.ActivoAmortizable
                    .AsNoTracking()
                    .Include(a => a.UnidadMedida)
                    .FirstOrDefaultAsync(a => a.Id == activoId && a.IsActive, cancellationToken)
                    ?? throw new InvalidOperationException($"El activo '{activoId}' no existe o está inactivo.");

                lineasAmortizables.Add(new LineaAmortizable(
                    activo.Nombre,
                    activo.UnidadMedida?.Abreviatura ?? string.Empty,
                    cantidad,
                    activo.CostoActual,
                    activo.MesesVidaUtil));
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