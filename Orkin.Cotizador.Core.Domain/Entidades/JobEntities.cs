namespace Orkin.Cotizador.Core.Domain.Entidades;

/// <summary>
/// Auditoría de una corrida de un job (Fase 8), incluidos los reenvíos manuales.
/// <para><b>Por qué no es una tabla de flujos ETL:</b> un job que lee los SPs de notificación de
/// SINCOERPORKIN no es un flujo de carga del ERP; vincular su bitácora a una conexión o flujo
/// exigiría inventar filas sin significado. Esta tabla es solo auditoría de ejecución, no una copia
/// de ninguna entidad legacy ni del staging <c>NotificarProgramacionVisitaTemporal</c>.</para>
/// <para>No hereda <c>BaseEntity</c>: es append-only, lo escribe el job y no se edita desde la UI.</para>
/// </summary>
public class JobEjecucion
{
    /// <summary><c>bigint IDENTITY</c>: un job puede correr muchas veces por minuto.</summary>
    public long Id { get; set; }

    /// <summary>Nombre del job, por ejemplo <c>Notificaciones</c>.</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>Qué hizo la corrida: <c>COLA</c>, <c>ENVIO</c>, <c>REENVIOS</c>.</summary>
    public string Operacion { get; set; } = null!;

    public DateTime IniciadoUtc { get; set; }

    public DateTime? FinalizadoUtc { get; set; }

    /// <summary><c>EN_CURSO</c>, <c>OK</c> o <c>FALLIDO</c>.</summary>
    public string Estado { get; set; } = null!;

    public int Encolados { get; set; }

    public int Enviados { get; set; }

    public int Fallidos { get; set; }

    public string? Mensaje { get; set; }

    public Guid? UsuarioId { get; set; }

    /// <summary>Snapshot del usuario que disparó una operación manual; <c>null</c> en el job.</summary>
    public string? Usuario { get; set; }
}
