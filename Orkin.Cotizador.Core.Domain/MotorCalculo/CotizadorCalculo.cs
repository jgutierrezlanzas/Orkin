using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Orkin.Cotizador.Core.Domain.Cotizacion;

namespace Orkin.Cotizador.Core.Domain.MotorCalculo;

/// <summary>
/// Motor de cálculo. Código de dominio puro: <b>sin dependencias</b> (ADR-005). No sabe qué es
/// EF Core, ni SQL, ni HTTP. Recibe la entrada completa y devuelve el resultado completo.
/// <para>Esa es la razón por la que los casos *golden master* contra el Excel son posibles: el
/// motor se puede ejercitar sin base de datos.</para>
/// <para>⚠️ <b>ADR-018: aquí no hay catálogos.</b> El motor no sabe qué es "residencial" ni qué
/// estados existen. Recibe un <see cref="MetodoCosteoId"/> ya resuelto y las implementaciones de
/// <see cref="IMetodoCosteo"/>, que arma la capa de infraestructura leyendo
/// <c>dbo.MetodoCosteoCatalogo</c> al arrancar.</para>
/// </summary>
public interface ICotizadorCalculo
{
    /// <summary>Aplica RN-01 a RN-08 y devuelve los totales con su hash.</summary>
    /// <exception cref="CalculoInvalidoException">Si la entrada viola una regla de negocio.</exception>
    ResultadoCalculo Calcular(EntradaCalculo entrada);
}

/// <summary>
/// Implementación del motor. Traduce **una a una** las fórmulas del Excel, con sus correcciones.
/// <para>Las fórmulas del Excel, tal como están en el archivo:</para>
/// <list type="bullet">
///   <item><description>Residencial: <c>E6 = [@Precio] * [@Cantidad a utilizar]</c></description></item>
///   <item><description>Residencial: <c>E112 = IFERROR((B112 * C112) / D112, "")</c></description></item>
///   <item><description>Residencial: <c>G6 = SUM(Tabla1[Total]) + SUM(Tabla475[Valor mensal])</c></description></item>
///   <item><description>Residencial: <c>H6 = (Σ E6:E108 + Σ valor mensal) + ((Σ E + Σ valor mensal) * J6)</c></description></item>
///   <item><description>Residencial: <c>I6 = H6 / VLOOKUP($H$4, Tabla2, 2)</c></description></item>
///   <item><description>Residencial: <c>K6 = IFERROR(G6 / H6, "")</c></description></item>
///   <item><description>Empresarial: <c>C8 = AVERAGE(SUM(Tabla16[Total]), SUM(Tabla168[Total])) + SUM(Tabla47[Valor mensal])</c></description></item>
///   <item><description>Empresarial: <c>D8 = C8 + (C8 * F8)</c></description></item>
///   <item><description>Empresarial: <c>E8 = D8 / VLOOKUP($D$6, Tabla2, 2)</c></description></item>
///   <item><description>Empresarial: <c>G8 = IFERROR(C8 / D8, "")</c></description></item>
/// </list>
/// </summary>
public sealed class CotizadorCalculo : ICotizadorCalculo
{
    /// <summary>
    /// Métodos indexados por el <c>Id</c> que les asignó la base. Lo arma el registro de
    /// dependencias, leyendo <c>dbo.MetodoCosteoCatalogo</c>.
    /// </summary>
    private readonly IReadOnlyDictionary<byte, IMetodoCosteo> _metodosPorId;

    private readonly SemestresResueltos _semestres;

    /// <param name="metodosPorId">
    /// Un <see cref="IMetodoCosteo"/> por <c>Id</c> de <c>dbo.MetodoCosteoCatalogo</c>. Si la base
    /// tiene un método sin implementación, la validación de arranque lo rechaza: mejor fallar al
    /// arrancar que calcular en silencio con otro método.
    /// </param>
    /// <param name="semestres">Ids de <c>dbo.SemestreCatalogo</c> ya resueltos.</param>
    public CotizadorCalculo(
        IReadOnlyDictionary<byte, IMetodoCosteo> metodosPorId,
        SemestresResueltos semestres)
    {
        ArgumentNullException.ThrowIfNull(metodosPorId);
        ArgumentNullException.ThrowIfNull(semestres);

        if (metodosPorId.Count == 0)
        {
            throw new ArgumentException(
                "No hay ningún método de costeo registrado. dbo.MetodoCosteoCatalogo está vacía o " +
                "ninguna fila tiene implementación en IMetodoCosteo.",
                nameof(metodosPorId));
        }

        _metodosPorId = metodosPorId;
        _semestres = semestres;
    }

    public ResultadoCalculo Calcular(EntradaCalculo entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var metodo = ResolverMetodo(entrada.MetodoCosteoId);

        Validar(entrada);

        // ── RN-01 · Σ de insumos por semestre ───────────────────────────────────────
        // El Excel separa en Tabla1 (semestre 1) y Tabla168 (semestre 2). El motor no separa
        // tablas: separa por la columna Semestre. Las líneas sin semestre (residencial) caen en
        // el primero, que es lo que hace el Excel cuando no hay tabla de segundo semestre.
        var totalSemestre1 = entrada.Insumos
            .Where(l => _semestres.Clasificar(l.SemestreId) == _semestres.PrimerSemestreId)
            .Sum(l => l.Total);

        var totalSemestre2 = entrada.Insumos
            .Where(l => _semestres.Clasificar(l.SemestreId) == _semestres.SegundoSemestreId)
            .Sum(l => l.Total);

        // ── RN-02 · Suma de valores mensuales de activos ──────────────────────────
        var totalAmortizables = entrada.Amortizables.Sum(l => l.ValorMensual);

        // ── RN-03 · Total de insumos ──────────────────────────────────────────────
        // Residencial: suma. Empresarial: promedio de los dos semestres. Cada método lo decide;
        // el motor no sabe cuál de los dos es cuál.
        var totalInsumos = metodo.CalcularTotalInsumos(totalSemestre1, totalSemestre2);

        // ── RN-04 / RN-07 · Costo total ───────────────────────────────────────────
        var costoTotal = metodo.CalcularCostoTotal(totalInsumos, totalAmortizables, entrada.CostoPlantilla);

        // ── RN-05 · Precio en córdobas ────────────────────────────────────────────
        // H6 = costo + (costo * margen)   |   D8 = C8 + (C8 * F8)
        var precioCordobas = costoTotal * (1m + entrada.Margen);

        // ── RN-06 · Precio en dólares ─────────────────────────────────────────────
        // I6 = H6 / TC   |   E8 = D8 / TC
        // ⚠️ H-01: el TC es el último con fecha ≤ la de la cotización, no el de hoy. Eso lo
        // resuelve quien arma la EntradaCalculo (TipoCambioService), no el motor.
        var precioUsd = precioCordobas / entrada.TipoCambio;

        // ── RN-08 · Porcentaje de plantilla (indicador, no precio) ─────────────────
        // K6 = G6 / H6 en el Excel: el subtotal antes de la plantilla sobre el subtotal con
        // plantilla. No participa en el precio.
        decimal? porcentajePlantilla = null;
        if (entrada.CostoPlantilla is > 0 && costoTotal != 0)
        {
            var subtotal = totalInsumos + totalAmortizables;
            porcentajePlantilla = entrada.PorcentajePlantilla ?? subtotal / costoTotal;
        }

        var parametros = JsonSerializer.Serialize(entrada, OpcionesJson);

        return new ResultadoCalculo
        {
            TotalInsumosSemestre1 = totalSemestre1,
            TotalInsumosSemestre2 = totalSemestre2,
            TotalInsumos = totalInsumos,
            TotalAmortizablesMensual = totalAmortizables,
            CostoTotal = costoTotal,
            PrecioCordobas = precioCordobas,
            PrecioUsd = precioUsd,
            MargenAplicado = entrada.Margen,
            TipoCambioValor = entrada.TipoCambio,
            MetodoCosteoId = entrada.MetodoCosteoId,
            PorcentajePlantilla = porcentajePlantilla,
            HashCalculo = SHA256.HashData(Encoding.UTF8.GetBytes(parametros)),
            ParametrosCalculo = parametros
        };
    }

    private IMetodoCosteo ResolverMetodo(byte metodoCosteoId)
    {
        if (_metodosPorId.TryGetValue(metodoCosteoId, out var metodo))
        {
            return metodo;
        }

        // No se cae a un "método por defecto": si el id no está, es un dato de más o una siembra
        // incompleta, y adivinar produciría cotizaciones con un costeo que nadie pidió.
        throw new CalculoInvalidoException(
            $"El método de costeo {metodoCosteoId} no tiene implementación registrada. Revise " +
            "dbo.MetodoCosteoCatalogo y el registro de IMetodoCosteo en el contenedor de dependencias.",
            "metodo de costeo no implementado");
    }

    /// <summary>
    /// Reglas que el motor hace cumplir. Cada una corrige un hallazgo del Excel; ninguna es
    /// "buena práctica" genérica.
    /// </summary>
    private static void Validar(EntradaCalculo entrada)
    {
        // H-02: el margen es una fracción. El Excel lo guarda en crudo (5.5 con formato 0%),
        // lo que lo muestra como 550 %. Si alguien pasa 5.5, es un error de entrada, no un
        // margen del 550 %.
        if (entrada.Margen is < 0 or > 1)
        {
            throw new CalculoInvalidoException(
                $"El margen debe ser una fracción entre 0 y 1 (0.0550 = 5.5 %). Recibido: {entrada.Margen}.",
                "margen fuera de rango");
        }

        // El Excel divide por el TC sin verificar. Con TC = 0 daba #DIV/0!.
        if (entrada.TipoCambio <= 0)
        {
            throw new CalculoInvalidoException(
                $"El tipo de cambio debe ser mayor que cero. Recibido: {entrada.TipoCambio}.",
                "tipo de cambio inválido");
        }

        // H-04: MesesVidaUtil = 0 divide entre cero. En el Excel el IFERROR devolvía "" y el
        // valor mensual quedaba en 0 sin que nadie se enterara.
        foreach (var linea in entrada.Amortizables.Where(l => l.MesesVidaUtil <= 0))
        {
            throw new CalculoInvalidoException(
                $"El activo \"{linea.ActivoNombre}\" tiene {linea.MesesVidaUtil} meses de vida útil. " +
                "Debe ser mayor que cero: con cero el valor mensual sería 0 en silencio.",
                "vida útil inválida");
        }

        if (entrada.Insumos.Any(l => l.Cantidad <= 0))
        {
            throw new CalculoInvalidoException(
                "Hay una línea de insumo con cantidad cero o negativa.", "cantidad inválida");
        }

        if (entrada.Insumos.Any(l => l.PrecioUnitario < 0))
        {
            throw new CalculoInvalidoException(
                "Hay una línea de insumo con precio negativo.", "precio inválido");
        }
    }

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = false
    };
}

/// <summary>
/// Error de negocio del motor. Es <b>distinto</b> de un error técnico: la API lo traduce a
/// <c>400 Bad Request</c> con un mensaje para el usuario, no a <c>500</c>.
/// </summary>
public sealed class CalculoInvalidoException : Exception
{
    public CalculoInvalidoException(string mensaje, string codigo)
        : base(mensaje) => Codigo = codigo;

    /// <summary>Código estable, para que la UI pueda reaccionar sin parsear el mensaje.</summary>
    public string Codigo { get; }
}
