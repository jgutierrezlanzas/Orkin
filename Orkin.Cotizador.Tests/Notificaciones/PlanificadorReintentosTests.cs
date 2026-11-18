using Orkin.Cotizador.Core.Application.Notificaciones;

namespace Orkin.Cotizador.Tests.Notificaciones;

/// <summary>
/// Reglas del backoff de la cola de correo. Función pura: si esto falla, una cola averiada
/// reintenta en bucle (fecha pasada) o deja de reintentar durante días (fecha lejana).
/// </summary>
public sealed class PlanificadorReintentosTests
{
    [Theory]
    [InlineData(1, 60, 60)]
    [InlineData(2, 60, 120)]
    [InlineData(3, 60, 240)]
    [InlineData(4, 60, 480)]
    public void El_backoff_duplica_la_base_en_cada_intento(int intento, int baseSegundos, int esperado)
    {
        Assert.Equal(TimeSpan.FromSeconds(esperado), PlanificadorReintentos.Siguiente(intento, baseSegundos));
    }

    [Fact]
    public void El_backoff_nunca_pasa_de_una_hora_aunque_el_intento_sea_enorme()
    {
        // El exponente se acota antes de desplazar: 1 << 40 desbordaría y una espera negativa
        // convertiría ProximoIntentoUtc en una fecha pasada, que el job volvería a reclamar sin parar.
        Assert.Equal(PlanificadorReintentos.Tope, PlanificadorReintentos.Siguiente(40, 60));
        Assert.Equal(TimeSpan.FromHours(1), PlanificadorReintentos.Siguiente(int.MaxValue, 3600));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(1, 0)]
    [InlineData(-1, -5)]
    public void Los_valores_imposibles_se_rechazan(int intento, int baseSegundos)
    {
        Assert.ThrowsAny<ArgumentException>(() => PlanificadorReintentos.Siguiente(intento, baseSegundos));
    }
}
