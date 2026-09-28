namespace Orkin.Cotizador.Core.Domain.Cotizacion;

/// <summary>Una línea de insumo tal como la recibe el motor. Es un <b>value object</b>: sin identidad.</summary>
/// <param name="SemestreId">
/// FK a <c>dbo.SemestreCatalogo</c>, ya resuelta. <c>null</c> = indistinto (residencial, RN-04). En
/// empresarial decide a qué semestre suma (RN-07).
/// <para>El Excel <b>copia</b> el semestre 1 en el 2; el SRS los declara independientes
/// (<b>D-03</b>). Acá cada línea dice lo suyo, y la copia —si alguna vez fuera necesaria— sería una
/// decisión explícita del servicio, no un efecto colateral de una fórmula.</para>
/// </param>
/// <param name="PrecioUnitario">Costo unitario. Viene del producto, ya leído.</param>
public readonly record struct LineaInsumo(
    string ProductoNombre,
    string UnidadMedidaAbreviatura,
    decimal Cantidad,
    decimal PrecioUnitario,
    byte? SemestreId = null)
{
    /// <summary>RN-01: <c>E6 = [@Precio] * [@Cantidad a utilizar]</c> en el Excel.</summary>
    public decimal Total => PrecioUnitario * Cantidad;
}

/// <summary>
/// Una línea de activo amortizable.
/// </summary>
/// <param name="MesesVidaUtil">
/// <b>Nunca puede ser 0.</b> El Excel trae <c>Meses = 0</c> en las 13 filas de <c>Tabla47</c>, y
/// eso dejaba el valor mensual en 0 sin avisar nada (<b>H-04</b>). Acá el motor lo rechaza con
/// excepción explícita, y la tabla tiene <c>CHECK (MesesVidaUtil &gt; 0)</c>.
/// </param>
public readonly record struct LineaAmortizable(
    string ActivoNombre,
    string UnidadMedidaAbreviatura,
    decimal Cantidad,
    decimal PrecioUnitario,
    int MesesVidaUtil)
{
    /// <summary>Costo total del activo: <c>PrecioUnitario × Cantidad</c>.</summary>
    public decimal Total => PrecioUnitario * Cantidad;

    /// <summary>RN-02: <c>E112 = (B112 * C112) / D112</c> en el Excel.</summary>
    public decimal ValorMensual => Total / MesesVidaUtil;
}

/// <summary>Entrada completa del motor. Es todo lo que necesita para calcular, y nada más.</summary>
public sealed record EntradaCalculo
{
    /// <summary>
    /// FK a <c>dbo.MetodoCosteoCatalogo</c>, ya resuelta. La capa de aplicación la obtiene de
    /// <c>dbo.TipoClienteCatalogo.MetodoCosteoId</c> del segmento del cliente.
    /// <para>El motor no recibe "residencial" ni "empresarial": recibe el método. Por eso no
    /// necesita saber ningún código de catálogo.</para>
    /// </summary>
    public required byte MetodoCosteoId { get; init; }

    /// <summary>Margen como <b>fracción</b>: <c>0.0550</c> = 5.5 %. Nunca <c>5.5</c> (H-02).</summary>
    public required decimal Margen { get; init; }

    /// <summary>Tipo de cambio córdoba → dólar. Debe ser > 0.</summary>
    public required decimal TipoCambio { get; init; }

    public IReadOnlyList<LineaInsumo> Insumos { get; init; } = [];

    public IReadOnlyList<LineaAmortizable> Amortizables { get; init; } = [];

    /// <summary>
    /// Costo de la plantilla (insumos de operación: viáticos, tiempo del técnico).
    /// En el Excel es la columna <c>J</c> del residencial, que se multiplica por el subtotal
    /// (<c>H6 = (Σ E + Σ valor mensual) + ((Σ E + Σ valor mensual) * J)</c>). Solo lo aplica el
    /// método de suma simple; el empresarial lo ignora, igual que el Excel.
    /// </summary>
    public decimal CostoPlantilla { get; init; }

    /// <summary>
    /// Porcentaje de la plantilla (RN-08). <b>No afecta el precio</b>: es un indicador interno
    /// para saber cuánto del costo es plantilla.
    /// </summary>
    public decimal? PorcentajePlantilla { get; init; }

    /// <summary>Nombre del tipo de servicio, para el PDF y los registros.</summary>
    public string? TipoServicioNombre { get; init; }
}

/// <summary>Salida del motor. Cada valor es el que se persiste como snapshot en <c>dbo.Cotizacion</c>.</summary>
public sealed record ResultadoCalculo
{
    public required decimal TotalInsumosSemestre1 { get; init; }
    public required decimal TotalInsumosSemestre2 { get; init; }

    /// <summary>RN-03. Lo define el método de costeo: suma o promedio.</summary>
    public required decimal TotalInsumos { get; init; }

    /// <summary>RN-02.</summary>
    public required decimal TotalAmortizablesMensual { get; init; }

    /// <summary>RN-04 / RN-07. También lo define el método de costeo.</summary>
    public required decimal CostoTotal { get; init; }

    /// <summary>RN-05: <c>CostoTotal × (1 + Margen)</c>.</summary>
    public required decimal PrecioCordobas { get; init; }

    /// <summary>RN-06: <c>PrecioCordobas / TipoCambio</c>.</summary>
    public required decimal PrecioUsd { get; init; }

    public required decimal MargenAplicado { get; init; }

    public required decimal TipoCambioValor { get; init; }

    /// <summary>Qué método produjo el resultado. Se persiste para poder reproducirlo.</summary>
    public required byte MetodoCosteoId { get; init; }

    /// <summary>RN-08.</summary>
    public decimal? PorcentajePlantilla { get; init; }

    /// <summary>SHA-256 de la <see cref="EntradaCalculo"/>. Si cambia sin que cambien las entradas, alguien editó la fila.</summary>
    public required byte[] HashCalculo { get; init; }

    /// <summary>JSON de la entrada, para poder reproducir el cálculo.</summary>
    public required string ParametrosCalculo { get; init; }
}
