using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Core.Domain.Integracion;

/// <summary>
/// Las decisiones que el motor de programación toma sobre un flujo: cuándo le toca de nuevo, si
/// hay que pausarlo, si su corrida merece fila en la bitácora, y si ya hay alguien corriéndolo.
/// <para>Están en una clase estática y sin base de datos a propósito. Son reglas, no I/O, y lo
/// difícil de este motor no es que ejecute la carga —eso ya lo sabe hacer
/// <c>ProcesoCargaService</c>— sino que no repita trabajo ni golpee de más el ERP de un cliente.
/// Merecen poder probarse sin levantar un ERP falso, y una prueba que necesita una base para
/// comprobar una suma no está probando la suma.</para>
/// </summary>
public static class PlanificadorSincronizacion
{
    /// <summary>
    /// Cuánto puede seguir marcada una corrida antes de que el alquiler se considere caducado y
    /// otro proceso pueda tomárselo.
    /// <para>Es una red de seguridad, no una reserva de tiempo: una corrida dura lo que dura la
    /// lectura del ERP del cliente, y ese tiempo no lo controlamos. Si el proceso se muere a media
    /// carga —un despliegue, un <c>kill</c>— la marca se queda puesta y, sin caducidad, el flujo no
    /// volvería a correr nunca. Nadie se enteraría: la última corrida exitosa seguiría en pantalla
    /// y el catálogo se quedaría viejo para siempre.</para>
    /// <para>Por eso es holgada a propósito. Preferible que un flujo espere quince minutos tras
    /// una caída a que dos corridas se pisen: la primera deja rastro visible —un fallo en la
    /// bitácora— y el motor se recupera solo; la segunda no deja nada.</para>
    /// </summary>
    public static readonly TimeSpan ToleranciaCorridaEnCurso = TimeSpan.FromMinutes(15);
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
    /// ¿Hay una corrida en curso sobre este flujo?
    /// <para>Es la misma pregunta que responden los dos <c>UPDATE</c> que escriben el alquiler —el
    /// reclamo del motor y la reserva de la corrida a mano—, escrita una vez aquí para que las tres
    /// copias no puedan separarse. La condición del SQL y esta función tienen que decir
    /// exactamente lo mismo: si el SQL es más laxo, dos corridas se pisan; si es más estricto, un
    /// flujo queda marcado para siempre.</para>
    /// <para>La caducidad es lo que hace que la marca sea un alquiler y no una lápida. Un proceso
    /// que se muere a media carga no suelta la marca, y sin caducidad ese flujo no volvería a
    /// correr nunca: la última corrida seguiría en pantalla y el catálogo se quedaría viejo sin
    /// que nada lo delate. Pasado el plazo se la da por muerta y el flujo vuelve a estar
    /// disponible.</para>
    /// <para>No se pregunta si el flujo está dado de baja. La baja no limpia la marca a propósito:
    /// si alguien dio de baja un flujo que se estaba sincronizando, esa sincronización termina
    /// igual —ya había empezado a leer el ERP— y lo que debe impedir la baja es empezar de nuevo,
    /// no dejar una corrida a medias.</para>
    /// </summary>
    public static bool TieneCorridaEnCurso(ErpFlujo flujo, DateTime ahora, TimeSpan tolerancia)
        => flujo.CorridaEnCursoUtc is not null
           && flujo.CorridaEnCursoUtc.Value > ahora - tolerancia;

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
