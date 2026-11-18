using Microsoft.Extensions.Logging;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Inventario;

/// <summary>Resultado observable de una llamada a los SPs de Exactus.</summary>
/// <param name="Exitoso">El SP terminó sin <c>RAISERROR</c>.</param>
/// <param name="Mensaje">Texto que debe ver el usuario; en un error es el mensaje del SP tal cual.</param>
/// <param name="ResultadoSp">
/// Valor del <c>SELECT ... AS RESULTADO</c> que devuelven ambos SPs. El WinForms lo descartaba
/// con <c>ExecuteNonQuery</c>; acá se captura y se registra, sin reemplazar los mensajes de éxito
/// de la UI legacy.
/// </param>
public sealed record ExactusResultado(bool Exitoso, string Mensaje, string? ResultadoSp);

/// <summary>
/// Envoltorio de los dos SPs legacy que escriben en Exactus (D-22: se reutilizan, no se
/// reimplementan). Parámetros con nombre, sin concatenación SQL, y registro de quién ejecutó cada
/// llamada y con qué resultado.
/// </summary>
public interface IExactusService
{
    /// <summary><c>dbo.SINCO_GENERA_DOCINV_EXACTUS @Requisa_Numero</c>.</summary>
    Task<ExactusResultado> GenerarDesdeRequisaAsync(
        string requisaNumero,
        CancellationToken cancellationToken = default);

    /// <summary><c>dbo.SINCO_TRASLADAR_DOCINV_EXACTUS @Documento_Inventario</c>.</summary>
    Task<ExactusResultado> TransferirAsync(
        string documentoInventario,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ExactusService : IExactusService
{
    private const string Generar = "dbo.SINCO_GENERA_DOCINV_EXACTUS";
    private const string Transferir = "dbo.SINCO_TRASLADAR_DOCINV_EXACTUS";

    public const string MensajeGenerado = "Documento de inventario generado satisfactoriamente.";
    public const string MensajeTrasladado =
        "Documento de inventario trasladado a Exactus satisfactoriamente.";

    private readonly EjecutorSql _sql;
    private readonly ILogger<ExactusService> _logger;
    private readonly IContextoUsuario _usuario;

    public ExactusService(
        EjecutorSql sql,
        ILogger<ExactusService> logger,
        IContextoUsuario usuario)
    {
        _sql = sql;
        _logger = logger;
        _usuario = usuario;
    }

    public Task<ExactusResultado> GenerarDesdeRequisaAsync(
        string requisaNumero,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requisaNumero);
        return EjecutarAsync(
            Generar, "Generar documento", "@Requisa_Numero", requisaNumero.Trim(),
            MensajeGenerado, cancellationToken);
    }

    public Task<ExactusResultado> TransferirAsync(
        string documentoInventario,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentoInventario);
        return EjecutarAsync(
            Transferir, "Transferir a Exactus", "@Documento_Inventario", documentoInventario.Trim(),
            MensajeTrasladado, cancellationToken);
    }

    private async Task<ExactusResultado> EjecutarAsync(
        string procedimiento,
        string operacion,
        string parametro,
        string valor,
        string mensajeUsuario,
        CancellationToken cancellationToken)
    {
        try
        {
            // El nombre del procedimiento y del parámetro son constantes de esta clase; el valor
            // del ERP viaja como SqlParameter. Nada de SQL concatenado con datos del usuario.
            var filas = await _sql.ConsultarAsync(
                $"EXEC {procedimiento} {parametro}",
                cancellationToken,
                (parametro, valor));

            var resultadoSp = filas.FirstOrDefault()?.Values.FirstOrDefault()?.ToString();
            _logger.LogInformation(
                "Exactus: {Operacion} ejecutado por {Usuario} sobre {Clave}. Resultado del SP: {Resultado}",
                operacion,
                _usuario.NombreUsuario ?? "sistema",
                valor,
                resultadoSp ?? "(sin resultado)");

            return new ExactusResultado(true, mensajeUsuario, resultadoSp);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Igual que Fase 4: el RAISERROR del SP es contrato observable del WinForms. Se registra
            // y se reenvía sin traducirlo para que el usuario vea exactamente el mismo texto.
            _logger.LogError(
                ex,
                "Exactus: {Operacion} falló para {Clave} ejecutado por {Usuario}.",
                operacion,
                valor,
                _usuario.NombreUsuario ?? "sistema");
            throw new InvalidOperationException(ex.Message, ex);
        }
    }
}
