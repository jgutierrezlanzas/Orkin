using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Domain.Integracion;

namespace Orkin.Cotizador.Tests.Carga;

/// <summary>
/// Pruebas de las reglas de programación (<c>§12.11</c>).
/// <para>Son funciones puras y por eso no levantan ni una base: el motor de sincronización
/// depende de que estas reglas no cambien de opinión, y una regla que se puede comprobar con una
/// línea de <c>Assert</c> no debería costar un segundo de arranque de SQL Server para
/// verificarla.</para>
/// <para>Las que de verdad importan son las de <see cref="PlanificadorSincronizacion.EsEjecutable"/>
/// y <see cref="PlanificadorSincronizacion.DebePausar"/>, porque las dos son cortocircuitos y un
/// cortocircuito mal escrito no da error: da un motor que se calla. El primero se "*corrigió*" mal
/// una vez —filtraba por estado activo, con lo que un flujo que fallaba una vez salía del ciclo
/// para siempre y el ERP caído no volvía solo— y la forma de que eso no vuelva a pasar es una
/// prueba que diga por qué.</para>
/// </summary>
public sealed class PlanificadorSincronizacionTests
{
    private static readonly DateTime Ahora = new(2026, 9, 29, 19, 0, 0, DateTimeKind.Utc);

    private static ErpFlujo Flujo(byte tipoDisparador, int? intervalo, byte estado, bool pausado = false) => new()
    {
        TipoDisparador = tipoDisparador,
        IntervaloSegundos = intervalo,
        Estado = estado,
        EsPausado = pausado,
        MaximoFallosConsecutivos = 5
    };

    // ── Próxima ejecución ─────────────────────────────────────────────────────

    [Fact]
    public void La_proxima_ejecucion_se_cuenta_desde_ahora_y_no_desde_cuando_vencio()
    {
        var resultado = PlanificadorSincronizacion.CalcularProximaEjecucion(Ahora, intervaloSegundos: 300);

        Assert.Equal(Ahora.AddSeconds(300), resultado);
    }

    [Fact]
    public void La_ventana_nunca_se_acumula_tras_un_retraso()
    {
        // El motor estuvo ocupado 10 minutos con otro flujo. Si la próxima ejecución se calculara
        // desde el instante en que venció, este flujo tendría que recupear de golpe las 60
        // corridas que se perdió, y todas le pegarían al ERP del cliente de una sola vez.
        var vencidoHaceMucho = Ahora.AddMinutes(-10);

        var resultado = PlanificadorSincronizacion.CalcularProximaEjecucion(Ahora, intervaloSegundos: 10);

        Assert.Equal(Ahora.AddSeconds(10), resultado);
        Assert.True(resultado > vencidoHaceMucho);
    }

    [Theory]
    [InlineData(null, false)]   // nunca programado
    [InlineData(true, true)]     // vencido
    [InlineData(false, false)]   // todavía no
    public void Solo_toca_cuando_la_ventana_ya_paso(bool? vencida, bool esperado)
    {
        DateTime? proxima = vencida switch
        {
            true => Ahora.AddSeconds(-1),
            false => Ahora.AddSeconds(60),
            _ => null
        };

        Assert.Equal(esperado, PlanificadorSincronizacion.Toca(Ahora, proxima));
    }

    // ── Ejecutabilidad ────────────────────────────────────────────────────────

    [Fact]
    public void Un_flujo_en_error_sigue_siendo_ejecutable_porque_eso_es_lo_que_lo_hace_recuperarse()
    {
        // Esta es la regresión que motivó la prueba. El motor filtraba por «activo», de modo que
        // en cuanto una corrida fallaba el flujo quedaba en CON_ERROR y salía del ciclo para
        // siempre: un ERP que se caía un minuto no volvía a sincronizarse solo, y el corte de
        // fallos seguidos —que existe justo para eso— nunca llegaba a activarse.
        var flujo = Flujo(IntegracionCatalogos.TipoDisparadorProgramado, 10,
            IntegracionCatalogos.EstadoFlujoConError);

        Assert.True(PlanificadorSincronizacion.EsEjecutable(flujo),
            "CON_ERROR describe la última corrida, no una orden de no tocar el flujo.");
    }

    [Theory]
    [InlineData(IntegracionCatalogos.EstadoFlujoBorrador, false, "borrador")]
    [InlineData(IntegracionCatalogos.EstadoFlujoActivo, true, "activo")]
    [InlineData(IntegracionCatalogos.EstadoFlujoConError, true, "con error")]
    public void Solo_el_borrador_significa_no_tocar_el_flujo(byte estado, bool esperado, string _)
    {
        var flujo = Flujo(IntegracionCatalogos.TipoDisparadorProgramado, 10, estado);

        Assert.Equal(esperado, PlanificadorSincronizacion.EsEjecutable(flujo));
    }

    [Fact]
    public void Un_flujo_pausado_no_se_toca_aunque_tenga_ventana()
    {
        var flujo = Flujo(IntegracionCatalogos.TipoDisparadorProgramado, 10,
            IntegracionCatalogos.EstadoFlujoActivo, pausado: true);

        Assert.False(PlanificadorSincronizacion.EsEjecutable(flujo));
    }

    [Fact]
    public void Un_flujo_manual_no_se_toca_aunque_tenga_ventana_puesta()
    {
        // La ventana es null en un flujo manual, pero el filtro no puede depender solo de eso: si
        // alguien guarda la programación a medio camino y la ventana queda puesta, el motor
        // arranca un flujo que el usuario dejó manual a propósito.
        var flujo = Flujo(IntegracionCatalogos.TipoDisparadorManual, 10,
            IntegracionCatalogos.EstadoFlujoActivo);

        Assert.False(PlanificadorSincronizacion.EsEjecutable(flujo));
    }

    [Fact]
    public void Un_flujo_sin_intervalo_no_se_toca()
    {
        var flujo = Flujo(IntegracionCatalogos.TipoDisparadorProgramado, null,
            IntegracionCatalogos.EstadoFlujoActivo);

        Assert.False(PlanificadorSincronizacion.EsEjecutable(flujo));
    }

    // ── Corte de fallos ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 5, false)]
    [InlineData(4, 5, false)]
    [InlineData(5, 5, true)]
    [InlineData(9, 5, true)]
    public void El_corte_se_alcanza_al_llegar_al_maximo(short fallos, short maximo, bool esperado)
    {
        Assert.Equal(esperado, PlanificadorSincronizacion.DebePausar(fallos, maximo));
    }

    [Fact]
    public void Un_maximo_de_cero_desactiva_el_corte()
    {
        // Cero no significa «pausar en el primer fallo»: significa «no pausar nunca», que es lo
        // que quiere alguien que prefiere enterarse por el log de que su ERP lleva caído una hora
        // en vez de que el flujo se detenga solo sin que nadie lo note.
        Assert.False(PlanificadorSincronizacion.DebePausar(fallosConsecutivos: 500, maximoFallos: 0));
    }

    [Fact]
    public void El_motivo_de_la_pausa_dice_cuantas_veces_fallo()
    {
        var motivo = PlanificadorSincronizacion.MotivoPausaPorFallos(5, 5);

        Assert.Contains("5", motivo);
        Assert.Contains("reanude", motivo);
    }

    // ── Alquiler de corrida ───────────────────────────────────────────────────

    [Fact]
    public void Un_flujo_sin_marca_esta_libre_para_correrlo_a_mano()
    {
        var flujo = new ErpFlujo { CorridaEnCursoUtc = null };

        Assert.False(PlanificadorSincronizacion.TieneCorridaEnCurso(
            flujo, Ahora, PlanificadorSincronizacion.ToleranciaCorridaEnCurso));
    }

    [Fact]
    public void Un_flujo_con_la_marca_puesta_no_se_puede_correr_en_paralelo()
    {
        // Esta es la carrera que motiva el alquiler. Un flujo programado puede tener la ventana
        // todavía en el futuro y estar corriendo ahora mismo —la corrida a mano no le adelanta la
        // ventana porque no le tocaba—, así que la ventana no alcanza para saber si hay alguien
        // dentro. Sin la marca, un «Ejecutar» pulsado en ese instante carga el mismo catálogo dos
        // veces contra el mismo ERP.
        var flujo = new ErpFlujo { CorridaEnCursoUtc = Ahora.AddSeconds(-2) };

        Assert.True(PlanificadorSincronizacion.TieneCorridaEnCurso(
            flujo, Ahora, PlanificadorSincronizacion.ToleranciaCorridaEnCurso));
    }

    [Fact]
    public void Una_marca_caducada_libera_el_flujo_porque_la_corrida_se_dio_por_muerta()
    {
        // El caso contrario y el que no se puede desatender: un proceso que se muere a media carga
        // no suelta la marca. Sin caducidad, ese flujo no volvería a correr nunca —la última
        // corrida seguiría en pantalla y el catálogo quedaría viejo sin que nada lo delate—.
        var tolerancia = PlanificadorSincronizacion.ToleranciaCorridaEnCurso;
        var flujo = new ErpFlujo { CorridaEnCursoUtc = Ahora - tolerancia - TimeSpan.FromSeconds(1) };

        Assert.False(PlanificadorSincronizacion.TieneCorridaEnCurso(flujo, Ahora, tolerancia));
    }

    [Fact]
    public void La_caducidad_del_alquiler_esta_a_salvo_de_una_corrida_lenta()
    {
        // Una corrida de verdad puede tardar lo que tarda el ERP del cliente, y ese tiempo no lo
        // controlamos. Con un corte corto, una lectura lenta de una vista grande perdería su
        // alquiler a mitad y el motor se metería encima. El plazo tiene que ser holgado; lo que
        // cuesta esperar quince minutos después de una caída es mucho menor que pisar una carga.
        Assert.True(PlanificadorSincronizacion.ToleranciaCorridaEnCurso >= TimeSpan.FromMinutes(10),
            "Con menos de diez minutos, una corrida lenta de verdad perdería el alquiler a mitad.");
    }

    // ── Qué cuenta como cambio ────────────────────────────────────────────────

    [Fact]
    public void Una_carga_solo_omitidas_no_es_un_cambio()
    {
        // Es el caso que decide si la corrida programada deja fila en la bitácora. Con 111
        // productos que no cambiaron, la respuesta tiene que ser «no», o la bitácora recibe
        // miles de filas por día que dicen «nada pasó».
        var resultado = new ResultadoCarga
        {
            Catalogo = "PRODUCTOS",
            TipoOrigen = "SQL_SERVER",
            Omitidos = 111
        };

        Assert.False(resultado.HayCambios);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 0, 1)]
    public void Insertar_actualizar_o_rechazar_cuentan_como_cambio(int creados, int actualizados, int rechazados)
    {
        var resultado = new ResultadoCarga
        {
            Catalogo = "PRODUCTOS",
            TipoOrigen = "SQL_SERVER",
            Omitidos = 110,
            Insertados = creados,
            Actualizados = actualizados,
            Rechazados = rechazados
        };

        Assert.True(resultado.HayCambios);
    }
}
