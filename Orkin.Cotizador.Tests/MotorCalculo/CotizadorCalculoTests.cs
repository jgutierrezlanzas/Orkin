using Orkin.Cotizador.Core.Domain.Cotizacion;
using Orkin.Cotizador.Core.Domain.MotorCalculo;

namespace Orkin.Cotizador.Tests.MotorCalculo;

/// <summary>
/// Pruebas del motor de cálculo. No usan base de datos: el motor es puro dominio (ADR-005).
/// </summary>
public sealed class CotizadorCalculoTests
{
    private const byte MetodoSumaSimpleId = 1;
    private const byte MetodoPromedioId = 2;
    private const byte PrimerSemestreId = 1;
    private const byte SegundoSemestreId = 2;

    private static readonly SemestresResueltos Semestres = new(PrimerSemestreId, SegundoSemestreId);

    private static CotizadorCalculo CrearMotor(IMetodoCosteo metodo, byte id)
        => new(new Dictionary<byte, IMetodoCosteo> { [id] = metodo }, Semestres);

    private static CotizadorCalculo MotorSumaSimple() => CrearMotor(new MetodoCosteoSumaSimpleStub(), MetodoSumaSimpleId);
    private static CotizadorCalculo MotorPromedio() => CrearMotor(new MetodoCosteoPromedioSemestresStub(), MetodoPromedioId);

    [Fact]
    public void Residencial_SinPlantilla_CalculaPrecioCorrecto()
    {
        var motor = MotorSumaSimple();
        var entrada = new EntradaCalculo
        {
            MetodoCosteoId = MetodoSumaSimpleId,
            Margen = 0.0550m,
            TipoCambio = 36.62m,
            Insumos = [new LineaInsumo("Insecticida", "ml", 2, 150.00m)],
            Amortizables = [new LineaAmortizable("Bomba", "u", 1, 1200.00m, 12)],
            CostoPlantilla = 0m
        };

        var resultado = motor.Calcular(entrada);

        Assert.Equal(300.00m, resultado.TotalInsumosSemestre1);
        Assert.Equal(0m, resultado.TotalInsumosSemestre2);
        Assert.Equal(300.00m, resultado.TotalInsumos);
        Assert.Equal(100.00m, resultado.TotalAmortizablesMensual);
        Assert.Equal(400.00m, resultado.CostoTotal);
        Assert.Equal(400.00m * 1.0550m, resultado.PrecioCordobas);
        Assert.Equal(resultado.PrecioCordobas / 36.62m, resultado.PrecioUsd);
        Assert.Equal(MetodoSumaSimpleId, resultado.MetodoCosteoId);
    }

    [Fact]
    public void Residencial_ConPlantilla_AplicaCostoPlantilla()
    {
        var motor = MotorSumaSimple();
        var entrada = new EntradaCalculo
        {
            MetodoCosteoId = MetodoSumaSimpleId,
            Margen = 0.10m,
            TipoCambio = 36.62m,
            Insumos = [new LineaInsumo("Insecticida", "ml", 1, 100.00m)],
            Amortizables = [],
            CostoPlantilla = 0.25m
        };

        var resultado = motor.Calcular(entrada);

        // CostoTotal = 100 + (100 * 0.25) = 125
        Assert.Equal(125.00m, resultado.CostoTotal);
        Assert.Equal(125.00m * 1.10m, resultado.PrecioCordobas);
    }

    [Fact]
    public void Empresarial_PromediaSemestres_Y_NoAplicaPlantilla()
    {
        var motor = MotorPromedio();
        var entrada = new EntradaCalculo
        {
            MetodoCosteoId = MetodoPromedioId,
            Margen = 0.20m,
            TipoCambio = 36.62m,
            Insumos =
            [
                new LineaInsumo("Semestre 1", "u", 1, 200.00m, PrimerSemestreId),
                new LineaInsumo("Semestre 2", "u", 1, 400.00m, SegundoSemestreId)
            ],
            Amortizables = [new LineaAmortizable("Equipo", "u", 1, 600.00m, 6)],
            CostoPlantilla = 0.50m // Debe ignorarse en empresarial
        };

        var resultado = motor.Calcular(entrada);

        Assert.Equal(200.00m, resultado.TotalInsumosSemestre1);
        Assert.Equal(400.00m, resultado.TotalInsumosSemestre2);
        Assert.Equal(300.00m, resultado.TotalInsumos);           // promedio
        Assert.Equal(100.00m, resultado.TotalAmortizablesMensual);
        Assert.Equal(400.00m, resultado.CostoTotal);             // no aplica plantilla
        Assert.Equal(400.00m * 1.20m, resultado.PrecioCordobas);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Margen_FueraDeRango_LanzaExcepcion(decimal margen)
    {
        var motor = MotorSumaSimple();
        var entrada = new EntradaCalculo
        {
            MetodoCosteoId = MetodoSumaSimpleId,
            Margen = margen,
            TipoCambio = 36.62m
        };

        var ex = Assert.Throws<CalculoInvalidoException>(() => motor.Calcular(entrada));
        Assert.Equal("margen fuera de rango", ex.Codigo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TipoDeCambio_Invalido_LanzaExcepcion(decimal tipoCambio)
    {
        var motor = MotorSumaSimple();
        var entrada = new EntradaCalculo
        {
            MetodoCosteoId = MetodoSumaSimpleId,
            Margen = 0.10m,
            TipoCambio = tipoCambio
        };

        var ex = Assert.Throws<CalculoInvalidoException>(() => motor.Calcular(entrada));
        Assert.Equal("tipo de cambio inválido", ex.Codigo);
    }

    [Fact]
    public void VidaUtil_Cero_LanzaExcepcion()
    {
        var motor = MotorSumaSimple();
        var entrada = new EntradaCalculo
        {
            MetodoCosteoId = MetodoSumaSimpleId,
            Margen = 0.10m,
            TipoCambio = 36.62m,
            Amortizables = [new LineaAmortizable("Equipo", "u", 1, 100.00m, 0)]
        };

        var ex = Assert.Throws<CalculoInvalidoException>(() => motor.Calcular(entrada));
        Assert.Equal("vida útil inválida", ex.Codigo);
    }

    [Fact]
    public void MetodoNoRegistrado_LanzaExcepcion()
    {
        var motor = new CotizadorCalculo(
            new Dictionary<byte, IMetodoCosteo> { [MetodoSumaSimpleId] = new MetodoCosteoSumaSimpleStub() },
            Semestres);

        var entrada = new EntradaCalculo
        {
            MetodoCosteoId = 99,
            Margen = 0.10m,
            TipoCambio = 36.62m
        };

        var ex = Assert.Throws<CalculoInvalidoException>(() => motor.Calcular(entrada));
        Assert.Equal("metodo de costeo no implementado", ex.Codigo);
    }

    /// <summary>Stub que expone un Id numérico para los tests sin depender de la base.</summary>
    private sealed class MetodoCosteoSumaSimpleStub : IMetodoCosteo
    {
        public string Codigo => CodigosMetodoCosteo.SumaSimple;
        public decimal CalcularTotalInsumos(decimal s1, decimal s2) => s1 + s2;
        public decimal CalcularCostoTotal(decimal insumos, decimal amortizables, decimal plantilla)
        {
            var subtotal = insumos + amortizables;
            return plantilla is 0 ? subtotal : subtotal + (subtotal * plantilla);
        }
    }

    private sealed class MetodoCosteoPromedioSemestresStub : IMetodoCosteo
    {
        public string Codigo => CodigosMetodoCosteo.PromedioSemestres;
        public decimal CalcularTotalInsumos(decimal s1, decimal s2) => (s1 + s2) / 2m;
        public decimal CalcularCostoTotal(decimal insumos, decimal amortizables, decimal plantilla)
            => insumos + amortizables;
    }
}
