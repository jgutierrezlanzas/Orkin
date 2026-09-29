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
            ModoExtraccion = IntegracionCatalogos.ModoExtraccionCompleta,
            Estado = IntegracionCatalogos.EstadoFlujoActivo
        };

        _contexto.ErpFlujo.Add(entidad);
        await _contexto.SaveChangesAsync(cancellationToken);
        return entidad;
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
    /// <para>Cada corrida deja fila en la bitácora <c>int.SincronizacionEjecucion</c>, exista o no
    /// el éxito: una sincronización que falló y no se registró es indistinguishable de una que
    /// nunca se disparó.</para>
    /// </summary>
    public async Task<ResultadoCarga> EjecutarFlujoAsync(
        Guid erpFlujoId,
        CancellationToken cancellationToken = default)
        => await CorrerFlujoAsync(erpFlujoId, esSimulacion: false, cancellationToken);

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
        => await CorrerFlujoAsync(erpFlujoId, esSimulacion: true, cancellationToken);

    private async Task<ResultadoCarga> CorrerFlujoAsync(
        Guid erpFlujoId,
        bool esSimulacion,
        CancellationToken cancellationToken)
    {
        var flujo = await ObtenerFlujoAsync(erpFlujoId, cancellationToken)
            ?? throw new KeyNotFoundException("El flujo no existe.");

        var conexion = await ObtenerConexionAsync(flujo.ErpConexionId, cancellationToken)
            ?? throw new KeyNotFoundException("La conexión del flujo no existe.");

        var inicio = DateTime.UtcNow;
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        var ejecucion = new SincronizacionEjecucion
        {
            ErpConexionId = conexion.Id,
            ErpFlujoId = flujo.Id,
            TipoDisparador = flujo.TipoDisparador,
            Estado = IntegracionCatalogos.EstadoSincronizacionEnCurso,
            InicioUtc = inicio,
            SolicitadoPor = _usuario.UsuarioId
        };

        // En simulación no se crea la fila de bitácora, y con ella no se toca el estado del flujo
        // ni el de la conexión. Ver la nota de SimularFlujoAsync: la bitácora tiene que poder
        // leerse como "esto es lo que le pasó al catálogo", sin filas de mentira en el medio.
        if (!esSimulacion)
        {
            _contexto.SincronizacionEjecucion.Add(ejecucion);
        }

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
                await RegistrarExitoAsync(flujo, conexion, ejecucion, reloj, resultado);
            }

            return resultado;
        }
        catch (Exception ex)
        {
            reloj.Stop();

            if (!esSimulacion)
            {
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

    private async Task RegistrarExitoAsync(
        ErpFlujo flujo,
        ErpConexion conexion,
        SincronizacionEjecucion ejecucion,
        System.Diagnostics.Stopwatch reloj,
        ResultadoCarga resultado)
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

        flujo.UltimaEjecucionUtc = ejecucion.InicioUtc;
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