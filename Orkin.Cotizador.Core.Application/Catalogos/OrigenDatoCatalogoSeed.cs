namespace Orkin.Cotizador.Core.Application.Catalogos;

/// <summary>
/// Valores de siembra de <c>dbo.OrigenDatoCatalogo</c> que usan los servicios cuando crean un
/// registro manual. Estos ids deben coincidir con la semilla <c>dbo.OrigenDatoCatalogo.json</c>.
/// <para>Se conserva aunque los catálogos espejo se retiraron (D-24): <c>TipoCambio</c> sigue siendo
/// un dato propio de la plataforma y <c>Cotizacion</c> lo registra como origen manual.</para>
/// </summary>
public static class OrigenDatoCatalogoSeed
{
    public const int Manual = 1;
}
