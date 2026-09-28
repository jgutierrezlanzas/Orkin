namespace Orkin.Cotizador.Core.Infrastructure.Persistence;

/// <summary>
/// Los cuatro esquemas de la base (ADR-006, `BASE-DE-DATOS.md` §1).
/// <para>Están en constantes y no en cadenas sueltas porque un nombre de esquema escrito a mano
/// en 40 lugares es un nombre de esquema mal escrito en 40 lugares.</para>
/// </summary>
public static class Esquemas
{
    /// <summary>Negocio: clientes, catálogos maestros, cotizaciones, parámetros.</summary>
    public const string Negocio = "dbo";

    /// <summary>Seguridad propia: usuarios, roles, permisos (D-02, §8).</summary>
    public const string Seguridad = "seg";

    /// <summary>Integración con el ERP: el motor ETL de §7. Fase 12.</summary>
    public const string Integracion = "int";

    /// <summary>Auditoría append-only, alimentada por un interceptor (§9).</summary>
    public const string Auditoria = "aud";
}
