using System.Data;
using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Sistema;

/// <summary>
/// Emite la numeración oficial de los documentos (<c>COT-2026-0413</c>).
/// <para><b>Por qué no es un <c>IDENTITY</c> de SQL Server:</b> un <c>IDENTITY</c> es un contador de
/// fila, no un número de documento. No se puede partir por año, no se puede reiniciar cuando la
/// administración decide que el año comercial empieza en otro mes, y no deja rastro de a quién se le
/// emitió. Acá el correlativo es una fila de <c>dbo.Consecutivo</c> y el número sale de ella.</para>
/// <para><b>Por qué el número nunca se repite:</b> la reserva se hace con un
/// <c>UPDATE … SET UltimoNumero = UltimoNumero + 1</c> y no con un leer-y-luego-escribir. La
/// sentencia toma un bloqueo de fila que dura lo que dura la operación, así que dos vendedores
/// guardando a la vez se serializan: el segundo espera al primero y lee el valor ya incrementado. Con
/// leer-y-luego-escribir, los dos leerían el mismo <c>UltimoNumero</c> y emitirían el mismo número —
/// y un número repetido en una cotización obliga a anular un documento que el cliente ya está
/// leyendo.</para>
/// <para><b>Por qué puede quedar un hueco:</b> si la transacción que reservó el número no llega a
/// confirmar el documento, el número se gastó igual. Es un hueco en la secuencia, visible y
/// auditable. La alternativa —guardar el número al final— quita los huecos a costa de los repetidos,
/// y un hueco se explica en una reunión mientras que un repetido hay que rehacer el papel.</para>
/// <para><b>Por qué el prefijo y el ancho se congelan con el primer número del año:</b> se copian a
/// la fila la primera vez y ahí se quedan. Así, cambiar el prefijo en Administración a mitad de año no
/// renumera hacia atrás lo ya emitido, que es exactamente lo que no puede pasar con una numeración
/// que la administración usa para citar.</para>
/// </summary>
public sealed class ConsecutivoService
{
    private readonly CotizadorDbContext _contexto;
    private readonly EjecutorSql _sql;

    public ConsecutivoService(CotizadorDbContext contexto, EjecutorSql sql)
    {
        _contexto = contexto;
        _sql = sql;
    }

    /// <summary>
    /// Reserva el siguiente número del tipo indicado y devuelve el documento ya formateado.
    /// <para>El tipo va por su <c>Codigo</c> y no por su <c>Id</c> por la misma razón que el resto
    /// del sistema: el número de un <c>IDENTITY</c> depende del orden en que se insertaron las filas,
    /// y un número que funciona en una base no funciona en otra.</para>
    /// </summary>
    /// <param name="tipoCodigo"><c>Codigo</c> de <c>dbo.TipoConsecutivoCatalogo</c> (§5.15).</param>
    /// <param name="anio">Año del documento, no el del reloj: una cotización de diciembre se
    /// guarda en enero y sigue siendo del año que dice arriba.</param>
    public async Task<string> SiguienteAsync(
        string tipoCodigo,
        int anio,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoCodigo);

        var tipo = await _contexto.TipoConsecutivoCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Codigo == tipoCodigo && t.EsActivo, cancellationToken)
            ?? throw new CatalogoNoResueltoException(nameof(TipoConsecutivoCatalogo), tipoCodigo);

        var prefijoAnio = $"{tipo.Prefijo}{anio.ToString(CultureInfo.InvariantCulture)}-";

        // La reserva necesita su propia transacción si el que llama no trajo una. No es un detalle
        // de estilo: los bloqueos de SERIALIZABLE duran lo que dura la transacción, y en modo
        // automático se sueltan al terminar cada sentencia. Sin una transacción que abarque el
        // UPDATE y el INSERT, dos llamadas simultáneas al primer número del año no se ven entre sí,
        // las dos fallan el UPDATE y las dos intentan insertar: una recibe una violación de clave
        // única en vez de un número. Con la transacción, la segunda espera a que la primera confirme.
        var propia = _contexto.Database.CurrentTransaction is null;

        var transaccion = propia
            ? await _contexto.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken)
            : null;

        try
        {
            var numero = await ReservarAsync(tipo, anio, prefijoAnio, cancellationToken);

            if (transaccion is not null)
            {
                // Se confirma antes de devolver el número, no después de que el documento se
                // guarde. Así el número que sale de este método está reservado de verdad: si el
                // guardado del documento falla, el número ya gastado es un hueco visible, y no un
                // número que alguien más pueda recibir después.
                await transaccion.CommitAsync(cancellationToken);
            }

            return numero;
        }
        finally
        {
            if (transaccion is not null)
            {
                await transaccion.DisposeAsync();
            }
        }
    }

    private async Task<string> ReservarAsync(
        TipoConsecutivoCatalogo tipo,
        int anio,
        string prefijoAnio,
        CancellationToken cancellationToken)
    {
        // Un solo lote, y no un SELECT seguido de un UPDATE: entre dos sentencias caben miles de
        // cosas. SERIALIZABLE sobre el UPDATE no solo bloquea la fila, sino también el rango del
        // índice único (Tipo, Anio), que es lo que impide el INSERT de una fila que ya existe.
        //
        // @ultimo arranca en -1 para distinguir "el UPDATE no encontró la fila" de "encontró una
        // fila cuyo contador es cero": con @@ROWCOUNT quedaría la misma ambigüedad en el caso
        // inverso, y un número 0 se confundiría con la señal de que hay que insertar.
        const string Lote = """
            SET XACT_ABORT ON;

            DECLARE @prefijo nvarchar(10), @digitos int, @ultimo bigint = -1;

            UPDATE dbo.Consecutivo WITH (UPDLOCK, SERIALIZABLE)
               SET UltimoNumero = UltimoNumero + 1
            OUTPUT INSERTED.Prefijo, INSERTED.Digitos, INSERTED.UltimoNumero
              INTO @prefijo, @digitos, @ultimo
             WHERE Tipo = @tipo AND Anio = @anio;

            IF @ultimo = -1
                INSERT INTO dbo.Consecutivo (Tipo, Anio, Prefijo, Digitos, UltimoNumero)
                     VALUES (@tipo, @anio, @prefijoNuevo, @digitosNuevo, 1);

            SELECT Prefijo, Digitos, UltimoNumero
              FROM dbo.Consecutivo
             WHERE Tipo = @tipo AND Anio = @anio;
            """;

        var filas = await _sql.ConsultarAsync(
            Lote,
            cancellationToken,
            ("@tipo", (object)(byte)tipo.Id),
            ("@anio", (object)(short)anio),
            ("@prefijoNuevo", prefijoAnio),
            ("@digitosNuevo", tipo.Digitos));

        if (filas.Count != 1)
        {
            // No debería pasar: el lote termina con un SELECT sobre una fila que acaba de existir o
            // de incrementarse. Si llegara cero filas, lo que se perdió fue el UPDATE, y eso se
            // avisa en vez de devolver un número inventado.
            throw new InvalidOperationException(
                $"No se pudo reservar el consecutivo de '{tipo.Codigo}' para {anio}: la base devolvió " +
                $"{filas.Count} fila(s) en lugar de una.");
        }

        var fila = filas[0];
        var prefijo = Convert.ToString(fila["Prefijo"], CultureInfo.InvariantCulture) ?? prefijoAnio;
        var digitos = Convert.ToInt32(fila["Digitos"], CultureInfo.InvariantCulture);
        var numero = Convert.ToInt64(fila["UltimoNumero"], CultureInfo.InvariantCulture);

        return $"{prefijo}{numero.ToString(CultureInfo.InvariantCulture).PadLeft(digitos, '0')}";
    }

    /// <summary>
    /// Muestra el estado de un correlativo <b>sin reservarlo</b>, para la pantalla de
    /// Administración. Devuelve <c>null</c> si el tipo de consecutivo no existe en el catálogo.
    /// </summary>
    public async Task<EstadoConsecutivo?> EstadoAsync(
        string tipoCodigo,
        int anio,
        CancellationToken cancellationToken = default)
    {
        var tipo = await _contexto.TipoConsecutivoCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Codigo == tipoCodigo, cancellationToken);

        if (tipo is null)
        {
            return null;
        }

        var fila = await _sql.ConsultarAsync(
            "SELECT Prefijo, Digitos, UltimoNumero FROM dbo.Consecutivo WHERE Tipo = @tipo AND Anio = @anio",
            cancellationToken,
            ("@tipo", (object)tipo.Id),
            ("@anio", (object)(short)anio));

        var prefijoAnio = $"{tipo.Prefijo}{anio.ToString(CultureInfo.InvariantCulture)}-";

        if (fila.Count == 0)
        {
            // Todavía no se emitió ninguno de ese tipo y año: el estado real es cero, no
            // "desconocido". La pantalla tiene que poder mostrar el próximo número de un
            // correlativo que nadie ha usado, que es justo el caso de un sistema nuevo.
            return new EstadoConsecutivo(
                tipo.Nombre,
                tipo.Prefijo,
                tipo.Digitos,
                0,
                1,
                prefijoAnio);
        }

        var prefijo = Convert.ToString(fila[0]["Prefijo"], CultureInfo.InvariantCulture) ?? prefijoAnio;
        var digitos = Convert.ToInt32(fila[0]["Digitos"], CultureInfo.InvariantCulture);
        var ultimo = Convert.ToInt64(fila[0]["UltimoNumero"], CultureInfo.InvariantCulture);

        return new EstadoConsecutivo(
            tipo.Nombre,
            tipo.Prefijo,
            digitos,
            ultimo,
            ultimo + 1,
            prefijo);
    }
}

/// <summary>
/// Lo que ve la pantalla de consecutivos. No es la entidad: la entidad no sale de la capa, y menos
/// todavía de la base hacia la pantalla.
/// </summary>
/// <param name="TipoNombre">Etiqueta del tipo de consecutivo, para la columna de la tabla.</param>
/// <param name="PrefijoCatalogo">El prefijo tal como está en el catálogo, sin el año.</param>
/// <param name="Digitos">Ceros a la izquierda con los que se rellena el número.</param>
/// <param name="UltimoEmitido">Cuántos se han emitido. <c>0</c> si todavía ninguno.</param>
/// <param name="Siguiente">El número que saldría en la próxima emisión.</param>
/// <param name="PrefijoAnio">El prefijo real del año, con el año ya pegado.</param>
public sealed record EstadoConsecutivo(
    string TipoNombre,
    string PrefijoCatalogo,
    int Digitos,
    long UltimoEmitido,
    long Siguiente,
    string PrefijoAnio);
