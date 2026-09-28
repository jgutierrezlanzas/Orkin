using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.MotorCalculo;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Infrastructure.MotorCalculo;

/// <summary>
/// Arma la <see cref="ConfiguracionMotor"/> leyendo <c>dbo.MetodoCosteoCatalogo</c>,
/// <c>dbo.SemestreCatalogo</c> y <c>dbo.TipoClienteCatalogo</c> una sola vez al arrancar.
/// <para><b>Esta clase es el contrato de ADR-018 hecho código.</b> El motor de cálculo
/// (<c>CotizadorCalculo</c>) no sabe qué es "residencial" ni qué es "primer semestre": recibe ids y
/// estrategias ya resueltas. Todo el conocimiento de qué filas existen se concentra aquí, y todas
/// las formas de equivocarse con esa información terminan en un mensaje de arranque.</para>
/// <para><b>Por qué lee la base directamente en vez de usar <c>ICatalogoResolver</c>:</b> este
/// servicio corre una vez, antes de que el proceso acepte peticiones, y necesita los <c>Id</c> de
/// <c>ValorCatalogo</c> no trae—. Abrir un scope propio y una consulta es más directo que ir
/// resolviendo de a uno, y aquí no hay nada que valga la pena cachear.</para>
/// <para><b>Por qué valida en las dos direcciones:</b> una fila de catálogo sin implementación es un
/// fallo de arranque; una implementación sin fila es un aviso, porque puede ser una clase escrita
/// para un valor que todavía nadie ha sembrado.</para>
/// </summary>
public sealed class ConstructorConfiguracionMotor : IConstructorConfiguracionMotor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConstructorConfiguracionMotor> _logger;

    /// <summary>
    /// Las implementaciones de <see cref="IMetodoCosteo"/>, una por clase. Agregar un método de
    /// costeo es <b>una clase acá más una fila en <c>dbo.MetodoCosteoCatalogo</c></b>; si falta
    /// cualquiera de las dos, el arranque lo dice por su nombre.
    /// </summary>
    private static readonly IMetodoCosteo[] Implementaciones =
    [
        new MetodoCosteoSumaSimple(),
        new MetodoCosteoPromedioSemestres()
    ];

    public ConstructorConfiguracionMotor(
        IServiceScopeFactory scopeFactory,
        ILogger<ConstructorConfiguracionMotor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<ConfiguracionMotor> ConstruirAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CotizadorDbContext>();

        var semestres = await ResolverSemestresAsync(context, cancellationToken);
        var metodos = await ResolverMetodosAsync(context, cancellationToken);
        var porSegmento = await ResolverSegmentosAsync(context, metodos, cancellationToken);

        _logger.LogInformation(
            "Configuración del motor lista: {CantidadMetodos} métodos de costeo, {CantidadSegmentos} " +
            "segmentos de cliente y semestres {PrimerSemestre}/{SegundoSemestre}.",
            metodos.Count,
            porSegmento.Count,
            semestres.PrimerSemestreId,
            semestres.SegundoSemestreId);

        return new ConfiguracionMotor
        {
            MetodosPorId = metodos,
            Semestres = semestres,
            MetodoPorSegmento = porSegmento
        };
    }

    // ── dbo.SemestreCatalogo ──────────────────────────────────────────────────────

    private static async Task<SemestresResueltos> ResolverSemestresAsync(
        CotizadorDbContext context,
        CancellationToken cancellationToken)
    {
        var activos = await context.SemestreCatalogo
            .AsNoTracking()
            .Where(s => s.EsActivo)
            .OrderBy(s => s.Orden)
            .ToListAsync(cancellationToken);

        // Exacto dos, ni uno más. Un tercer semestre activo haría que Clasificar() tuviera que
        // adivinar a cuál pertenece una línea que no dice, y ese "adivinar" sería un default
        // silencioso en la única parte del motor que no admite silencios.
        if (activos.Count != 2)
        {
            throw new InvalidOperationException(
                $"dbo.SemestreCatalogo tiene {activos.Count} valores activos y deben ser exactamente 2 " +
                $"({CodigosSemestre.Primero} y {CodigosSemestre.Segundo}). Revise el seed " +
                "scripts/seed/dbo.SemestreCatalogo.json o desactive el que sobre.");
        }

        var primero = Buscar(activos, CodigosSemestre.Primero);
        var segundo = Buscar(activos, CodigosSemestre.Segundo);

        return new SemestresResueltos(primero.Id, segundo.Id);
    }

    private static SemestreCatalogo Buscar(IReadOnlyList<SemestreCatalogo> filas, string codigo)
        => filas.FirstOrDefault(f => string.Equals(f.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException(
               $"Falta la fila '{codigo}' en dbo.SemestreCatalogo. Está activa en el catálogo pero no " +
               "aparece entre los valores activos leídos de la base.");

    // ── dbo.MetodoCosteoCatalogo ──────────────────────────────────────────────────

    private async Task<Dictionary<byte, IMetodoCosteo>> ResolverMetodosAsync(
        CotizadorDbContext context,
        CancellationToken cancellationToken)
    {
        var activas = await context.MetodoCosteoCatalogo
            .AsNoTracking()
            .Where(m => m.EsActivo)
            .ToListAsync(cancellationToken);

        if (activas.Count == 0)
        {
            throw new InvalidOperationException(
                "dbo.MetodoCosteoCatalogo no tiene ningún valor activo. Sin ella el motor no puede " +
                "calcular nada. Corra la siembra (RUN_SEED=true) antes de levantar la aplicación.");
        }

        var porCodigo = Implementaciones.ToDictionary(i => i.Codigo, StringComparer.OrdinalIgnoreCase);
        var porId = new Dictionary<byte, IMetodoCosteo>();

        foreach (var fila in activas)
        {
            if (!porCodigo.TryGetValue(fila.Codigo, out var implementacion))
            {
                throw new InvalidOperationException(
                    $"La fila '{fila.Codigo}' de dbo.MetodoCosteoCatalogo no tiene implementación en " +
                    "Core.Domain/MotorCalculo. Agregue una clase que implemente IMetodoCosteo y " +
                    "devuelva ese Codigo, o quite la fila del seed. No se arranca calculando con " +
                    "otro método en silencio.");
            }

            porId[fila.Id] = implementacion;
        }

        // La dirección inversa no es un error: una clase puede existir para un valor que alguien
        // todavía no ha sembrado. Se avisa para que el código muerto sea visible en el log.
        var huerfanas = porCodigo.Keys.Where(c => activas.All(f =>
            !string.Equals(f.Codigo, c, StringComparison.OrdinalIgnoreCase))).ToArray();

        if (huerfanas.Length > 0)
        {
            _logger.LogWarning(
                "Hay {Cantidad} implementación(es) de IMetodoCosteo sin fila en " +
                "dbo.MetodoCosteoCatalogo: {Codigos}. El motor no las puede usar.",
                huerfanas.Length,
                string.Join(", ", huerfanas));
        }

        return porId;
    }

    // ── dbo.TipoClienteCatalogo ───────────────────────────────────────────────────

    private static async Task<Dictionary<byte, byte>> ResolverSegmentosAsync(
        CotizadorDbContext context,
        IReadOnlyDictionary<byte, IMetodoCosteo> metodos,
        CancellationToken cancellationToken)
    {
        var segmentos = await context.TipoClienteCatalogo
            .AsNoTracking()
            .Where(t => t.EsActivo)
            .ToListAsync(cancellationToken);

        if (segmentos.Count == 0)
        {
            throw new InvalidOperationException(
                "dbo.TipoClienteCatalogo no tiene ningún valor activo. Sin segmentos el motor no " +
                "sabría qué método de costeo aplicarle a un cliente.");
        }

        var porSegmento = new Dictionary<byte, byte>();

        foreach (var segmento in segmentos)
        {
            if (!metodos.ContainsKey(segmento.MetodoCosteoId))
            {
                throw new InvalidOperationException(
                    $"El segmento '{segmento.Codigo}' de dbo.TipoClienteCatalogo apunta a " +
                    $"MetodoCosteoId = {segmento.MetodoCosteoId}, y ese método no está en el " +
                    "catálogo activo o no tiene implementación. Corrija el seed.");
            }

            porSegmento[segmento.Id] = segmento.MetodoCosteoId;
        }

        return porSegmento;
    }

    /// <summary>
    /// Contrato con las filas de <c>dbo.SemestreCatalogo</c>, igual que
    /// <c>CodigosMetodoCosteo</c> lo es con <c>dbo.MetodoCosteoCatalogo</c>: no es una lista de
    /// valores permitidos, es el nombre con el que estas conversiones hablan con la tabla. El
    /// conjunto completo de semestres vive en la base.
    /// </summary>
    private static class CodigosSemestre
    {
        public const string Primero = "PRIMERO";

        public const string Segundo = "SEGUNDO";
    }
}
