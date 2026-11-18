namespace Orkin.Cotizador.Core.Application.Notificaciones;

/// <summary>
/// Backoff exponencial de la cola de correos. Función pura: se prueba sin reloj, sin base y sin esperar.
/// <para>El tope de una hora importa: sin él, el décimo reintento caería días después y una cola
/// averiada tendría filas "pendientes" que nadie volvería a mirar en toda la semana.</para>
/// </summary>
public static class PlanificadorReintentos
{
    /// <summary>Máximo tiempo entre intentos. Evita esperas de días.</summary>
    public static TimeSpan Tope { get; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Devuelve la espera después del intento <paramref name="numeroIntento"/> (1-based): base,
    /// 2·base, 4·base… con tope en <see cref="Tope"/>.
    /// </summary>
    public static TimeSpan Siguiente(int numeroIntento, int backoffBaseSegundos)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numeroIntento, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(backoffBaseSegundos, 1);

        // Clamp del exponente antes de desplazar: sin esto, 1 << 40 desborda y el resultado
        // "negativo" se convierte en una fecha pasada, que el job reintentaría sin parar.
        var exponente = Math.Clamp(numeroIntento - 1, 0, 16);
        var segundos = Math.Min((long)backoffBaseSegundos * (1L << exponente), Tope.TotalSeconds);

        return TimeSpan.FromSeconds(segundos);
    }
}
