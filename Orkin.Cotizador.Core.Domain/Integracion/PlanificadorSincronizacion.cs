using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Core.Domain.Integracion;

/// <summary>
/// Las tres decisiones que el motor de programación toma sobre un flujo: cuándo le toca de nuevo,
/// si hay que pausarlo, y si su corrida merece fila en la bitácora.
/// <para>Están en una clase estática y sin base de datos a propósito. Son reglas, no I/O, y lo
/// difícil de este motor no es que ejecute la carga —eso ya lo sabe hacer
/// <c>ProcesoCargaService</c>— sino que no repita trabajo ni golpee de más el ERP de un cliente.
/// Merecen poder probarse sin levantar un ERP falso, y una prueba que necesita una base para
/// comprobar una suma no está probando la suma.</para>
/// </summary>
public static class PlanificadorSincronizacion
{
    /// <summary>
    /// Instante en que el flujo vuelve a correr.
    /// <para>Se cuenta desde <paramref name="ahora"/> y no desde la hora a la que venció: un motor
    /// que se reinicia, o que estuvo ocupado con otro flujo, no debe acumular una cola de
    /// corridas perdidas que se ejecuten todas de golpe al volver. Perder una corrida de un flujo
    /// que se sincroniza cada 10 segundos no cuesta nada; pegarle 300 consultas de golpe al ERP
    /// de un cliente, sí.</para>
    /// <para>El <see cref="ErpFlujo.DesfaseSegundos"/> no va aquí a propósito: es fijo por flujo y
    /// ya se aplicó al elegir la ventana original. Volver a sumarlo en cada corrida lo convertiría
    /// en una deriva de minutos/día, y para eso existe.</para>
    /// </summary>
    public static DateTime CalcularProximaEjecucion(DateTime ahora, int intervaloSegundos)
        => ahora.AddSeconds(intervaloSegundos);

    /// <summary>
    /// ¿Toca correr a este flujo en <paramref name="ahora"/>?
    /// <para>Solo mira <see cref="ErpFlujo.ProximaEjecucionUtc"/>, porque es lo que el reclamo
    /// atómico dejó escrito. Que el motor rederive aquí el «¿toca?» a partir del intervalo se
    /// duplicaría, y las dos copias discreparían en el instante de la carrera entre dos
    /// instancias.</para>
    /// </summary>
    public static bool Toca(DateTime ahora, DateTime? proximaEjecucionUtc)
        => proximaEjecucionUtc is not null && proximaEjecucionUtc.Value <= ahora;

    /// <summary>¿Este flujo está en condiciones de que el motor lo toque?</summary>
    /// <para><b>El estado que se mira es BORRADOR y no ACTIVO, y esa diferencia es el motor
    /// entero.</b> Filtrar por «activo» suena razonable y está mal: en cuanto una corrida falla,
    /// el flujo pasa a <c>CON_ERROR</c> y saldría del ciclo para siempre, con lo cual un ERP que
    /// se cae un minuto no vuelve a sincronizarse solo y el corte de fallos seguidos —que existe
    /// justamente para eso— nunca llega. <c>CON_ERROR</c> no significa «no lo toques», significa
    /// «la última corrida falló», que es un dato para el que lo mira, no una orden para el
    /// planificador. La única orden real es <c>BORRADOR</c>, que significa «todavía no lo
    /// configures».</para>
    /// </summary>
    public static bool EsEjecutable(ErpFlujo flujo)
        => !flujo.EsPausado
           && flujo.Estado != IntegracionCatalogos.EstadoFlujoBorrador
           && flujo.IntervaloSegundos is not null
           && flujo.TipoDisparador != IntegracionCatalogos.TipoDisparadorManual;

    /// <summary>
    /// ¿Se alcanzó el corte de fallos y hay que pausar?
    /// <para>Este es el cortocircuito del motor contra sí mismo. Sin él, un ERP caído produce un
    /// intento fallido cada 10 segundos, para siempre, y la única forma de detenerlo es que alguien
    /// se dé cuenta. Con él, el motor se detiene solo y deja el motivo escrito.</para>
    /// </summary>
    public static bool DebePausar(short fallosConsecutivos, short maximoFallos)
        => maximoFallos > 0 && fallosConsecutivos >= maximoFallos;

    /// <summary>El motivo que se escribe en <c>MotivoPausa</c> y se muestra en la pantalla.</summary>
    public static string MotivoPausaPorFallos(short fallosConsecutivos, short maximoFallos)
        => $"Pausado automáticamente: {fallosConsecutivos} corridas fallidas seguidas. " +
           "El flujo no se volverá a tocar hasta que alguien lo reanude desde la pantalla de orígenes.";

    /// <summary>
    /// Intervalo de espera entre vueltas del motor.
    /// <para>No es el intervalo del flujo. El motor despierta cada 5 segundos, mira qué se venció y
    /// se vuelve a dormir. Es más frecuente que casi todos los intervalos configurados, y esa
    /// asimetría es lo que permite que un flujo de 10 segundos se cumpla: si el motor despertara
    /// cada 5 minutos, ningún intervalo por debajo de eso existiría.</para>
    /// <para>El costo de esta asimetría es una consulta por vuelta, y por eso hay índice
    /// filtrado: sobre las dos o tres filas de <c>int.ErpFlujo</c> de hoy es nada, y sobre las
    /// que habrá cuando esto esté en producción es una búsqueda, no un recorrido.</para>
    /// </summary>
    public const int EsperaEntreVueltasSegundos = 5;
}
