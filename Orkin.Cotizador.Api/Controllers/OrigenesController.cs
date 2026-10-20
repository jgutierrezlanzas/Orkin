using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Application.Integracion;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Carga;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Orígenes de datos externos (Fase 12): las conexiones al ERP de los clientes y los flujos que
/// llenan nuestros catálogos desde ellas.
/// <para>Todo el grupo exige <c>CATALOGOS.ORIGENES</c>, un permiso aparte de
/// <c>CATALOGOS.EDITAR</c> a propósito: poder corregir un precio de un producto no es lo mismo
/// que poder abrir una conexión a la base de datos de un cliente. El segundo puede leer catálogos
/// completos del ERP y cambiar de dónde viene el dato; el primero no.</para>
/// <para>⚠️ La base del ERP del cliente se lee, nunca se escribe (D-19). La columna
/// <c>EsSoloLectura</c> tiene un <c>CHECK (= 1)</c> en la base.</para>
/// </summary>
[ApiController]
[Route("api/origenes")]
[Produces("application/json")]
[Authorize("CATALOGOS.ORIGENES")]
public sealed class OrigenesController : ControllerBase
{
    private readonly IntegracionService _servicio;
    private readonly CotizadorDbContext _contexto;
    private readonly ProcesoCargaService _proceso;

    public OrigenesController(IntegracionService servicio, CotizadorDbContext contexto, ProcesoCargaService proceso)
    {
        _servicio = servicio;
        _contexto = contexto;
        _proceso = proceso;
    }

    // ── Conexiones ────────────────────────────────────────────────────────────

    [HttpGet("conexiones")]
    public async Task<IResult> ListarConexiones(CancellationToken cancellationToken)
    {
        var conexiones = await _servicio.ListarConexionesAsync(cancellationToken);

        var flujosPorConexion = await _contexto.ErpFlujo
            .AsNoTracking()
            .GroupBy(f => f.ErpConexionId)
            .Select(g => new { ConexionId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.ConexionId, x => x.Total, cancellationToken);

        var dtos = conexiones
            .Select(c => AResponse(c, flujosPorConexion.GetValueOrDefault(c.Id)))
            .ToList();

        return ApiResponse<IReadOnlyList<ErpConexionResponse>>.Ok(dtos).AHttp();
    }

    [HttpPost("conexiones")]
    public async Task<IResult> CrearConexion(
        [FromBody] CrearErpConexionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.CrearConexionAsync(
                request.Nombre,
                request.Descripcion,
                request.Servidor,
                request.Puerto,
                request.NombreBaseDatos,
                request.Esquema,
                request.Usuario,
                request.Contrasena,
                request.TiempoEsperaSegundos ?? 30,
                cancellationToken);

            return ApiResponse<ErpConexionResponse>.Ok(AResponse(entidad, 0)).AHttp();
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ErpConexionResponse>.Error("VALIDACION", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ErpConexionResponse>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    [HttpPut("conexiones/{id:guid}")]
    public async Task<IResult> ActualizarConexion(
        Guid id,
        [FromBody] ActualizarErpConexionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.ActualizarConexionAsync(
                id,
                request.Descripcion,
                request.Servidor,
                request.Puerto,
                request.NombreBaseDatos,
                request.Esquema,
                request.Usuario,
                request.Contrasena,
                request.TiempoEsperaSegundos,
                cancellationToken);

            var flujos = await _contexto.ErpFlujo.CountAsync(f => f.ErpConexionId == id, cancellationToken);

            return ApiResponse<ErpConexionResponse>.Ok(AResponse(entidad, flujos)).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ErpConexionResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ErpConexionResponse>.Error("VALIDACION", ex.Message).AHttp();
        }
    }

    /// <summary>
    /// Abre la conexión y ejecuta un <c>SELECT 1</c>. Es lo que se corre antes de crear un flujo:
    /// descubrir que la contraseña está mal con la prueba es mejor que descubrirlo con una
    /// sincronización que no cargó nada.
    /// </summary>
    [HttpPost("conexiones/{id:guid}/probar")]
    public async Task<IResult> ProbarConexion(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _servicio.ProbarConexionAsync(id, cancellationToken);

            if (!resultado.Exito)
            {
                // La prueba fallida es un 400 y no un 200 con el fallo adentro: quien llama tiene
                // que poder distinguir "probé y falló" de "probé y funcionó" por el código, y no
                // leyendo un campo booleano que puede venir vacío.
                return ApiResponse<ProbarConexionResponse>
                    .Error("DATO_INVALIDO", resultado.Mensaje)
                    .AHttp();
            }

            var respuesta = new ProbarConexionResponse(
                resultado.Exito,
                resultado.Mensaje,
                resultado.VersionServidor,
                resultado.Milisegundos);

            return ApiResponse<ProbarConexionResponse>.Ok(respuesta).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ProbarConexionResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
    }

    // ── Flujos ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los catálogos que hoy tienen cargador registrado. Sale del DI, no de una constante: la
    /// pantalla de flujos ofrece exactamente lo que el motor puede correr, y agregar un cargador
    /// lo hace aparecer solo.
    /// </summary>
    [HttpGet("catalogos")]
    public IResult CatalogosDisponibles()
        => ApiResponse<IReadOnlyList<string>>.Ok(_proceso.CatalogosDisponibles.ToList()).AHttp();

    /// <summary>
    /// Los campos que un catálogo espera del origen — para la tabla de homologación.
    /// <para>Es lo que reemplaza el JSON crudo: quien configura un flujo ve QUÉ datos se
    /// necesitan, de QUÉ tipo, y cuáles puede dejar vacíos. Su vista del ERP solo tiene que
    /// traer columnas con esos nombres, o mapearlas si ya existen con otros.</para>
    /// </summary>
    [HttpGet("catalogos/{catalogo}/campos")]
    public IResult CamposEsperados(string catalogo)
    {
        var cargador = _proceso.ResolverCargador(catalogo);

        if (cargador is null)
        {
            // NO_ENCONTRADO y no un 500: acá el catálogo es el cliente quien lo nombra, y la
            // respuesta le dice cuáles sí existen. Es un 404 con la lista, no un fallo del
            // servidor.
            return ApiResponse<object>.Error("NO_ENCONTRADO",
                $"El catálogo '{catalogo}' no tiene cargador. Los disponibles son: {string.Join(", ", _proceso.CatalogosDisponibles)}.").AHttp();
        }

        return ApiResponse<IReadOnlyList<CampoEsperadoResponse>>.Ok(
            cargador.CamposEsperados
                .Select(c => new CampoEsperadoResponse(c.Nombre, c.Tipo, c.EsObligatorio, c.Descripcion))
                .ToList()).AHttp();
    }

    /// <summary>
    /// Las columnas que trae la vista del ERP — la otra mitad de la homologación.
    /// <para>Con los dos lados a la vista, configurar un flujo deja de ser adivinar: la pantalla
    /// muestra qué campos necesita la plataforma, cuáles trae la vista y cuáles faltan. Es un
    /// <c>POST</c> y no un <c>GET</c> porque abre una conexión al ERP del cliente —algo con
    /// efecto, aunque no escriba— y mandarlo en la URL acabaría con la cadena de conexión y el
    /// nombre de la vista en el log de cualquier proxy.</para>
    /// </summary>
    [HttpPost("flujos/columnas")]
    public async Task<IResult> ColumnasDelOrigen(
        [FromBody] LeerColumnasRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var columnas = await _servicio.LeerColumnasOrigenAsync(
                request.ErpConexionId, request.Origen, cancellationToken);

            return ApiResponse<IReadOnlyList<ColumnaOrigenResponse>>.Ok(
                columnas
                    .Select(c => new ColumnaOrigenResponse(c.Nombre, c.Tipo, c.PermiteNulos, c.Longitud))
                    .ToList()).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<IReadOnlyList<ColumnaOrigenResponse>>
                .Error("VALIDACION", ex.Message).AHttp();
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            // VALIDACION y no un error de servidor, porque el que lo mandó lo puede arreglar:
            // casi siempre es un nombre de vista equivocado. Y el mensaje del SQL Server se pasa
            // tal cual a propósito —«invalid object name» le dice a un administrador qué
            // escribir, y un genérico «no se pudo leer» lo deja probando nombres a ciegas.
            return ApiResponse<IReadOnlyList<ColumnaOrigenResponse>>
                .Error("VALIDACION", $"No se pudo leer el origen: {ex.Message}").AHttp();
        }
    }

    [HttpGet("flujos")]
    public async Task<IResult> ListarFlujos(CancellationToken cancellationToken)
    {
        var flujos = await _servicio.ListarFlujosAsync(cancellationToken);
        var nombres = await _contexto.ErpConexion
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Nombre, cancellationToken);

        var dtos = flujos
            .Select(f => AResponse(f, nombres.GetValueOrDefault(f.ErpConexionId, "(conexión eliminada)")))
            .ToList();

        return ApiResponse<IReadOnlyList<ErpFlujoResponse>>.Ok(dtos).AHttp();
    }

    [HttpPost("flujos")]
    public async Task<IResult> CrearFlujo(
        [FromBody] CrearErpFlujoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.CrearFlujoAsync(
                request.ErpConexionId,
                request.Nombre,
                request.Descripcion,
                request.Catalogo,
                request.Origen,
                request.MapeoJson,
                cancellationToken);

            var nombreConexion = await _contexto.ErpConexion
                .Where(c => c.Id == entidad.ErpConexionId)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? "";

            return ApiResponse<ErpFlujoResponse>.Ok(AResponse(entidad, nombreConexion)).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("VALIDACION", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    [HttpPut("flujos/{id:guid}")]
    public async Task<IResult> ActualizarFlujo(
        Guid id,
        [FromBody] ActualizarErpFlujoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.ActualizarFlujoAsync(
                id,
                request.Descripcion,
                request.Catalogo,
                request.Origen,
                request.MapeoJson,
                cancellationToken);

            var nombreConexion = await _contexto.ErpConexion
                .Where(c => c.Id == entidad.ErpConexionId)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? "";

            return ApiResponse<ErpFlujoResponse>.Ok(AResponse(entidad, nombreConexion)).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("VALIDACION", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    [HttpDelete("flujos/{id:guid}")]
    public async Task<IResult> DarDeBajaFlujo(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _servicio.DarDeBajaFlujoAsync(id, cancellationToken);
            return ApiResponse<object>.Ok(new { }).AHttpSinCuerpo();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<object>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
    }

    /// <summary>
    /// Corre el flujo ahora: lee del origen configurado y llena el catálogo.
    /// <para>Devuelve el resultado de la carga (insertados, actualizados, omitidos, rechazados).
    /// Lo que se rechaza no es un fallo de la carga: son filas que no se pudieron transformar, y
    /// la respuesta las trae con su motivo para que seLean.</para>
    /// </summary>
    [HttpPost("flujos/{id:guid}/ejecutar")]
    public async Task<IResult> EjecutarFlujo(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _servicio.EjecutarFlujoAsync(id, cancellationToken);
            return ApiResponse<ResultadoCarga>.Ok(resultado).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ResultadoCarga>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            // Incluye "no hay cargador para este catálogo": es un problema de configuración del
            // flujo, no del servidor, y la respuesta lo dice.
            return ApiResponse<ResultadoCarga>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    /// <summary>
    /// Corre el flujo <b>sin escribir nada</b> y devuelve lo que haría: cuántos registros
    /// crearía, cuántos cambiarían —con el valor anterior y el nuevo de cada campo— y cuáles
    /// no se podrían transformar.
    /// <para>Existe porque la forma de romper un catálogo no es que la carga falle: es que
    /// funcione y traiga el dato equivocado. Una vista mal armada, una columna corrida un
    /// lugar, un precio en otra moneda — nada de eso da error, dan datos buenos con la
    /// etiqueta de la fuente equivocada. Con la simulación se ve antes de que entren.</para>
    /// <para>No deja rastro en la bitácora ni toca el estado del flujo: una simulación no es
    /// una corrida, y anotarla ahí haría que el historial dijera que se modificó el catálogo
    /// cuando no se modificó nada.</para>
    /// </summary>
    [HttpPost("flujos/{id:guid}/simular")]
    public async Task<IResult> SimularFlujo(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _servicio.SimularFlujoAsync(id, cancellationToken);
            return ApiResponse<SimulacionFlujoResponse>.Ok(AResponse(resultado)).AHttp();
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<SimulacionFlujoResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<SimulacionFlujoResponse>.Error("DATO_INVALIDO", ex.Message).AHttp();
        }
    }

    /// <summary>Historial de corridas del flujo, de la más reciente a la más vieja.</summary>
    [HttpGet("flujos/{id:guid}/ejecuciones")]
    public async Task<IResult> ListarEjecuciones(
        Guid id,
        [FromQuery] int top = 20,
        CancellationToken cancellationToken = default)
    {
        var ejecuciones = await _servicio.ListarEjecucionesAsync(id, top, cancellationToken);
        var dtos = ejecuciones.Select(AResponse).ToList();

        return ApiResponse<IReadOnlyList<SincronizacionEjecucionResponse>>.Ok(dtos).AHttp();
    }

    // ── Programación (12.11) ──────────────────────────────────────────────────

    /// <summary>
    /// Programa un flujo o lo devuelve a manual. La primera corrida sale inmediata, así que la
    /// pantalla puede refrescar a los pocos segundos y ver que el automático funciona, en vez de
    /// tener que esperar el ciclo entero para sospechar que no arrancó.
    /// </summary>
    [HttpPut("flujos/{id:guid}/programacion")]
    public async Task<IResult> ConfigurarProgramacion(
        Guid id,
        [FromBody] ConfigurarProgramacionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.ConfigurarProgramacionAsync(
                id,
                request.TipoDisparador,
                request.IntervaloSegundos,
                request.MaximoRegistrosPorCorrida,
                request.MaximoFallosConsecutivos,
                cancellationToken);

            return await ResponderFlujoAsync(entidad, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
        catch (ArgumentException ex)
        {
            // El intervalo por debajo del piso, el tipo de disparador desconocido y el tope de
            // registros inválido son todos problemas de lo que se mandó, no del servidor.
            return ApiResponse<ErpFlujoResponse>.Error("VALIDACION", ex.Message).AHttp();
        }
    }

    /// <summary>
    /// Pausa un flujo. El motor deja de tocarlo y queda escrito por qué, que es lo que hace falta
    /// para que alguien pueda entender más tarde por qué un flujo lleva un día sin moverse.
    /// </summary>
    [HttpPost("flujos/{id:guid}/pausar")]
    public async Task<IResult> PausarFlujo(
        Guid id,
        [FromBody] PausarFlujoRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entidad = await _servicio.PausarFlujoAsync(id, request?.Motivo, cancellationToken);
            return await ResponderFlujoAsync(entidad, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
    }

    /// <summary>
    /// Reanuda un flujo pausado, lo pausara una persona o el corte de fallos. También pone el
    /// contador de fallos en cero: si no, el motor lo volvería a pausar en la primera vuelta sin
    /// haber intentado una sola vez.
    /// </summary>
    [HttpPost("flujos/{id:guid}/reanudar")]
    public async Task<IResult> ReanudarFlujo(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var entidad = await _servicio.ReanudarFlujoAsync(id, cancellationToken);
            return await ResponderFlujoAsync(entidad, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return ApiResponse<ErpFlujoResponse>.Error("NO_ENCONTRADO", ex.Message).AHttp();
        }
    }

    // ── Mapeo de entidades a DTOs ─────────────────────────────────────────────

    /// <summary>
    /// Arma la respuesta de un flujo recién tocado, con el nombre de su conexión resuelto. Vive
    /// aparte porque los tres endpoints de programación la necesitan igual y el nombre se busca
    /// siempre con la misma consulta.
    /// </summary>
    private async Task<IResult> ResponderFlujoAsync(ErpFlujo flujo, CancellationToken cancellationToken)
    {
        var nombreConexion = await _contexto.ErpConexion
            .Where(c => c.Id == flujo.ErpConexionId)
            .Select(c => c.Nombre)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        return ApiResponse<ErpFlujoResponse>.Ok(AResponse(flujo, nombreConexion)).AHttp();
    }

    private static ErpConexionResponse AResponse(ErpConexion c, int totalFlujos) => new(
        c.Id,
        c.Nombre,
        c.Descripcion,
        c.Servidor,
        c.Puerto,
        c.NombreBaseDatos,
        c.Esquema,
        c.Usuario,
        c.EsSoloLectura,
        c.TiempoEsperaSegundos,
        c.ContrasenaCifrada is { Length: > 0 },
        c.UltimaConexionOkUtc is null ? null : true,
        c.UltimaConexionOkUtc,
        c.UltimoError,
        c.UltimoErrorUtc,
        c.IsActive,
        totalFlujos);

    private static ErpFlujoResponse AResponse(ErpFlujo f, string conexionNombre) => new(
        f.Id,
        f.ErpConexionId,
        conexionNombre,
        f.Nombre,
        f.Descripcion,
        f.Catalogo,
        f.Origen,
        f.MapeoJson,
        f.IsActive,
        f.UltimaEjecucionUtc,
        f.UltimosInsertados,
        f.UltimosActualizados,
        f.UltimosOmitidos,
        f.UltimosRechazados,
        f.UltimoError,
        f.TipoDisparador switch
        {
            IntegracionCatalogos.TipoDisparadorManual => "MANUAL",
            IntegracionCatalogos.TipoDisparadorProgramado => "PROGRAMADO",
            IntegracionCatalogos.TipoDisparadorEvento => "EVENTO",
            _ => "MANUAL"
        },
        f.IntervaloSegundos,
        f.DesfaseSegundos,
        f.MaximoRegistrosPorCorrida,
        f.MaximoFallosConsecutivos,
        f.ProximaEjecucionUtc,
        f.UltimoIntentoUtc,
        f.EsPausado,
        f.MotivoPausa,
        f.FallosConsecutivos,
        f.CorridaEnCursoUtc);

    private static SincronizacionEjecucionResponse AResponse(SincronizacionEjecucion e) => new(
        e.Id,
        e.InicioUtc,
        e.FinUtc,
        e.DuracionMs,
        e.Estado switch
        {
            IntegracionCatalogos.EstadoSincronizacionEnCurso => "EN_CURSO",
            IntegracionCatalogos.EstadoSincronizacionCorrecta => "CORRECTA",
            IntegracionCatalogos.EstadoSincronizacionFallida => "FALLIDA",
            _ => "DESCONOCIDO"
        },
        e.RegistrosLeidos,
        e.RegistrosCreados,
        e.RegistrosActualizados,
        e.RegistrosSinCambio,
        e.RegistrosRechazados,
        e.MensajeError);

    /// <summary>
    /// Traduce el resultado de la simulación al formato de transporte. El enum se pasa a texto
    /// con <c>ToString()</c> a propósito: el nombre del miembro ya es exactamente la palabra que
    /// quiere leer la persona que está mirando la pantalla ("Actualizar", "Adoptar"), así que
    /// no hace falta una tabla de traducción que se pueda quedar vieja.
    /// </summary>
    private static SimulacionFlujoResponse AResponse(ResultadoCarga r) => new(
        r.Catalogo,
        r.Total,
        r.Insertados,
        r.Actualizados,
        r.Omitidos,
        r.Rechazados,

        // Solo cuentan como "cambio" las filas que hay que escribir. Las omitidas no lo son, y
        // las de acción Adoptar sí: la fila existe pero pasa a ser de otra fuente, lo cual es
        // un cambio aunque ningún campo de negocio cambie.
        r.Cambios.Any(c => c.Accion is AccionSimulada.Actualizar or AccionSimulada.Insertar or AccionSimulada.Adoptar),
        r.Cambios.Select(c => new CambioSimuladoResponse(
            c.NumeroFila,
            c.CodigoExterno,
            c.Accion.ToString(),
            c.Campos.Select(f => new CampoSimuladoResponse(f.Campo, f.Anterior, f.Nuevo)).ToList())).ToList(),
        r.Rechazos.Select(x => new RechazoCargaResponse(x.NumeroFila, x.CodigoExterno, x.Motivo, x.FilaJson)).ToList(),
        r.EjecutadoUtc);
}
