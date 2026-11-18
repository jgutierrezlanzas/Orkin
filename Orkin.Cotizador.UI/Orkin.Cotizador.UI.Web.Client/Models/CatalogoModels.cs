namespace Orkin.Cotizador.UI.Web.Client.Models;

public sealed class TipoCambioModel
{
    public Guid Id { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Valor { get; set; }
    public byte FuenteId { get; set; }
    public string? FuenteNombre { get; set; }
    public bool EsActivo { get; set; }
}

public sealed class FuenteTipoCambioOption
{
    public byte Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>Opción legible de una vista <c>vv*</c>: el código se guarda, el texto se muestra.</summary>
public sealed class OpcionErpModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public string? UnidadMedida { get; set; }
    public string? Clasificacion { get; set; }
    public decimal? Costo { get; set; }
}

public sealed class OpcionesErpModel
{
    public List<OpcionErpModel> Locaciones { get; set; } = [];
    public List<OpcionErpModel> Vehiculos { get; set; } = [];
    public List<OpcionErpModel> Plagas { get; set; } = [];
    public List<OpcionErpModel> Ubicaciones { get; set; } = [];
    public List<OpcionErpModel> Dosificaciones { get; set; } = [];
    public List<OpcionErpModel> Metodos { get; set; } = [];
    public List<OpcionErpModel> Frecuencias { get; set; } = [];
    public List<OpcionErpModel> Equipos { get; set; } = [];

    /// <summary>Bodegas activas de vvBodegas, compartidas por todas las pantallas (Fase 5).</summary>
    public List<OpcionErpModel> Bodegas { get; set; } = [];

    /// <summary>Catálogos ERP del cotizador (D-24): se guarda el código, nunca una copia local.</summary>
    public List<OpcionErpModel> Clientes { get; set; } = [];
    public List<OpcionErpModel> TiposServicio { get; set; } = [];
    public List<OpcionErpModel> Tecnicos { get; set; } = [];
    public List<OpcionErpModel> Articulos { get; set; } = [];
    public List<OpcionErpModel> ActivosAmortizables { get; set; } = [];
    public List<OpcionErpModel> Unidades { get; set; } = [];
    public List<OpcionErpModel> Clasificaciones { get; set; } = [];
}
