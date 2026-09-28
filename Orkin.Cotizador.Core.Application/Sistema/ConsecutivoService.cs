using System.Data;
using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Sistema;

/// <summary>
/// Emite la numeración oficial de los documentos que nacen en la plataforma.
/// <para><b>Qué se numera acá y qué no:</b> se numera lo que la plataforma crea. Hoy, la
/// cotización. Los códigos de cliente, producto y activo <b>no</b> se numeran acá: esos los trae
/// la transformación desde el ERP o desde el archivo que el cliente entrega, y un código que
/// llega de afuera con su propio identificador no se renumera —renumerarlo rompe el
/// enlace con el ERP del que vino—. Por eso <c>TipoConsecutivoCatalogo</c> tiene una sola fila
/// sembrada y no cuatro.</para>
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
/// <para><b>Por qué el formato se congela con el primer número del año:</b> se copia a la fila la
/// primera vez y ahí se queda. Así, cambiar la plantilla en Administración a mitad de año no
/// renumera hacia atrás lo ya emitido, que es exactamente lo que no puede pasar con una numeración
/// que la administración usa para citar.</para>
/// </summary>
public sealed class ConsecutivoService
{
    /// <summary>
    /// Año que se usa en la fila de contador de un tipo que <b>no</b> reinicia por año.
    /// <para><b>Por qué un año de mentira en vez de quitar la columna:</b> <c>Anio</c> es parte del
    /// índice único junto con <c>Tipo</c>, y quitarlo del índice cambiaría la clave de una tabla
    /// que ya está en producción. Con un solo valor fijo, la fila es única para siempre y el
    /// contador no vuelve a empezar. El año real del documento sigue viviendo en
    /// <c>dbo.Cotizacion.Anio</c>, que es un dato del negocio y no del formato.</para>
    /// <para><b>Por qué 0 y no -1:</b> porque 0 es un año que nadie va a escribir, y un -1 acá
    /// parecería un dato de negocio cuando es un detalle de implementación.</para>
    /// </summary>
    private const short AnioSinReinicio = 0;

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
    /// <param name="anchoMaximo">Ancho de la columna donde el número va a quedar. Se comprueba
    /// antes de devolver, porque truncar un número oficial es peor que negarse a emitirlo.</param>
    public async Task<string> SiguienteAsync(
        string tipoCodigo,
        int anio,
        int anchoMaximo = AnchoPorDefecto,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoCodigo);
        ArgumentOutOfRangeException.ThrowIfLessThan(anio, 1);

        if (anchoMaximo <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(anchoMaximo), anchoMaximo, "El ancho tiene que ser positivo.");
        }

        var tipo = await _contexto.TipoConsecutivoCatalogo
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Codigo == tipoCodigo && t.EsActivo, cancellationToken)
            ?? throw new CatalogoNoResueltoException(nameof(TipoConsecutivoCatalogo), tipoCodigo);

        // El año del contador no es el año del documento cuando el tipo no reinicia. La plantilla
        // puede no llevar {ANIO} y eso está bien; lo que no puede ser es que el contador reinicie
        // igual, porque en enero volvería a salir ABC0001 y chocaría contra el índice único. La
        // base lo impide con el CHECK de la plantilla, así que acá el año del contador sale de la
        // bandera y no de lo que diga la plantilla.
        var anioDelContador = tipo.ReiniciaPorAnio ? (short)anio : AnioSinReinicio;

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
            var numero = await ReservarAsync(tipo, anio, anioDelContador, cancellationToken);

            // El número ya está reservado aquí: si no cabe en la columna, el hueco queda igual y
            // eso se avisa. Truncarlo sería accesible, pero el número truncado ya no es el que se
            // reservó, y reusar el reservado para otro documento rompería la secuencia.
            if (numero.Length > anchoMaximo)
            {
                throw new ConsecutivoNoCabeException(
                    tipo.Codigo, numero, anchoMaximo);
            }

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

    /// <summary>
    /// Ancho con el que se resuelve el número si quien llama no dice otro.
    /// <para><b>Por qué un default y no un parámetro obligatorio:</b> el ancho depende de la
    /// columna de destino, que es un detalle de la tabla, no del consecutivo. El default es el
    /// ancho con el que está <c>dbo.Cotizacion.Numero</c> hoy, y quien vaya a guardar el número en
    /// otra columna tiene que pasar el ancho de esa columna. Un default silencioso sería una
    /// trampa: un número de 35 chars guardado en una columna de 20 se cortaría, y el
    /// <c>CHECK</c> de la plantilla no lo puede ver porque la plantilla no sabe a qué tabla va.</para>
    /// </summary>
    public const int AnchoPorDefecto = 40;

    private async Task<string> ReservarAsync(
        TipoConsecutivoCatalogo tipo,
        int anio,
        short anioDelContador,
        CancellationToken cancellationToken)
    {
        // Un solo lote, y no un SELECT seguido de un UPDATE: entre dos sentencias caben miles de
        // cosas. SERIALIZABLE sobre el UPDATE no solo bloquea la fila, sino también el rango del
        // índice único (Tipo, Anio), que es lo que impide el INSERT de una fila que ya existe.
        //
        // @ultimo arranca en -1 para distinguir "el UPDATE no encontró la fila" de "encontró una
        // fila cuyo contador es cero": con @@ROWCOUNT quedaría la misma ambigüedad en el caso
        // inverso, y un número 0 se confundiría con la señal de que hay que insertar.
        // El destino intermedio es una variable de tabla y no las variables escalares directo:
        // OUTPUT...INTO no admite una lista de variables, y una tabla de una fila es la única
        // forma de devolver las columnas congeladas y el contador nuevo en una sola ida.
        const string Lote = """
            DECLARE @Reserva TABLE (
                Plantilla nvarchar(80),
                Prefijo nvarchar(20),
                Sufijo nvarchar(20),
                Digitos int,
                UltimoNumero bigint);

            UPDATE dbo.Consecutivo WITH (UPDLOCK, SERIALIZABLE)
               SET UltimoNumero = UltimoNumero + 1
            OUTPUT INSERTED.Plantilla, INSERTED.Prefijo, INSERTED.Sufijo,
                   INSERTED.Digitos, INSERTED.UltimoNumero
              INTO @Reserva
             WHERE Tipo = @tipo AND Anio = @anioContador;

            DECLARE @plantilla nvarchar(80), @prefijo nvarchar(20), @sufijo nvarchar(20),
                    @digitos int, @ultimo bigint = -1;

            SELECT @plantilla = Plantilla, @prefijo = Prefijo, @sufijo = Sufijo,
                   @digitos = Digitos, @ultimo = UltimoNumero
              FROM @Reserva;

            IF @ultimo = -1
                INSERT INTO dbo.Consecutivo
                       (Tipo, Anio, Plantilla, Prefijo, Sufijo, Digitos, UltimoNumero)
                VALUES (@tipo, @anioContador, @plantillaNueva, @prefijoNuevo, @sufijoNuevo,
                        @digitosNuevo, 1);

            SELECT Plantilla, Prefijo, Sufijo, Digitos, UltimoNumero
              FROM dbo.Consecutivo
             WHERE Tipo = @tipo AND Anio = @anioContador;
            """;

        var filas = await _sql.ConsultarAsync(
            Lote,
            cancellationToken,
            ("@tipo", (object)(byte)tipo.Id),
            ("@anioContador", (object)anioDelContador),
            ("@plantillaNueva", tipo.Plantilla),
            ("@prefijoNuevo", tipo.Prefijo),
            ("@sufijoNuevo", tipo.Sufijo),
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

        // Lo que se compone es lo congelado en la fila, no lo que dice el catálogo ahora. En el
        // primer número del año las dos cosas son iguales; en el número 400 ya no, y el que vale
        // es lo congelado. Por eso el fallback es la fila y no el catálogo: si la fila, como
        // excepción, no trae Plantilla —solo pasa si se editó la base a mano— componer con el
        // catálogo es mejor que no devolver nada.
        var plantilla = Convert.ToString(fila["Plantilla"], CultureInfo.InvariantCulture);
        var prefijo = Convert.ToString(fila["Prefijo"], CultureInfo.InvariantCulture);
        var sufijo = Convert.ToString(fila["Sufijo"], CultureInfo.InvariantCulture);
        var digitos = Convert.ToInt32(fila["Digitos"], CultureInfo.InvariantCulture);
        var numero = Convert.ToInt64(fila["UltimoNumero"], CultureInfo.InvariantCulture);

        return Componer(
            plantilla ?? tipo.Plantilla,
            prefijo ?? tipo.Prefijo,
            sufijo ?? tipo.Sufijo,
            digitos,
            numero,
            anio,
            tipo.Codigo);
    }

    /// <summary>
    /// Expande la plantilla y devuelve el número completo.
    /// <para><b>Por qué se arma con un <see cref="StringBuilder"/> y no con
    /// <c>string.Replace</c>:</b> <c>Replace</c> sobre <c>{NUM}</c> reemplazaría también el
    /// <c>{NUM}</c> que alguien pudiera haber puesto dentro del prefijo, y el resultado sería un
    /// número con la cifra repetida sin que nadie lo note. Recorriendo la plantilla sección por
    /// sección, cada una se consume una vez.</para>
    /// </summary>
    private static string Componer(
        string plantilla,
        string prefijo,
        string sufijo,
        int digitos,
        long numero,
        int anio,
        string tipoCodigo)
    {
        if (string.IsNullOrWhiteSpace(plantilla))
        {
            throw new PlantillaDeConsecutivoInvalida(
                tipoCodigo, "la plantilla está vacía. Un consecutivo necesita al menos {NUM}.");
        }

        if (digitos is < 1 or > 19)
        {
            throw new PlantillaDeConsecutivoInvalida(
                tipoCodigo, $"{digitos} dígitos no es un ancho válido: van de 1 a 19.");
        }

        var resultado = new StringBuilder(plantilla.Length + 24);
        var indice = 0;
        var vistas = new HashSet<string>(StringComparer.Ordinal);

        while (indice < plantilla.Length)
        {
            if (plantilla[indice] != '{')
            {
                resultado.Append(plantilla[indice]);
                indice++;
                continue;
            }

            var cierre = plantilla.IndexOf('}', indice);

            if (cierre < 0)
            {
                throw new PlantillaDeConsecutivoInvalida(
                    tipoCodigo,
                    $"la plantilla '{plantilla}' abre una sección con {{ y nunca la cierra.");
            }

            var seccion = plantilla[(indice + 1)..cierre];

            // La misma sección dos veces es un error y no un resultado raro: el catálogo valida
            // esto con la UDF al guardar, y acá se replica porque una fila pudo haberse escrito
            // antes de que el CHECK existiera. También impide el caso sutil de un {NUM} repetido,
            // que duplicaría la cifra en el medio del número sin que nadie lo notara.
            if (!vistas.Add(seccion))
            {
                throw new PlantillaDeConsecutivoInvalida(
                    tipoCodigo,
                    $"la sección '{{{seccion}}}' aparece más de una vez en '{plantilla}' y " +
                    "cada sección solo puede usarse una vez.");
            }

            switch (seccion)
            {
                case "PRE":
                    resultado.Append(prefijo);
                    break;

                case "SUF":
                    resultado.Append(sufijo);
                    break;

                case "ANIO":
                    resultado.Append(anio.ToString(CultureInfo.InvariantCulture));
                    break;

                case "NUM":
                    // PadLeft no trunca: si el contador llega a 10000 con 4 dígitos, el número
                    // sale con 5. Acortar un número oficial para que quepa en un ancho es peor
                    // que emitir uno más largo, porque dos documentos distintos pasarían a
                    // llamarse igual. El ancho se comprueba aparte y avisa.
                    resultado.Append(numero.ToString(CultureInfo.InvariantCulture).PadLeft(digitos, '0'));
                    break;

                case "":
                    throw new PlantillaDeConsecutivoInvalida(
                        tipoCodigo, $"la plantilla '{plantilla}' tiene una {{}} sin sección.");

                default:
                    throw new PlantillaDeConsecutivoInvalida(
                        tipoCodigo,
                        $"la sección '{{{seccion}}}' no existe. Las que existen son " +
                        "{PRE}, {SUF}, {ANIO} y {NUM}.");
            }

            indice = cierre + 1;
        }

        var compuesto = resultado.ToString();

        if (!plantilla.Contains(MarcaNumero, StringComparison.Ordinal))
        {
            throw new PlantillaDeConsecutivoInvalida(
                tipoCodigo,
                $"la plantilla '{plantilla}' no tiene {{NUM}}, y sin {MarcaNumero} el número " +
                "no incrementa: todos los documentos saldrían con el mismo texto.");
        }

        return compuesto;
    }

    private const string MarcaNumero = "{NUM}";

    /// <summary>
    /// Muestra el estado de un correlativo <b>sin reservarlo</b>, para la pantalla de
    /// Administración. Devuelve <c>null</c> si el tipo de consecutivo no existe en el catálogo.
    /// <para><b>Por qué compone el número de muestra sin tocar la base:</b> la pantalla de
    /// Administración muestra el próximo número, y mostrarlo no puede gastarlo. Por eso compone
    /// con <c>UltimoNumero + 1</c> y no pide un número de verdad.</para>
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

        var anioDelContador = tipo.ReiniciaPorAnio ? (short)anio : AnioSinReinicio;

        var fila = await _sql.ConsultarAsync(
            """
            SELECT Plantilla, Prefijo, Sufijo, Digitos, UltimoNumero
              FROM dbo.Consecutivo
             WHERE Tipo = @tipo AND Anio = @anioContador
            """,
            cancellationToken,
            ("@tipo", (object)tipo.Id),
            ("@anioContador", (object)anioDelContador));

        var siguiente = 1L;
        var emitida = false;
        string? numeroMuestra = null;

        if (fila.Count == 0)
        {
            // Todavía no se emitió ninguno de ese tipo y año: el estado real es cero, no
            // "desconocido". La pantalla tiene que poder mostrar el próximo número de un
            // correlativo que nadie ha usado, que es justo el caso de un sistema nuevo.
            numeroMuestra = Componer(
                tipo.Plantilla, tipo.Prefijo, tipo.Sufijo, tipo.Digitos, siguiente, anio, tipo.Codigo);
        }
        else
        {
            emitida = true;
            siguiente = Convert.ToInt64(fila[0]["UltimoNumero"], CultureInfo.InvariantCulture) + 1;

            // Igual que al emitir, se compone con lo congelado en la fila y no con lo que diga
            // el catálogo. Si se muestra con el catálogo nuevo, la pantalla estaría enseñando un
            // número que la plataforma no emitiría.
            numeroMuestra = Componer(
                Convert.ToString(fila[0]["Plantilla"], CultureInfo.InvariantCulture) ?? tipo.Plantilla,
                Convert.ToString(fila[0]["Prefijo"], CultureInfo.InvariantCulture) ?? tipo.Prefijo,
                Convert.ToString(fila[0]["Sufijo"], CultureInfo.InvariantCulture) ?? tipo.Sufijo,
                Convert.ToInt32(fila[0]["Digitos"], CultureInfo.InvariantCulture),
                siguiente,
                anio,
                tipo.Codigo);
        }

        return new EstadoConsecutivo(
            tipo.Codigo,
            tipo.Nombre,
            tipo.Plantilla,
            tipo.Prefijo,
            tipo.Sufijo,
            tipo.Digitos,
            emitida ? siguiente - 1 : 0,
            siguiente,
            numeroMuestra!,
            tipo.ReiniciaPorAnio);
    }
}

/// <summary>
/// Lo que ve la pantalla de consecutivos. No es la entidad: la entidad no sale de la capa, y menos
/// todavía de la base hacia la pantalla.
/// </summary>
/// <param name="TipoCodigo">Clave de negocio del tipo, para que la UI pueda volver a pedirlo.</param>
/// <param name="TipoNombre">Etiqueta del tipo, para la columna de la tabla.</param>
/// <param name="Plantilla">La plantilla tal como está en el catálogo, con las secciones sin
/// expandir. Es lo que hay que editar para cambiar el formato.</param>
/// <param name="Prefijo">Lo que se expande en <c>{PRE}</c>.</param>
/// <param name="Sufijo">Lo que se expande en <c>{SUF}</c>.</param>
/// <param name="Digitos">Ceros a la izquierda de <c>{NUM}</c>.</param>
/// <param name="UltimoEmitido">Cuántos se han emitido. <c>0</c> si todavía ninguno.</param>
/// <param name="Siguiente">El número correlativo que saldría en la próxima emisión.</param>
/// <param name="NumeroMuestra">Ese mismo número, ya formateado, para leerlo de un vistazo.</param>
/// <param name="ReiniciaPorAnio">Si el contador vuelve a <c>1</c> cada 1 de enero.</param>
public sealed record EstadoConsecutivo(
    string TipoCodigo,
    string TipoNombre,
    string Plantilla,
    string Prefijo,
    string Sufijo,
    int Digitos,
    long UltimoEmitido,
    long Siguiente,
    string NumeroMuestra,
    bool ReiniciaPorAnio);

/// <summary>
/// La plantilla de un tipo de consecutivo no se puede interpretar.
/// <para><b>Por qué es una excepción propia y no un <c>ArgumentException</c>:</b> porque la
/// plantilla casi nunca viene del código de quien llama, sino de una fila del catálogo que alguien
/// escribió en Administración. El error que tiene que ver quien la causó es "esa plantilla está
/// mal", no "el argumento no es válido", y el mensaje dice qué sección no existe o qué falta en
/// vez de repetir el nombre del parámetro.</para>
/// <para><b>Por qué la base la atrapa antes:</b> el <c>CHECK</c> con
/// <c>dbo.FnPlantillaConsecutivoValida</c> rechaza casi todos estos casos al guardar el catálogo,
/// así que esta excepción es la red de seguridad, no el camino normal. Está porque una fila puede
/// haber sido escrita antes de que el <c>CHECK</c> existiera.</para>
/// </summary>
public sealed class PlantillaDeConsecutivoInvalida : InvalidOperationException
{
    public PlantillaDeConsecutivoInvalida(string tipoCodigo, string motivo)
        : base($"La plantilla del consecutivo '{tipoCodigo}' no sirve: {motivo}")
        => TipoCodigo = tipoCodigo;

    public string TipoCodigo { get; }
}

/// <summary>
/// El número ya reservado no cabe en la columna donde se va a guardar.
/// <para><b>Por qué se lanza después de reservar y no antes:</b> la composición depende de lo que
/// está congelado en la fila de <c>dbo.Consecutivo</c>, y eso solo existe después de la primera
/// emisión. No hay forma de saber cuánto mide el número sin haberlo reservado, así que el número
/// queda gastado: es un hueco visible. Truncarlo para que quepa sería peor —el documento diría un
/// número que nadie reservó—, y este error es de configuración: o se agranda la columna o se
/// acorta la plantilla.</para>
/// <para><b>Por qué es una excepción aparte de <see cref="PlantillaDeConsecutivoInvalida"/>:</b>
/// ahí la plantilla no se puede interpretar; acá se interpreta perfecto y el problema es que el
/// resultado no cabe. El mensaje que necesita quien lo lee es distinto: uno dice qué sección no
/// existe, y este dice cuánto mide el número y cuánto puede medir.</para>
/// </summary>
public sealed class ConsecutivoNoCabeException : InvalidOperationException
{
    public ConsecutivoNoCabeException(string tipoCodigo, string numero, int anchoMaximo)
        : base($"El consecutivo '{tipoCodigo}' generó '{numero}' ({(numero.Length)} caracteres) y " +
               $"la columna de destino admite {anchoMaximo}. O se agranda la columna o se acorta " +
               "la plantilla; un número cortado no se puede emitir.")
    {
        TipoCodigo = tipoCodigo;
        Numero = numero;
        AnchoMaximo = anchoMaximo;
    }

    public string TipoCodigo { get; }
    public string Numero { get; }
    public int AnchoMaximo { get; }
}
