namespace Orkin.Cotizador.Core.Domain.Catalogos;

/// <summary>
/// Forma común de las tablas catálogo de <c>dbo.*Catalogo</c> (ADR-018, `BASE-DE-DATOS.md` §5.0).
/// <para><b>Por qué no hay un <c>enum</c> de C# equivalente:</b> cualquier conjunto de valores de
/// negocio —estados, modalidades, unidades, niveles— existe en la base y se edita desde
/// Administración. Si estuviera también en el código, habría dos verdades y la base dejaría de ser
/// la fuente de verdad.</para>
/// <para><see cref="Id"/> no tiene setter público a propósito: lo asigna el <c>IDENTITY</c> de SQL
/// Server. El código se refiere a una fila por <see cref="Codigo"/>, que es una clave estable en
/// texto, y resuelve el <c>Id</c> al arrancar.</para>
/// </summary>
public abstract class CatalogoDominioBase
{
    /// <summary><c>tinyint IDENTITY</c>. Lo asigna la base; el código nunca lo fija.</summary>
    public byte Id { get; private set; }

    /// <summary>
    /// Clave estable con la que el código se refiere al valor. Ej. <c>RESIDENCIAL</c>,
    /// <c>EN_APROBACION</c>. <c>nvarchar(30)</c>, única.
    /// </summary>
    public string Codigo { get; set; } = null!;

    /// <summary>Etiqueta que ve el usuario. La muestran la UI, el PDF y los correos.</summary>
    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    /// <summary>Orden de presentación. Ni la UI ni el PDF ordenan por texto.</summary>
    public byte Orden { get; set; }

    public bool EsActivo { get; set; } = true;

    /// <summary>
    /// <c>true</c> = la fila viene del seed; <c>false</c> = la creó un usuario. Es lo que permite
    /// que la siembra sea idempotente sin pisar lo que alguien corrigió a mano (§5.20).
    /// </summary>
    public bool EsSembrado { get; set; } = true;
}
