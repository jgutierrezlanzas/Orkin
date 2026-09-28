using Orkin.Cotizador.Core.Domain.MotorCalculo;

namespace Orkin.Cotizador.Tests.MotorCalculo;

public sealed class SemestresResueltosTests
{
    [Fact]
    public void Constructor_ConIdsIguales_LanzaArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SemestresResueltos(1, 1));
        Assert.Contains("mismo id", ex.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void Clasificar_Null_DevuelvePrimerSemestre()
    {
        var semestres = new SemestresResueltos(1, 2);

        var clasificado = semestres.Clasificar(null);

        Assert.Equal((byte)1, clasificado);
    }

    [Fact]
    public void Clasificar_SegundoSemestre_DevuelveSegundo()
    {
        var semestres = new SemestresResueltos(1, 2);

        var clasificado = semestres.Clasificar(2);

        Assert.Equal((byte)2, clasificado);
    }
}
