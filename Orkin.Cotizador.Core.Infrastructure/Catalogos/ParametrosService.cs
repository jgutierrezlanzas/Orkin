using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Domain.Sistema;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Infrastructure.Catalogos;

/// <summary>
/// Lee <c>dbo.ParametroGeneral</c> y lo devuelve ya convertido al tipo que declara
/// <c>dbo.TipoDatoParametroCatalogo</c>.
/// <para><b>Por qué es singleton y abre su propio scope:</b> los parámetros se leen en bucles —una
/// vez por línea de cotización, por ejemplo— y no tiene sentido abrir un contexto por lectura. La
/// caché se invalida explícitamente cuando Administración guarda un cambio, así que no hace falta
/// recargar la base por cada lectura ni reiniciar el proceso.</para>
/// <para><b>Por qué cada getter exige que el tipo declarado coincida con el que devuelve:</b> si
/// alguien cambia <c>APROBACION.MONTO_MAXIMO_SIN_APROBACION</c> de <c>DECIMAL</c> a <c>TEXTO</c> en
/// Administración, <c>DecimalAsync</c> tiene que fallar en el punto de la lectura y no devolver
/// <c>null</c>. Un <c>null</c> silencioso se traduce en "no hay monto máximo", que es justo la
/// configuración que deja pasar todas las aprobaciones.</para>
/// <para><b>Por qué los códigos de <c>TipoDatoParametroCatalogo</c> están declarados acá:</b> no es
/// un catálogo quemado, es el contrato entre la fila de la base y las conversiones de esta clase, en
/// el mismo sentido que <c>CodigosMetodoCosteo</c> lo es entre <c>MetodoCosteoCatalogo</c> y
/// <c>IMetodoCosteo</c>. El conjunto completo de tipos vive en la base; lo que el código necesita es
/// poder decirle "a este código lo convierte esta rama". Si la base gana un tipo que esta clase no
/// conoce, la conversión falla con un mensaje explícito en vez de devolver el texto crudo.</para>
/// </summary>
public sealed class ParametrosService : IParametros
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ParametrosService> _logger;

    private readonly ConcurrentDictionary<string, ParametroCrudo> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <c>TipoDatoId</c> → <c>Codigo</c> de <c>dbo.TipoDatoParametroCatalogo</c>. Se llena una vez
    /// por proceso leyendo la tabla: el mapeo no se compila, así que cambiar el orden de inserción
    /// de las filas del catálogo no rompe nada.
    /// </summary>
    private static readonly ConcurrentDictionary<byte, string> TiposPorId = new();

    public ParametrosService(IServiceScopeFactory scopeFactory, ILogger<ParametrosService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── Lecturas tipadas ──────────────────────────────────────────────────────────

    public async Task<string?> TextoAsync(string clave, CancellationToken cancellationToken = default)
    {
        var p = await LeerAsync(clave, cancellationToken);

        if (p is null)
        {
            return null;
        }

        ExigirTipo(clave, p, CodigosTipoDatoParametro.Texto);
        return p.Valor;
    }

    public async Task<int?> EnteroAsync(string clave, CancellationToken cancellationToken = default)
    {
        var p = await LeerAsync(clave, cancellationToken);

        if (p is null)
        {
            return null;
        }

        ExigirTipo(clave, p, CodigosTipoDatoParametro.Entero);

        if (!int.TryParse(p.Valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor))
        {
            throw ErrorDeConversion(clave, p, "entero");
        }

        return valor;
    }

    public async Task<decimal?> DecimalAsync(string clave, CancellationToken cancellationToken = default)
    {
        var p = await LeerAsync(clave, cancellationToken);

        if (p is null)
        {
            return null;
        }

        ExigirTipo(clave, p, CodigosTipoDatoParametro.Decimal);

        if (!decimal.TryParse(p.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
        {
            throw ErrorDeConversion(clave, p, "decimal");
        }

        return valor;
    }

    public async Task<bool?> LogicoAsync(string clave, CancellationToken cancellationToken = default)
    {
        var p = await LeerAsync(clave, cancellationToken);

        if (p is null)
        {
            return null;
        }

        ExigirTipo(clave, p, CodigosTipoDatoParametro.Logico);

        var texto = (p.Valor ?? string.Empty).Trim();

        if (texto is "1" or "si" or "sí" or "true" or "verdadero")
        {
            return true;
        }

        if (texto is "0" or "no" or "false" or "falso")
        {
            return false;
        }

        throw ErrorDeConversion(clave, p, "lógico (1/0, si/no, true/false, verdadero/falso)");
    }

    public async Task<DateOnly?> FechaAsync(string clave, CancellationToken cancellationToken = default)
    {
        var p = await LeerAsync(clave, cancellationToken);

        if (p is null)
        {
            return null;
        }

        ExigirTipo(clave, p, CodigosTipoDatoParametro.Fecha);

        if (!DateOnly.TryParseExact(
                p.Valor,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var valor))
        {
            // Se exige el formato ISO y no el parseo de cualquier cultura: un parámetro escrito a
            // mano como "05/03/2026" es ambiguo entre marzo y mayo, y esa ambigüedad decidirla sola
            // es la peor forma de decidirla.
            throw ErrorDeConversion(clave, p, "fecha ISO yyyy-MM-dd");
        }

        return valor;
    }

    public async Task<JsonElement?> JsonAsync(string clave, CancellationToken cancellationToken = default)
    {
        var p = await LeerAsync(clave, cancellationToken);

        if (p is null)
        {
            return null;
        }

        ExigirTipo(clave, p, CodigosTipoDatoParametro.Json);

        if (string.IsNullOrWhiteSpace(p.Valor))
        {
            return null;
        }

        try
        {
            // Clone es obligatorio: el elemento que devuelve el parser queda ligado al
            // JsonDocument, que se invalida al salir de este método. Guardarlo sin clonar produce
            // un fallo diferido, y el punto donde revienta no es el punto donde falló.
            using var documento = JsonDocument.Parse(p.Valor);
            return documento.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ParametroInvalidoException(
                clave,
                $"está declarado como {CodigosTipoDatoParametro.Json} pero su valor no es JSON " +
                $"bien formado: {ex.Message}");
        }
    }

    public async Task<decimal> DecimalRequeridoAsync(string clave, CancellationToken cancellationToken = default)
    {
        var p = await LeerAsync(clave, cancellationToken);

        if (p is null)
        {
            throw new ParametroInvalidoException(
                clave,
                "se pidió como obligatorio y no existe. No hay valor por defecto a propósito: " +
                "agréguelo en Administración o en dbo.ParametroGeneral antes de seguir.");
        }

        ExigirTipo(clave, p, CodigosTipoDatoParametro.Decimal);

        if (!decimal.TryParse(p.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
        {
            throw ErrorDeConversion(clave, p, "decimal");
        }

        return valor;
    }

    public Task InvalidarCacheAsync(string? clave = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            var cantidad = _cache.Count;
            _cache.Clear();

            _logger.LogInformation(
                "Caché de parámetros invalidada por completo: {Cantidad} entradas.", cantidad);
        }
        else if (_cache.TryRemove(clave, out _))
        {
            _logger.LogInformation("Parámetro '{Clave}' invalidado de la caché.", clave);
        }

        return Task.CompletedTask;
    }

    // ── Interno ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve la fila cruda, o <c>null</c> si el parámetro no existe. Cargar la tabla entera una
    /// sola vez y cachearla es lo correcto aquí: son veinte filas y se leen de todas formas.
    /// </summary>
    private async Task<ParametroCrudo?> LeerAsync(string clave, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);

        if (_cache.TryGetValue(clave, out var crudo))
        {
            return crudo;
        }

        await AsegurarTiposCargadosAsync(cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CotizadorDbContext>();

        var filas = await context.ParametroGeneral
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var fila in filas)
        {
            _cache[fila.Nombre] = new ParametroCrudo(fila.Valor, fila.TipoDatoId);
        }

        _logger.LogInformation(
            "Parámetros cargados: {Cantidad} filas de dbo.ParametroGeneral.", filas.Count);

        return _cache.TryGetValue(clave, out crudo) ? crudo : null;
    }

    /// <summary>
    /// Carga el mapeo <c>TipoDatoId → Codigo</c> del catálogo de tipos de dato, una sola vez por
    /// proceso. Si la tabla viniera vacía, <see cref="ExigirTipo"/> no puede validar nada y todos los
    /// parámetros fallarían con un mensaje incomprensible; por eso se avisa con nombre de tabla.
    /// </summary>
    private async Task AsegurarTiposCargadosAsync(CancellationToken cancellationToken)
    {
        if (TiposPorId.Count > 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CotizadorDbContext>();

        var tipos = await context.TipoDatoParametroCatalogo
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (tipos.Count == 0)
        {
            throw new ParametroInvalidoException(
                "dbo.TipoDatoParametroCatalogo",
                "está vacía. Sin ella ningún parámetro se puede convertir, porque la columna " +
                "TipoDatoId de dbo.ParametroGeneral no apunta a nada. Corra la siembra " +
                "(RUN_SEED=true) o revise que el catálogo se haya cargado.");
        }

        foreach (var tipo in tipos)
        {
            TiposPorId[tipo.Id] = tipo.Codigo;
        }

        _logger.LogInformation(
            "Tipos de dato de parámetro cargados: {Cantidad} tipos de dbo.TipoDatoParametroCatalogo.",
            tipos.Count);
    }

    private static void ExigirTipo(string clave, ParametroCrudo p, string tipoEsperado)
    {
        if (!TiposPorId.TryGetValue(p.TipoDatoId, out var declarado))
        {
            throw new ParametroInvalidoException(
                clave,
                $"su TipoDatoId = {p.TipoDatoId} no corresponde a ninguna fila activa de " +
                "dbo.TipoDatoParametroCatalogo.");
        }

        if (!string.Equals(declarado, tipoEsperado, StringComparison.Ordinal))
        {
            throw new ParametroInvalidoException(
                clave,
                $"está declarado como {declarado} pero se pidió como {tipoEsperado}. Cambiar el " +
                "tipo de un parámetro rompe a quien ya lo leía: corrige el tipo en " +
                "dbo.ParametroGeneral o usa el getter que corresponde.");
        }
    }

    private static ParametroInvalidoException ErrorDeConversion(string clave, ParametroCrudo p, string tipo)
    {
        return new ParametroInvalidoException(
            clave,
            $"no se pudo convertir a {tipo}. Valor leído: '{p.Valor ?? "(vacío)"}'. Se convierte con " +
            "cultura invariante, así que el decimal lleva punto y no coma.");
    }

    /// <summary>La fila del parámetro tal como está en la base, sin interpretar.</summary>
    private sealed record ParametroCrudo(string? Valor, byte TipoDatoId);

    /// <summary>
    /// Contrato con las filas de <c>dbo.TipoDatoParametroCatalogo</c>. Mismo criterio que
    /// <c>CodigosMetodoCosteo</c>: el conjunto de tipos vive en la base, y lo que el código declara
    /// es con qué código conversa cada rama de conversión.
    /// </summary>
    private static class CodigosTipoDatoParametro
    {
        public const string Texto = "TEXTO";
        public const string Entero = "ENTERO";
        public const string Decimal = "DECIMAL";
        public const string Logico = "LOGICO";
        public const string Fecha = "FECHA";
        public const string Json = "JSON";
    }
}

/// <summary>
/// Falla al leer o interpretar un parámetro. Es una excepción y no un <c>null</c> devuelto a
/// propósito: todos los parámetros de este sistema existen porque alguien decidió que ese valor
/// tenga efecto. Devolver <c>null</c> y seguir convertiría "no configurado" en "todo permitido".
/// </summary>
public sealed class ParametroInvalidoException : Exception
{
    public ParametroInvalidoException(string clave, string motivo)
        : base($"El parámetro '{clave}' {motivo}")
    {
        Clave = clave;
    }

    /// <summary>Nombre del parámetro, o el de la tabla del catálogo si el problema fue el catálogo.</summary>
    public string Clave { get; }
}
