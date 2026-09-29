using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Application.Carga;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Carga;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Integracion;

/// <summary>Resultado de probar la conexión al ERP sin leer datos.</summary>
public sealed record ResultadoPruebaConexion(
    bool Exito,
    string Mensaje,
    string? VersionServidor = null,
    double? Milisegundos = null);

/// <summary>
/// Caso de uso de la integración (Fase 12): administra las conexiones al ERP de los clientes y
/// los flujos que llenan nuestros catálogos desde ellas.
/// <para>Es un servicio de aplicación y no un controlador porque las tres cosas que hace —probar
/// una conexión, ejecutar un flujo, dejar la bitácora— ocurren igual desde la API, desde el
/// scheduler o desde una prueba.</para>
/// </summary>
public sealed class IntegracionService
{
    /// <summary>
    /// El intervalo más corto que se puede programar. Tiene que coincidir con el
    /// <c>CK_ErpFlujo_IntervaloMinimo</c> de la base; la base manda y esta constante solo sirve
    /// para poder explicar el límite con un mensaje que se pueda leer en la pantalla.
    /// </summary>
    public const int IntervaloMinimoSegundos = 10;

    private readonly CotizadorDbContext _contexto;
    private readonly CifradorSecreto _cifrador;
    private readonly ProcesoCargaService _proceso;
    private readonly IContextoUsuario _usuario;

    public IntegracionService(
        CotizadorDbContext contexto,
        CifradorSecreto cifrador,
        ProcesoCargaService proceso,
        IContextoUsuario usuario)
    {
        _contexto = contexto;
        _cifrador = cifrador;
        _proceso = proceso;
        _usuario = usuario;
    }

    // ── Conexiones ────────────────────────────────────────────────────────────

    public Task<List<ErpConexion>> ListarConexionesAsync(CancellationToken cancellationToken = default)
        => _contexto.ErpConexion
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .ToListAsync(cancellationToken);

    public Task<ErpConexion?> ObtenerConexionAsync(Guid id, CancellationToken cancellationToken = default)
        => _contexto.ErpConexion.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <summary>
    /// Crea una conexión. La contraseña se cifra antes de tocar la base: no hay ningún momento
    /// en que la contraseña en claro esté en una entidad que se pueda volcar a un log.
    /// </summary>
    public async Task<ErpConexion> CrearConexionAsync(
        string nombre,
        string? descripcion,
        string servidor,
        int? puerto,
        string nombreBaseDatos,
        string? esquema,
        string? usuario,
        string? contrasena,
        int tiempoEsperaSegundos = 30,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(servidor);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreBaseDatos);

        if (await _contexto.ErpConexion.AnyAsync(c => c.Nombre == nombre, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe una conexión llamada '{nombre}'.");
        }

        var entidad = new ErpConexion
        {
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim(),
            TipoProveedor = IntegracionCatalogos.TipoProveedorSqlServer,
            Servidor = servidor.Trim(),
            Puerto = puerto,
            NombreBaseDatos = nombreBaseDatos.Trim(),
            Esquema = string.IsNullOrWhiteSpace(esquema) ? null : esquema.Trim(),
            MetodoAutenticacion = IntegracionCatalogos.MetodoAutenticacionBasica,
            Usuario = usuario?.Trim(),
            ContrasenaCifrada = _cifrador.Cifrar(contrasena),
            TiempoEsperaSegundos = (tiempoEsperaSegundos is >= 1 and <= 600) ? (int)tiempoEsperaSegundos : 30,
            EsSoloLectura = true,
            Estado = IntegracionCatalogos.EstadoConexionBorrador
        };

        _contexto.ErpConexion.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);
        return entidad;
    }

    /// <summary>
    /// Actualiza una conexión. La contraseña solo se reescribe si viene una nueva: dejarla vacía
    /// en el formulario no debe borrar la que ya funciona.
    /// </summary>
    public async Task<ErpConexion> ActualizarConexionAsync(
        Guid id,
        string? descripcion,
        string servidor,
        int? puerto,
        string nombreBaseDatos,
        string? esquema,
        string? usuario,
        string? contrasena,
        int? tiempoEsperaSegundos = null,
        CancellationToken cancellationToken = default)
    {
        var entidad = await ObtenerConexionAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("La conexión no existe.");

        entidad.Descripcion = descripcion?.Trim();
        entidad.Servidor = servidor.Trim();
        entidad.Puerto = puerto;
        entidad.NombreBaseDatos = nombreBaseDatos.Trim();
        entidad.Esquema = string.IsNullOrWhiteSpace(esquema) ? null : esquema.Trim();
        entidad.Usuario = usuario?.Trim();

        if (!string.IsNullOrEmpty(contrasena))
        {
            entidad.ContrasenaCifrada = _cifrador.Cifrar(contrasena);
        }

        if (tiempoEsperaSegundos is >= 1 and <= 600)
        {
            entidad.TiempoEsperaSegundos = tiempoEsperaSegundos.Value;
        }

        await _contexto.SaveChangesAsync(cancellationToken);
        return entidad;
    }

    /// <summary>
    /// Abre la conexión y ejecuta un <c>SELECT 1</c>. Es la prueba que un administrador corre
    /// antes de guardar un flujo: si el servidor no responde o la contraseña está mal, se entera
    /// sin ensuciar ningún catálogo con una carga fallida.
    /// <para>El resultado (ok o error) se guarda en la conexión para que la lista muestre el
    /// último estado sin tener que preguntar otra vez.</para>
    /// </summary>
    public async Task<ResultadoPruebaConexion> ProbarConexionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var conexion = await ObtenerConexionAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("La conexión no existe.");

        var cadena = ConstruirCadena(conexion);
        var mensaje = "Conexión correcta.";

        if (string.IsNullOrWhiteSpace(cadena))
        {
            mensaje = "Falta usuario o contraseña: la conexión no tiene credenciales configuradas.";
        }
        else
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                await using var sql = new SqlConnection(cadena);
                await sql.OpenAsync(cancellationToken);

                await using var comando = new SqlCommand("SELECT @@VERSION", sql);
                var version = await comando.ExecuteScalarAsync(cancellationToken);
                reloj.Stop();

                conexion.UltimaConexionOkUtc = DateTime.UtcNow;
                conexion.UltimoError = null;
                conexion.UltimoErrorUtc = null;
                conexion.Estado = IntegracionCatalogos.EstadoConexionActiva;
                await _contexto.SaveChangesAsync(cancellationToken);

                // "@@VERSION" empieza con el número de versión del motor ("16.0.1000.6 - ..."), que
                // es lo que el administrador necesita ver para reportar un problema de conexión.
                var numero = (version as string ?? string.Empty).Split('-').FirstOrDefault()?.Trim();
                return new ResultadoPruebaConexion(
                    true, mensaje, string.IsNullOrWhiteSpace(numero) ? null : numero,
                    Math.Round(reloj.Elapsed.TotalMilliseconds));
            }
            catch (SqlException ex)
            {
                reloj.Stop();
                mensaje = $"No se pudo conectar: {ex.Message}";

                conexion.UltimaConexionOkUtc = null;
                conexion.UltimoError = ex.Message;
                conexion.UltimoErrorUtc = DateTime.UtcNow;
                conexion.Estado = IntegracionCatalogos.EstadoConexionConError;
                await _contexto.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                reloj.Stop();
                mensaje = $"No se pudo conectar: {ex.Message}";

                conexion.UltimaConexionOkUtc = null;
                conexion.UltimoError = ex.Message;
                conexion.UltimoErrorUtc = DateTime.UtcNow;
                conexion.Estado = IntegracionCatalogos.EstadoConexionConError;
                await _contexto.SaveChangesAsync(cancellationToken);
            }
        }

        if (mensaje.StartsWith("Falta", StringComparison.Ordinal))
        {
            conexion.UltimoError = mensaje;
            conexion.UltimoErrorUtc = DateTime.UtcNow;
            conexion.Estado = IntegracionCatalogos.EstadoConexionConError;
            await _contexto.SaveChangesAsync(cancellationToken);
        }

        return new ResultadoPruebaConexion(false, mensaje);
    }

    /// <summary>
    /// Arma la cadena de conexión a partir de la conexión guardada, descifrando la contraseña.
    /// <c>Application Name</c> identifica la conexión en las vistas de administración del SQL
    /// Server del cliente: si el ERPBeingxFan sees una consulta de ORKIN, se ve por qué.
    /// </summary>
    private string? ConstruirCadena(ErpConexion conexion)
    {
        var contrasena = _cifrador.Descifrar(conexion.ContrasenaCifrada);

        if (string.IsNullOrWhiteSpace(contrasena))
        {
            return null;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = conexion.Puerto.HasValue
                ? $"{conexion.Servidor},{conexion.Puerto.Value}"
                : conexion.Servidor,
            InitialCatalog = conexion.NombreBaseDatos,
            UserID = conexion.Usuario,
            Password = contrasena,
            ApplicationName = "ORKIN Cotizador ETL",
            ConnectTimeout = conexion.TiempoEsperaSegundos,
            // El ERP del cliente casi nunca tiene un certificado válido para el nombre por el que
            // lo conocemos. Sin esto la conexión falla por el certificado, no por la contraseña,
            // que es un error que esconde el problema real. Es una conexión de solo lectura.
            TrustServerCertificate = true
        };

        return builder.ConnectionString;
    }

    // ── Flujos ────────────────────────────────────────────────────────────────

    public Task<List<ErpFlujo>> ListarFlujosAsync(CancellationToken cancellationToken = default)
        => _contexto.ErpFlujo
            .AsNoTracking()
            .OrderBy(f => f.Nombre)
            .ToListAsync(cancellationToken);

    public Task<ErpFlujo?> ObtenerFlujoAsync(Guid id, CancellationToken cancellationToken = default)
        => _contexto.ErpFlujo.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<ErpFlujo> CrearFlujoAsync(
        Guid erpConexionId,
        string nombre,
        string? descripcion,
        string catalogo,
        string origen,
        string? mapeoJson = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogo);
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);

        var conexion = await ObtenerConexionAsync(erpConexionId, cancellationToken)
            ?? throw new KeyNotFoundException("La conexión no existe.");

        if (await _contexto.ErpFlujo.AnyAsync(f => f.ErpConexionId == erpConexionId && f.Nombre == nombre, cancellationToken))
        {
            throw new InvalidOperationException($"Esa conexión ya tiene un flujo llamado '{nombre}'.");
        }

        ValidarMapeo(mapeoJson);

        var entidad = new ErpFlujo
        {
            ErpConexionId = conexion.Id,
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim(),
            Catalogo = catalogo.Trim().ToUpperInvariant(),
            Origen = origen.Trim(),
            MapeoJson = string.IsNullOrWhiteSpace(mapeoJson) ? null : mapeoJson.Trim(),
            TipoDisparador = IntegracionCatalogos.TipoDisparadorManual,
            // El desfase se tira una vez, acá, y no se vuelve a tocar. Fijarlo desde el
            // nacimiento del flujo es lo que reparte la carga entre los clientes: si se
            // recalculara en cada guardado, todos los flujos reprogramados el mismo día acabarían
            // con desfases alineados y volverían a pegarle al ERP del cliente a la vez.
            DesfaseSegundos = (short)Random.Shared.Next(0, 31),
            ModoExtraccion = IntegracionCatalogos.ModoExtraccionCompleta,
            Estado = IntegracionCatalogos.EstadoFlujoActivo
        };

        _contexto.ErpFlujo.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);
        return entidad;
    }

    // ── Programación (12.11) ──────────────────────────────────────────────────

    /// <summary>
    /// Programa un flujo: cada cuántos segundos correr solo, o lo devuelve a manual.
    /// <para>Al activar la programación la primera corrida es <b>inmediata</b>
    /// (<c>ProximaEjecucionUtc = ahora</c>), no «dentro de un intervalo». Quien enciende el
    /// automático lo hace para ver que funciona, y esperar un ciclo entero para comprobarlo es
    /// pedirle que crea que no funcionó.</para>
    /// </summary>
    public async Task<ErpFlujo> ConfigurarProgramacionAsync(
        Guid id,
        string tipoDisparador,
        int? intervaloSegundos,
        int? maximoRegistrosPorCorrida = null,
        short? maximoFallosConsecutivos = null,
        CancellationToken cancellationToken = default)
    {
        var entidad = await ObtenerFlujoAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El flujo no existe.");

        var tipo = InterpretarTipoDisparador(tipoDisparador);

        // Se valida acá y no solo con el CHECK de la base. El CHECK no se puede saltar, pero un
        // `DbUpdateException` con el texto del constraint no es una respuesta que se pueda
        // mostrar tal cual en la pantalla; este mensaje sí.
        if (tipo != IntegracionCatalogos.TipoDisparadorManual
            && intervaloSegundos is null)
        {
            throw new ArgumentException(
                "Un flujo programado necesita un intervalo. Para volverlo manual, deje el intervalo vacío.");
        }

        if (intervaloSegundos is < IntervaloMinimoSegundos)
        {
            throw new ArgumentException(
                $"El intervalo no puede ser menor de {IntervaloMinimoSegundos} segundos. " +
                "Por debajo de eso son más de 8.600 consultas diarias contra el ERP del cliente, " +
                "y eso no se le pide a la base de producción de nadie.");
        }

        if (maximoRegistrosPorCorrida is <= 0)
        {
            throw new ArgumentException("El máximo de registros por corrida tiene que ser mayor que cero, o vacío para no poner tope.");
        }

        entidad.TipoDisparador = tipo;
        entidad.IntervaloSegundos = tipo == IntegracionCatalogos.TipoDisparadorManual
            ? null
            : intervaloSegundos;

        if (maximoRegistrosPorCorrida is not null)
        {
            entidad.MaximoRegistrosPorCorrida = maximoRegistrosPorCorrida;
        }

        if (maximoFallosConsecutivos is > 0)
        {
            entidad.MaximoFallosConsecutivos = maximoFallosConsecutivos.Value;
        }

        entidad.EsPausado = false;
        entidad.MotivoPausa = null;
        entidad.FallosConsecutivos = 0;
        entidad.Estado = IntegracionCatalogos.EstadoFlujoActivo;

        // Inmediata, no «dentro del intervalo». Ver la nota del método.
        entidad.ProximaEjecucionUtc = tipo == IntegracionCatalogos.TipoDisparadorManual
            ? null
            : DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);
        return entidad;
    }

    /// <summary>Pausa un flujo a mano. El motor deja de tocarlo y queda escrito por qué.</summary>
    public async Task<ErpFlujo> PausarFlujoAsync(
        Guid id,
        string? motivo = null,
        CancellationToken cancellationToken = default)
    {
        var entidad = await ObtenerFlujoAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El flujo no existe.");

        entidad.EsPausado = true;
        // Se borra la ventana, no se deja en el pasado: un flujo pausado no tiene «próxima
        // corrida», y dejarla vencida haría que la pantalla dijera que está al día cuando en
        // realidad no se está sincronizando desde hace una hora.
        entidad.ProximaEjecucionUtc = null;
        entidad.MotivoPausa = string.IsNullOrWhiteSpace(motivo)
            ? "Pausado a mano desde la pantalla de orígenes."
            : motivo.Trim();

        await _contexto.SaveChangesAsync(cancellationToken);
        return entidad;
    }

    /// <summary>
    /// Reanuda un flujo pausado, sea a mano o por el corte de fallos. La primera corrida sale
    /// inmediata, y el contador de fallos vuelve a cero: si no, el motor pausaría otra vez en la
    /// primera vuelta sin ni siquiera haber intentado una.
    /// </summary>
    public async Task<ErpFlujo> ReanudarFlujoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entidad = await ObtenerFlujoAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El flujo no existe.");

        entidad.EsPausado = false;
        entidad.MotivoPausa = null;
        entidad.FallosConsecutivos = 0;
        entidad.Estado = IntegracionCatalogos.EstadoFlujoActivo;
        entidad.UltimoError = null;
        entidad.ProximaEjecucionUtc = entidad.IntervaloSegundos is null
            ? null
            : DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);
        return entidad;
    }

    /// <summary>
    /// Traduce el texto que manda la pantalla al byte de la base. Acepta el nombre o el número,
    /// porque una llamada hecha a mano con <c>curl</c> no va a saber el nombre de memoria.
    /// </summary>
    private static byte InterpretarTipoDisparador(string? texto)
    {
        var t = texto?.Trim();

        return t?.ToUpperInvariant() switch
        {
            "MANUAL" or "1" => IntegracionCatalogos.TipoDisparadorManual,
            "PROGRAMADO" or "3" => IntegracionCatalogos.TipoDisparadorProgramado,
            "EVENTO" or "5" => IntegracionCatalogos.TipoDisparadorEvento,
            null or "" => IntegracionCatalogos.TipoDisparadorManual,
            _ => throw new ArgumentException(
                $"'{texto}' no es un tipo de disparador válido. Use MANUAL, PROGRAMADO o EVENTO.")
        };
    }

    public async Task<ErpFlujo> ActualizarFlujoAsync(
        Guid id,
        string? descripcion,
        string catalogo,
        string origen,
        string? mapeoJson = null,
        CancellationToken cancellationToken = default)
    {
        var entidad = await ObtenerFlujoAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El flujo no existe.");

        ArgumentException.ThrowIfNullOrWhiteSpace(catalogo);
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);

        ValidarMapeo(mapeoJson);

        entidad.Descripcion = descripcion?.Trim();
        entidad.Catalogo = catalogo.Trim().ToUpperInvariant();
        entidad.Origen = origen.Trim();
        entidad.MapeoJson = string.IsNullOrWhiteSpace(mapeoJson) ? null : mapeoJson.Trim();

        await _contexto.SaveChangesAsync(cancellationToken);
        return entidad;
    }

    public async Task DarDeBajaFlujoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entidad = await ObtenerFlujoAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El flujo no existe.");

        entidad.IsActive = false;
        entidad.Estado = IntegracionCatalogos.EstadoFlujoBorrador;
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// El mapeo es un JSON de objeto plano. Se valida al guardar, no al ejecutar: un error de
    /// tipeo en una llave del mapeo se detecta mientras se configura, no cuando el ETL corre
    /// a las tres de la mañana y deja el catálogo a medias.
    /// </summary>
    private static void ValidarMapeo(string? mapeoJson)
    {
        if (string.IsNullOrWhiteSpace(mapeoJson))
        {
            return;
        }

        try
        {
            using var documento = JsonDocument.Parse(mapeoJson);
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("El mapeo debe ser un objeto JSON {\"columnaOrigen\": \"campoDestino\"}.");
            }

            foreach (var propiedad in documento.RootElement.EnumerateObject())
            {
                if (propiedad.Value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException(
                        $"El valor del mapeo de '{propiedad.Name}' debe ser texto (el nombre del campo destino).");
                }
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"El mapeo no es un JSON válido: {ex.Message}");
        }
    }

    // ── Ejecución ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Corre un flujo: lee del origen (la vista, tabla o consulta que el cliente configuró) y
    /// llena el catálogo con el <see cref="ICargadorCatalogo"/> correspondiente.
    /// <para>La corrida manual <b>siempre</b> deja fila en la bitácora
    /// <c>int.SincronizacionEjecucion</c>, haya éxito o no: alguien la pidió a mano y tiene que
    /// poder ver qué pasó, incluso si no pasó nada —«lo corrí y no se movió nada» es una
    /// respuesta, y sin bitácora sería indistinguible de «nunca lo corrí».</para>
    /// </summary>
    public async Task<ResultadoCarga> EjecutarFlujoAsync(
        Guid erpFlujoId,
        CancellationToken cancellationToken = default)
        => await CorrerFlujoAsync(erpFlujoId, esSimulacion: false, registrarSiempre: true, cancellationToken);

    /// <summary>
    /// Corre el flujo como lo haría el motor de programación, sin que nadie lo haya pedido: la
    /// corrida que no cambia nada no deja fila.
    /// <para>Es la contraparte de <see cref="EjecutarFlujoAsync"/> en la única cosa en que se
    /// diferencian, y la razón es aritmética. Un flujo cada 30 segundos son 2.880 corridas
    /// diarias; a 10 segundos, 8.640. Anotarlas todas llena la bitácora de filas que dicen
    /// «0 creados, 0 actualizados, 0 rechazados» y borra de ella la información para la que
    /// existe: qué le pasó a este catálogo.</para>
    /// <para>Lo que sí se anota siempre es el fallo, porque una sincronización rota que no
    /// aparece en ninguna parte es indistinguible de un flujo que nadie configuró.</para>
    /// </summary>
    public async Task<ResultadoCarga> EjecutarFlujoProgramadoAsync(
        Guid erpFlujoId,
        CancellationToken cancellationToken = default)
        => await CorrerFlujoAsync(erpFlujoId, esSimulacion: false, registrarSiempre: false, cancellationToken);

    /// <summary>
    /// Corre el flujo <b>sin escribir nada</b> y devuelve lo que haría, con el detalle de los
    /// campos que tocaría en cada fila.
    /// <para>La diferencia con <see cref="EjecutarFlujoAsync"/> no está en la lectura, que es
    /// idéntica, sino en dos cosas que se saltan: la escritura en el catálogo y la fila de la
    /// bitácora. Lo segundo importa más de lo que parece —una simulación no es una corrida, y
    /// anotarla en la bitácora haría que la lista de ejecuciones dijera que el catálogo se
    /// tocó cuando no se tocó—. Quien mira la bitácora para saber qué le pasó al catálogo tiene
    /// que poder confiar en que cada fila de esa tabla fue una carga real.</para>
    /// <para>Sí se lee del ERP, y por eso una simulación puede fallar si el ERP está caído: es
    /// el mismo dato el que se está previendo, y una previdencia sin haber leído no prevee
    /// nada.</para>
    /// </summary>
    public async Task<ResultadoCarga> SimularFlujoAsync(
        Guid erpFlujoId,
        CancellationToken cancellationToken = default)
        => await CorrerFlujoAsync(erpFlujoId, esSimulacion: true, registrarSiempre: false, cancellationToken);

    private async Task<ResultadoCarga> CorrerFlujoAsync(
        Guid erpFlujoId,
        bool esSimulacion,
        bool registrarSiempre,
        CancellationToken cancellationToken)
    {
        var flujo = await ObtenerFlujoAsync(erpFlujoId, cancellationToken)
            ?? throw new KeyNotFoundException("El flujo no existe.");

        var conexion = await ObtenerConexionAsync(flujo.ErpConexionId, cancellationToken)
            ?? throw new KeyNotFoundException("La conexión del flujo no existe.");

        var inicio = DateTime.UtcNow;
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        // El latido se fija al empezar, no al terminar. Se fija acá porque «cuándo intentamos por
        // última vez» es una pregunta del intento, y si la corrida se cuelga o el proceso muere
        // a media lectura, el latido ya está puesto y la pantalla no muestra un flujo congelado
        // como si no se hubiera Corrido en días.
        if (!esSimulacion)
        {
            flujo.UltimoIntentoUtc = inicio;
        }

        var ejecucion = new SincronizacionEjecucion
        {
            ErpConexionId = conexion.Id,
            ErpFlujoId = flujo.Id,
            TipoDisparador = flujo.TipoDisparador,
            Estado = IntegracionCatalogos.EstadoSincronizacionEnCurso,
            InicioUtc = inicio,
            SolicitadoPor = _usuario.UsuarioId
        };

        // La fila de bitácora NO se agrega todavía. Se agrega al final, cuando ya se sabe si esta
        // corrida dejó rastro, porque para la programada esa es la decisión y tomarla antes sería
        // tomarla a ciegas. Agregarla antes no cambiaba nada en la base —EF no escribe hasta el
        // SaveChanges— y por eso moverla acá no cuesta una escritura: cuesta una línea de código
        // menos que leer.
        //
        // En simulación no se agrega nunca, y con ella no se toca el estado del flujo ni el de la
        // conexión. Ver la nota de SimularFlujoAsync: la bitácora tiene que poder leerse como
        // "esto es lo que le pasó al catálogo", sin filas de mentira en el medio.

        var cadena = ConstruirCadena(conexion);

        if (string.IsNullOrWhiteSpace(cadena))
        {
            reloj.Stop();

            const string mensaje = "La conexión no tiene usuario o contraseña configurados. " +
                "Vuelva a editarla y guárdela de nuevo para cifrar la contraseña.";

            if (!esSimulacion)
            {
                await RegistrarFalloAsync(flujo, conexion, ejecucion, reloj, mensaje);
            }

            // Faltan credenciales: es un problema de configuración de la conexión, no del ERP.
            // Se devuelve como error de validación (400) y no como fallo del servidor.
            throw new InvalidOperationException(mensaje);
        }

        var configuracion = new ConfiguracionCarga
        {
            Catalogo = flujo.Catalogo,
            TipoOrigen = "SQL_SERVER",
            MapeoColumnas = LeerMapeo(flujo.MapeoJson),
            ErpConexionId = conexion.Id,
            EsSimulacion = esSimulacion
        };

        try
        {
            var lector = new LectorSqlServer(cadena, flujo.Origen, conexion.TiempoEsperaSegundos);
            var resultado = await _proceso.EjecutarAsync(configuracion, lector, cancellationToken);

            reloj.Stop();

            if (!esSimulacion)
            {
                // La corrida programada que no movió nada no deja fila. Se decide acá y no antes
                // porque recién acá se sabe.
                if (registrarSiempre || resultado.HayCambios)
                {
                    _contexto.SincronizacionEjecucion.Add(ejecucion);
                }

                await RegistrarExitoAsync(flujo, conexion, ejecucion, reloj, resultado, hayBitacora: registrarSiempre || resultado.HayCambios);
            }

            return resultado;
        }
        catch (Exception ex)
        {
            reloj.Stop();

            if (!esSimulacion)
            {
                // El fallo se anota siempre, sin mirar `registrarSiempre`. Un flujo roto que no
                // aparece en la bitácora es indistinguible de un flujo que nadie configuró, y
                // eso es justo el fallo que hay que poder ver.
                _contexto.SincronizacionEjecucion.Add(ejecucion);
                await RegistrarFalloAsync(flujo, conexion, ejecucion, reloj, ex.Message);
            }

            throw;
        }
    }

    /// <summary>
    /// El mapeo del flujo si tiene; si no, el mapeo por identidad, que es el caso de la vista
    /// bien hecha: cuando el origen es una vista que ya entrega las columnas con los nombres que
    /// el cargador espera, no hace falta escribir un mapeo.
    /// </summary>
    private static IReadOnlyDictionary<string, string> LeerMapeo(string? mapeoJson)
    {
        if (string.IsNullOrWhiteSpace(mapeoJson))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Codigo"] = "Codigo",
                ["Nombre"] = "Nombre",
                ["Descripcion"] = "Descripcion",
                ["CategoriaCodigo"] = "CategoriaCodigo",
                ["UnidadMedidaCodigo"] = "UnidadMedidaCodigo",
                ["CostoActual"] = "CostoActual",
                ["PrecioVentaActual"] = "PrecioVentaActual",
                ["VidaUtilMeses"] = "VidaUtilMeses"
            };
        }

        using var documento = JsonDocument.Parse(mapeoJson);
        var mapeo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var propiedad in documento.RootElement.EnumerateObject())
        {
            mapeo[propiedad.Name] = propiedad.Value.GetString() ?? propiedad.Name;
        }

        return mapeo;
    }

    /// <param name="hayBitacora">
    /// Si esta corrida dejó fila en la bitácora. Cuando es <c>false</c> —corrida programada que
    /// no cambió nada— <see cref="ErpFlujo.UltimaEjecucionUtc"/> no avanza, porque ese campo
    /// responde «¿cuándo fue la última corrida que puedo abrir en el historial?», y esta no está
    /// en el historial. El latido del flujo lo lleva <see cref="ErpFlujo.UltimoIntentoUtc"/>.
    /// </param>
    private async Task RegistrarExitoAsync(
        ErpFlujo flujo,
        ErpConexion conexion,
        SincronizacionEjecucion ejecucion,
        System.Diagnostics.Stopwatch reloj,
        ResultadoCarga resultado,
        bool hayBitacora)
    {
        ejecucion.Estado = resultado.Rechazados > 0
            ? IntegracionCatalogos.EstadoSincronizacionEnCurso
            : IntegracionCatalogos.EstadoSincronizacionCorrecta;
        ejecucion.RegistrosLeidos = resultado.Total;
        ejecucion.RegistrosCreados = resultado.Insertados;
        ejecucion.RegistrosActualizados = resultado.Actualizados;
        ejecucion.RegistrosSinCambio = resultado.Omitidos;
        ejecucion.RegistrosRechazados = resultado.Rechazados;
        ejecucion.FinUtc = DateTime.UtcNow;
        ejecucion.DuracionMs = (int)Math.Min(int.MaxValue, reloj.Elapsed.TotalMilliseconds);

        if (hayBitacora)
        {
            flujo.UltimaEjecucionUtc = ejecucion.InicioUtc;
        }

        // El estado del flujo sí se actualiza aunque no haya bitácora: describe si el flujo está
        // sano, no si dejó registro. Un flujo que corre cada 10 s y va bien está bien, y decirlo es
        // más cierto que dejarlo en el estado de la última corrida que sí se anotó.
        flujo.UltimoEstado = ejecucion.Estado;
        flujo.UltimoError = null;
        flujo.FallosConsecutivos = 0;
        flujo.UltimosInsertados = resultado.Insertados;
        flujo.UltimosActualizados = resultado.Actualizados;
        flujo.UltimosOmitidos = resultado.Omitidos;
        flujo.UltimosRechazados = resultado.Rechazados;

        conexion.UltimaConexionOkUtc = ejecucion.FinUtc;
        conexion.UltimoError = null;
        conexion.Estado = IntegracionCatalogos.EstadoConexionActiva;

        await _contexto.SaveChangesAsync();
    }

    private async Task RegistrarFalloAsync(
        ErpFlujo flujo,
        ErpConexion conexion,
        SincronizacionEjecucion ejecucion,
        System.Diagnostics.Stopwatch reloj,
        string mensaje)
    {
        ejecucion.Estado = IntegracionCatalogos.EstadoSincronizacionFallida;
        ejecucion.MensajeError = mensaje.Length > 2000 ? mensaje[..2000] : mensaje;
        ejecucion.FinUtc = DateTime.UtcNow;
        ejecucion.DuracionMs = (int)Math.Min(int.MaxValue, reloj.Elapsed.TotalMilliseconds);

        flujo.UltimaEjecucionUtc = ejecucion.InicioUtc;
        flujo.UltimoEstado = ejecucion.Estado;
        flujo.UltimoError = ejecucion.MensajeError;
        flujo.FallosConsecutivos = (short)Math.Min(short.MaxValue, flujo.FallosConsecutivos + 1);
        flujo.Estado = IntegracionCatalogos.EstadoFlujoConError;

        conexion.UltimoError = ejecucion.MensajeError;
        conexion.UltimoErrorUtc = ejecucion.FinUtc;

        await _contexto.SaveChangesAsync();
    }

    public Task<List<SincronizacionEjecucion>> ListarEjecucionesAsync(
        Guid erpFlujoId,
        int top = 20,
        CancellationToken cancellationToken = default)
        => _contexto.SincronizacionEjecucion
            .AsNoTracking()
            .Where(e => e.ErpFlujoId == erpFlujoId)
            .OrderByDescending(e => e.InicioUtc)
            .Take(Math.Clamp(top, 1, 200))
            .ToListAsync(cancellationToken);
}