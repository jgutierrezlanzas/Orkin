using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Notificaciones;

/// <summary>
/// Cola de correos de la Fase 8. Los avisos se guardan en <c>dbo.NotificacionEnviada</c> —la tabla
/// de bitácora que ya existía, extendida— y cada corrida se anota en <c>int.JobEjecucion</c>.
/// <para><b>No hay tabla paralela de correos ni de "leídos".</b> La campana lee la misma fila que el
/// proveedor; el campo <c>LeidoUtc</c> es el único estado nuevo que se necesitó.</para>
/// <para><b>Los SPs legacy solo se leen.</b> Se valida su definición y su permiso antes de invocarlos,
/// con SQL fijo y parámetros enlazados. Este servicio no inserta, actualiza ni borra en el staging
/// <c>NotificarProgramacionVisitaTemporal</c> ni en ninguna otra tabla del ERP.</para>
/// </summary>
public sealed class NotificacionService : INotificacionService
{
    private const string JobNombre = "Notificaciones";
    private const string SqlVisitas = "EXEC dbo.SINCO_NOTIFICAR_CORREOS_ENVIADOS";
    private const string SqlRequisas = "EXEC dbo.SINCO_NOTIFICAR_REQUISAS_ERRADAS";

    private readonly CotizadorDbContext _contexto;
    private readonly ICatalogoResolver _catalogos;
    private readonly IProveedorCorreo _proveedor;
    private readonly ILectorSql _lectorSql;
    private readonly ValidadorSpNotificacion _validador;
    private readonly NotificacionesOpciones _opciones;
    private readonly IContextoUsuario _contextoUsuario;
    private readonly TimeProvider _reloj;
    private readonly ILogger<NotificacionService> _log;

    public NotificacionService(
        CotizadorDbContext contexto,
        ICatalogoResolver catalogos,
        IProveedorCorreo proveedor,
        ILectorSql lectorSql,
        ValidadorSpNotificacion validador,
        IOptions<NotificacionesOpciones> opciones,
        IContextoUsuario contextoUsuario,
        TimeProvider reloj,
        ILogger<NotificacionService> log)
    {
        _contexto = contexto;
        _catalogos = catalogos;
        _proveedor = proveedor;
        _lectorSql = lectorSql;
        _validador = validador;
        _opciones = opciones.Value;
        _contextoUsuario = contextoUsuario;
        _reloj = reloj;
        _log = log;
    }

    /// <inheritdoc />
    public async Task<Guid?> EncolarAsync(
        SolicitudNotificacion solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!EsCorreoValido(solicitud.Destinatario))
        {
            throw new ArgumentException(
                $"El destinatario '{solicitud.Destinatario}' no es una dirección de correo válida.",
                nameof(solicitud));
        }

        var tipoId = await _catalogos.IdAsync<TipoNotificacionCatalogo>(solicitud.TipoCodigo, cancellationToken);
        var pendienteId = await _catalogos.IdAsync<EstadoNotificacionCatalogo>("PENDIENTE", cancellationToken);

        // Una clave repetida no es un error: es la segunda vuelta del job viendo la misma visita.
        // Si el envío falló, el reenvío se hace con ReenviarAsync, no metiendo otra fila.
        if (!string.IsNullOrWhiteSpace(solicitud.ClaveNegocio) &&
            await _contexto.NotificacionEnviada
                .AnyAsync(n => n.ClaveNegocio == solicitud.ClaveNegocio, cancellationToken))
        {
            return null;
        }

        var ahora = Ahora();
        var fila = new NotificacionEnviada
        {
            TipoId = tipoId,
            CotizacionId = solicitud.CotizacionId,
            Destinatario = solicitud.Destinatario.Trim(),
            Asunto = Recortar(solicitud.Asunto, 400),
            CuerpoHtml = solicitud.CuerpoHtml,
            EstadoId = pendienteId,
            Intentos = 0,
            ClaveNegocio = string.IsNullOrWhiteSpace(solicitud.ClaveNegocio)
                ? null
                : Recortar(solicitud.ClaveNegocio, 200),
            ProximoIntentoUtc = ahora
        };

        _contexto.NotificacionEnviada.Add(fila);
        await _contexto.SaveChangesAsync(cancellationToken);

        return fila.Id;
    }

    /// <inheritdoc />
    public async Task<ResultadoCola> RecogerLegacyAsync(CancellationToken cancellationToken = default)
    {
        var auditoria = await IniciarAsync("COLA", cancellationToken);
        var encolados = 0;

        try
        {
            if (await PuedoInvocarAsync("dbo.SINCO_NOTIFICAR_CORREOS_ENVIADOS", cancellationToken))
            {
                encolados += await RecogerVisitasAsync(cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(_opciones.CorreoInterno) &&
                await PuedoInvocarAsync("dbo.SINCO_NOTIFICAR_REQUISAS_ERRADAS", cancellationToken))
            {
                encolados += await RecogerRequisasAsync(cancellationToken);
            }
            else if (string.IsNullOrWhiteSpace(_opciones.CorreoInterno))
            {
                _log.LogDebug(
                    "Notificaciones__CorreoInterno vacío: se omite el aviso de requisas erradas.");
            }

            var resultado = new ResultadoCola(encolados, 0, 0);
            await FinalizarAsync(auditoria, resultado, cancellationToken);
            return resultado;
        }
        catch (Exception ex)
        {
            await FallirAsync(auditoria, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ResultadoCola> ProcesarColaAsync(CancellationToken cancellationToken = default)
    {
        var auditoria = await IniciarAsync("ENVIO", cancellationToken);

        try
        {
            var ahora = Ahora();
            var pendienteId = await _catalogos.IdAsync<EstadoNotificacionCatalogo>("PENDIENTE", cancellationToken);
            var enviadaId = await _catalogos.IdAsync<EstadoNotificacionCatalogo>("ENVIADA", cancellationToken);
            var fallidaId = await _catalogos.IdAsync<EstadoNotificacionCatalogo>("FALLIDA", cancellationToken);

            var vencidas = await _contexto.NotificacionEnviada
                .Where(n => n.EstadoId == pendienteId &&
                            (n.ProximoIntentoUtc == null || n.ProximoIntentoUtc <= ahora))
                .OrderBy(n => n.ProximoIntentoUtc)
                .Take(Math.Max(1, _opciones.TamanoLote))
                .ToListAsync(cancellationToken);

            var enviados = 0;
            var fallidos = 0;

            foreach (var fila in vencidas)
            {
                cancellationToken.ThrowIfCancellationRequested();

                fila.Intentos++;
                var resultado = await _proveedor.EnviarAsync(
                    new CorreoSaliente(fila.Destinatario, fila.Asunto ?? string.Empty, fila.CuerpoHtml ?? string.Empty),
                    cancellationToken);

                if (resultado.Exito)
                {
                    fila.EstadoId = enviadaId;
                    fila.EnviadoUtc = Ahora();
                    fila.IdExterno = resultado.IdExterno;
                    fila.Error = null;
                    fila.ProximoIntentoUtc = null;
                    enviados++;
                    continue;
                }

                fallidos++;
                fila.Error = Recortar(resultado.Error, 2000);

                if (fila.Intentos >= Math.Max(1, _opciones.MaximoIntentos))
                {
                    fila.EstadoId = fallidaId;
                    fila.ProximoIntentoUtc = null;
                }
                else
                {
                    fila.ProximoIntentoUtc = Ahora() + PlanificadorReintentos.Siguiente(
                        fila.Intentos, _opciones.BackoffBaseSegundos);
                }
            }

            if (vencidas.Count > 0)
            {
                await _contexto.SaveChangesAsync(cancellationToken);
            }

            var resultadoVuelta = new ResultadoCola(0, enviados, fallidos);
            await FinalizarAsync(auditoria, resultadoVuelta, cancellationToken);
            return resultadoVuelta;
        }
        catch (Exception ex)
        {
            await FallirAsync(auditoria, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<BandejaNotificaciones> ObtenerBandejaAsync(CancellationToken cancellationToken = default)
    {
        var enviadaId = await _catalogos.IdAsync<EstadoNotificacionCatalogo>("ENVIADA", cancellationToken);
        var tipos = await _catalogos.ListarAsync<TipoNotificacionCatalogo>(false, cancellationToken);
        var estados = await _catalogos.ListarAsync<EstadoNotificacionCatalogo>(false, cancellationToken);
        var porTipo = tipos.ToDictionary(t => t.Id, t => t.Codigo);
        var porEstado = estados.ToDictionary(e => e.Id, e => e.Codigo);

        var noLeidas = await _contexto.NotificacionEnviada
            .CountAsync(n => n.EstadoId == enviadaId && n.LeidoUtc == null, cancellationToken);

        var filas = await _contexto.NotificacionEnviada
            .Where(n => n.EstadoId == enviadaId)
            .OrderByDescending(n => n.EnviadoUtc)
            .ThenByDescending(n => n.CreatedAt)
            .Take(Math.Max(1, _opciones.TamanoBandeja))
            .ToListAsync(cancellationToken);

        var recientes = filas.Select(n => new NotificacionResumen(
            n.Id,
            porTipo.GetValueOrDefault(n.TipoId, string.Empty),
            tipos.FirstOrDefault(t => t.Id == n.TipoId)?.Nombre ?? string.Empty,
            n.Asunto ?? string.Empty,
            n.Destinatario,
            porEstado.GetValueOrDefault(n.EstadoId, string.Empty),
            n.EnviadoUtc,
            n.LeidoUtc,
            n.Intentos,
            n.Error)).ToArray();

        return new BandejaNotificaciones(noLeidas, recientes);
    }

    /// <inheritdoc />
    public async Task<bool> MarcarLeidaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fila = await _contexto.NotificacionEnviada
            .FirstOrDefaultAsync(n => n.Id == id && n.LeidoUtc == null, cancellationToken);

        if (fila is null)
        {
            return false;
        }

        fila.LeidoUtc = Ahora();
        await _contexto.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> MarcarTodasLeidasAsync(CancellationToken cancellationToken = default)
    {
        var enviadaId = await _catalogos.IdAsync<EstadoNotificacionCatalogo>("ENVIADA", cancellationToken);
        var ahora = Ahora();

        return await _contexto.NotificacionEnviada
            .Where(n => n.EstadoId == enviadaId && n.LeidoUtc == null)
            .ExecuteUpdateAsync(
                establecer => establecer.SetProperty(n => n.LeidoUtc, ahora),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ReenviarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var auditoria = await IniciarAsync("REENVIOS", cancellationToken);

        try
        {
            var fila = await _contexto.NotificacionEnviada
                .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

            if (fila is null)
            {
                await FinalizarAsync(auditoria, new ResultadoCola(0, 0, 0), cancellationToken);
                return false;
            }

            var pendienteId = await _catalogos.IdAsync<EstadoNotificacionCatalogo>("PENDIENTE", cancellationToken);

            fila.EstadoId = pendienteId;
            fila.Intentos = 0;
            fila.Error = null;
            fila.IdExterno = null;
            fila.EnviadoUtc = null;
            fila.LeidoUtc = null;
            fila.ProximoIntentoUtc = Ahora();

            await FinalizarAsync(auditoria, new ResultadoCola(1, 0, 0), cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            await FallirAsync(auditoria, ex);
            throw;
        }
    }

    // ── SPs legacy: solo lectura ─────────────────────────────────────────────────────

    private async Task<bool> PuedoInvocarAsync(string nombre, CancellationToken cancellationToken)
    {
        if (!_opciones.ValidarDefinicionesSp)
        {
            _log.LogWarning(
                "Se invoca {SP} sin validar su definición porque Notificaciones__ValidarDefinicionesSp=false.",
                nombre);
            return true;
        }

        var validacion = await _validador.ValidarAsync(nombre, cancellationToken);

        if (!validacion.Valido)
        {
            _log.LogWarning("{Motivo}", validacion.Motivo);
        }

        return validacion.Valido;
    }

    private async Task<int> RecogerVisitasAsync(CancellationToken cancellationToken)
    {
        var filas = await _lectorSql.ConsultarAsync(SqlVisitas, cancellationToken);
        var encolados = 0;

        foreach (var fila in filas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cliente = Texto(fila, "Cliente");
            var idVisita = Texto(fila, "ID_VISITA");
            var idCalendario = Texto(fila, "ID_CALENDARIO");
            var nombreCliente = Texto(fila, "NOMBRE_CLIENTE");
            var locacion = Texto(fila, "LOCACION_NOMBRE");
            if (string.IsNullOrWhiteSpace(locacion)) locacion = Texto(fila, "LOCACION");
            var servicio = Texto(fila, "DESCRIPCION");
            if (string.IsNullOrWhiteSpace(servicio)) servicio = Texto(fila, "TIPO_SERVICIO");
            var fechaInicio = Texto(fila, "FECHA_INICIO");
            var horaInicio = Texto(fila, "HORA_INICIO");
            var fechaFinal = Texto(fila, "FECHA_FINAL");
            var horaFinal = Texto(fila, "HORA_FINAL");
            var tecnico = Texto(fila, "TECNICO");

            var contactos = new[]
            {
                (Correo: Texto(fila, "CORREO"), Contacto: Texto(fila, "CONTACTO")),
                (Correo: Texto(fila, "CORREO2"), Contacto: Texto(fila, "CONTACTO2")),
                (Correo: Texto(fila, "CORREO3"), Contacto: Texto(fila, "CONTACTO3"))
            };

            foreach (var contacto in contactos)
            {
                if (!EsCorreoValido(contacto.Correo))
                {
                    continue;
                }

                var cuerpoContacto = PlantillasCorreo.Visita(
                    cliente, nombreCliente, locacion, servicio,
                    fechaInicio, horaInicio, fechaFinal, horaFinal, tecnico, contacto.Contacto);

                var id = await EncolarAsync(new SolicitudNotificacion(
                    TipoCodigo: "VISITA_PROXIMA",
                    Destinatario: contacto.Correo,
                    Asunto: $"Programación de visita {idVisita} — {nombreCliente}".Trim(),
                    CuerpoHtml: cuerpoContacto,
                    ClaveNegocio: $"VISITA|{idVisita}|{idCalendario}|{contacto.Correo}".Trim('|')));

                if (id.HasValue)
                {
                    encolados++;
                }
            }
        }

        return encolados;
    }

    private async Task<int> RecogerRequisasAsync(CancellationToken cancellationToken)
    {
        var filas = await _lectorSql.ConsultarAsync(SqlRequisas, cancellationToken);
        var encolados = 0;

        var grupos = filas
            .GroupBy(f => Texto(f, "REQUISA_NUMERO"))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key));

        foreach (var grupo in grupos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lineas = grupo.Select(f =>
                $"{Texto(f, "CODIGO_ARTICULO")} — {Texto(f, "DESCRIP_ARTICULO")}: " +
                $"costo {Texto(f, "COSTO_UNITARIO")}, centro {Texto(f, "CENTRO_COSTO")}").ToArray();

            var id = await EncolarAsync(new SolicitudNotificacion(
                TipoCodigo: "REQUISA_ERRADA",
                Destinatario: _opciones.CorreoInterno,
                Asunto: $"Requisa {grupo.Key} con líneas erradas",
                CuerpoHtml: PlantillasCorreo.RequisasErradas(grupo.Key, lineas),
                ClaveNegocio: $"REQUISA|{grupo.Key}"));

            if (id.HasValue)
            {
                encolados++;
            }
        }

        return encolados;
    }

    // ── Auditoría y utilidades ───────────────────────────────────────────────────────

    private async Task<JobEjecucion> IniciarAsync(string operacion, CancellationToken cancellationToken)
    {
        var auditoria = new JobEjecucion
        {
            Nombre = JobNombre,
            Operacion = operacion,
            IniciadoUtc = Ahora(),
            Estado = "EN_CURSO",
            UsuarioId = _contextoUsuario.UsuarioId,
            Usuario = string.IsNullOrWhiteSpace(_contextoUsuario.NombreUsuario)
                ? null
                : Recortar(_contextoUsuario.NombreUsuario, 200)
        };

        _contexto.JobEjecucion.Add(auditoria);
        await _contexto.SaveChangesAsync(cancellationToken);
        return auditoria;
    }

    private async Task FinalizarAsync(
        JobEjecucion auditoria,
        ResultadoCola resultado,
        CancellationToken cancellationToken)
    {
        auditoria.FinalizadoUtc = Ahora();
        auditoria.Estado = "OK";
        auditoria.Encolados = resultado.Encolados;
        auditoria.Enviados = resultado.Enviados;
        auditoria.Fallidos = resultado.Fallidos;
        auditoria.Mensaje = Recortar(resultado.Mensaje, 2000);

        await _contexto.SaveChangesAsync(cancellationToken);
    }

    private async Task FallirAsync(JobEjecucion auditoria, Exception ex)
    {
        auditoria.FinalizadoUtc = Ahora();
        auditoria.Estado = "FALLIDO";
        auditoria.Mensaje = Recortar(ex.Message, 2000);

        try
        {
            // CancellationToken.None a propósito: si el apagado canceló la corrida, todavía queremos
            // que quede escrito por qué falló.
            await _contexto.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception guardarEx)
        {
            _log.LogError(guardarEx, "No se pudo escribir la auditoría del job de notificaciones.");
        }
    }

    private DateTime Ahora() => _reloj.GetUtcNow().UtcDateTime;

    private static string Recortar(string? valor, int maximo) =>
        string.IsNullOrEmpty(valor) ? string.Empty : (valor.Length <= maximo ? valor : valor[..maximo]);

    private static string Texto(IReadOnlyDictionary<string, object?> fila, string clave)
    {
        if (!fila.TryGetValue(clave, out var valor))
        {
            var equivalente = fila.FirstOrDefault(p =>
                string.Equals(p.Key, clave, StringComparison.OrdinalIgnoreCase));
            valor = equivalente.Value;
        }

        return valor is null ? string.Empty : Convert.ToString(valor)?.Trim() ?? string.Empty;
    }

    /// <summary>Validación mínima del sobre; el proveedor vuelve a validar con <c>MailAddress</c>.</summary>
    internal static bool EsCorreoValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var correo = valor.Trim();
        var arroba = correo.IndexOf('@');

        return arroba > 0 &&
               arroba < correo.Length - 1 &&
               correo.IndexOf('@', arroba + 1) < 0 &&
               !correo.Any(char.IsWhiteSpace) &&
               correo.IndexOf('.', arroba + 1) >= 0;
    }
}
