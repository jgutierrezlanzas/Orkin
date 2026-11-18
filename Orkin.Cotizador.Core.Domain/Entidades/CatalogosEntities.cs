using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;

namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Tipo de cambio córdoba/dólar.
/// <para>⚠️ <b>H-01</b> / RN-05: la cotización toma el <b>último valor cuya fecha sea ≤ la fecha de
/// la cotización</b>, no el valor de hoy.</para>
/// </summary>
public class TipoCambio : BaseEntity
{
    public DateOnly Fecha { get; set; }

    public decimal Valor { get; set; }

    /// <summary>FK → <c>dbo.FuenteTipoCambioCatalogo</c>. Depende de <b>D-06</b>.</summary>
    public byte FuenteId { get; set; }

    public FuenteTipoCambioCatalogo? Fuente { get; set; }

    /// <summary>FK → <c>dbo.OrigenDatoCatalogo</c>.</summary>
    public byte OrigenDatoId { get; set; }

    public Guid? ErpConexionId { get; set; }
    public string? CodigoExterno { get; set; }
    public byte[]? HashContenido { get; set; }
    public DateTime? UltimaSyncUtc { get; set; }
}

// D-20 / D-24: aquí NO se definen clientes, productos, activos ni sus precios. Los códigos se
// validan contra las vistas vv* del ERP y el único catálogo histórico propio es el tipo de cambio.
