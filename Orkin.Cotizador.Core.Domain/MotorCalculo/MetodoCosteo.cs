namespace Orkin.Cotizador.Core.Domain.MotorCalculo;

/// <summary>
/// Un método de costeo. Una implementación por fila de <c>dbo.MetodoCosteoCatalogo</c> (§5.12).
/// <para>La división es deliberada y es la que pediste:</para>
/// <list type="bullet">
///   <item><description><b>Qué</b> métodos existen y <b>qué</b> etiqueta tiene cada uno → la base
///   de datos (<c>dbo.MetodoCosteoCatalogo</c>).</description></item>
///   <item><description><b>Cómo</b> se calcula cada uno → estas clases, en el código, con tests.</description></item>
/// </list>
/// <para>La fila de la base y la clase se conectan por <see cref="Codigo"/>. Ese string no es una
/// copia del dato: es la clave de contrato entre la tabla y la clase que implementa su
/// comportamiento. Está junto a la lógica, no en un <c>enum</c> central que alguien tendría que
/// editar aparte. Agregar un método es <b>una fila + una clase</b>.</para>
/// </summary>
public interface IMetodoCosteo
{
    /// <summary>
    /// Debe coincidir con el <c>Codigo</c> de la fila en <c>dbo.MetodoCosteoCatalogo</c>. Si no
    /// coincide, la fila queda sin implementar y el arranque falla con un mensaje explícito: es
    /// preferible a calcular en silencio con el método equivocado.
    /// </summary>
    string Codigo { get; }

    /// <summary>
    /// RN-03. Cómo se obtiene el total de insumos a partir de los dos semestres.
    /// <para>Residencial: los dos semestres ya vienen con la misma información (todo cae en el
    /// primero), así que devuelve la suma. Empresarial: el promedio, tal como
    /// <c>AVERAGE(SUM(...), SUM(...))</c> en el Excel.</para>
    /// </summary>
    decimal CalcularTotalInsumos(decimal totalSemestre1, decimal totalSemestre2);

    /// <summary>
    /// RN-04 / RN-07. Cómo se arma el costo total a partir de los subtotales.
    /// </summary>
    decimal CalcularCostoTotal(decimal totalInsumos, decimal totalAmortizablesMensual, decimal costoPlantilla);
}

/// <summary>
/// RN-04 · <c>SUMA_SIMPLE</c>. Es el método residencial del Excel.
/// <para>Hoja residencial: <c>H6 = (SUM(E6:E108) + SUM(Tabla475[Valor mensal])) + ((SUM(E) + SUM(valor mensual)) * J6)</c>,
/// es decir el subtotal <b>más</b> el subtotal multiplicado por el costo de plantilla.</para>
/// <para>Cuando no hay plantilla, la fórmula es el subtotal tal cual.</para>
/// </summary>
public sealed class MetodoCosteoSumaSimple : IMetodoCosteo
{
    /// <summary>Clave de contrato con <c>dbo.MetodoCosteoCatalogo</c> (§5.12).</summary>
    public string Codigo => CodigosMetodoCosteo.SumaSimple;

    public decimal CalcularTotalInsumos(decimal totalSemestre1, decimal totalSemestre2)
        => totalSemestre1 + totalSemestre2;

    public decimal CalcularCostoTotal(decimal totalInsumos, decimal totalAmortizablesMensual, decimal costoPlantilla)
    {
        var subtotal = totalInsumos + totalAmortizablesMensual;
        return costoPlantilla is 0 ? subtotal : subtotal + (subtotal * costoPlantilla);
    }
}

/// <summary>
/// RN-07 · `PROMEDIO_SEMESTRES`. Es el método empresarial del Excel.
/// <para>Hoja empresarial: <c>C8 = AVERAGE(SUM(Tabla16[Total]), SUM(Tabla168[Total])) + SUM(Tabla47[Valor mensal])</c>.</para>
/// <para>Dos diferencias con el residencial que conviene no perder de vista: (1) los insumos se
/// <b>promedian</b>, no se suman; (2) los amortizables se suman <b>fuera</b> del promedio, y la
/// plantilla no se aplica. Por eso van en clases distintas y no en un <c>if</c> con un número
/// mágico.</para>
/// </summary>
public sealed class MetodoCosteoPromedioSemestres : IMetodoCosteo
{
    /// <summary>Clave de contrato con <c>dbo.MetodoCosteoCatalogo</c> (§5.12).</summary>
    public string Codigo => CodigosMetodoCosteo.PromedioSemestres;

    public decimal CalcularTotalInsumos(decimal totalSemestre1, decimal totalSemestre2)
        => (totalSemestre1 + totalSemestre2) / 2m;

    public decimal CalcularCostoTotal(decimal totalInsumos, decimal totalAmortizablesMensual, decimal costoPlantilla)
        => totalInsumos + totalAmortizablesMensual;
}

/// <summary>
/// Las claves de <c>dbo.MetodoCosteoCatalogo</c>. No es un catálogo: es el <b>contrato</b> entre
/// la tabla y las clases que implementan su comportamiento.
/// <para>Si se agrega un método, se agrega la fila a la tabla <b>y</b> la clase, y su código entra
/// acá. No hay una lista central de "valores permitidos" que pueda quedar desactualizada respecto
/// de la base.</para>
/// </summary>
public static class CodigosMetodoCosteo
{
    public const string SumaSimple = "SUMA_SIMPLE";
    public const string PromedioSemestres = "PROMEDIO_SEMESTRES";
}

/// <summary>
/// Los ids de <c>dbo.SemestreCatalogo</c> ya resueltos, para que el motor pueda clasificar una
/// línea sin conocer el catálogo.
/// <para>Se construye una vez al arrancar, leyendo la tabla. Es lo que evita que el motor tenga
/// que comparar contra el número <c>1</c> y asumir que ese <c>1</c> es "primer semestre".</para>
/// </summary>
public sealed class SemestresResueltos
{
    public SemestresResueltos(byte primerSemestreId, byte segundoSemestreId)
    {
        if (primerSemestreId == segundoSemestreId)
        {
            throw new ArgumentException(
                "Primer y segundo semestre resolvieron al mismo id. Es un problema del catálogo, " +
                "no de la lógica: revise dbo.SemestreCatalogo.",
                nameof(primerSemestreId));
        }

        PrimerSemestreId = primerSemestreId;
        SegundoSemestreId = segundoSemestreId;
    }

    public byte PrimerSemestreId { get; }

    public byte SegundoSemestreId { get; }

    /// <summary>
    /// Clasifica el semestre de una línea. Una línea sin semestre (el caso residencial, RN-04)
    /// cuenta como primer semestre, que es lo que el Excel hace cuando no hay tabla de segundo
    /// semestre.
    /// </summary>
    public byte Clasificar(byte? semestreId) => semestreId ?? PrimerSemestreId;
}
