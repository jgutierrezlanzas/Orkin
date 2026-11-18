using System.Net.Mail;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orkin.Cotizador.Core.Application.Notificaciones;

/// <summary>
/// Proveedor real de correo saliente por SMTP, configurado íntegramente desde <c>Correo__*</c>.
/// <para><b>No hay credenciales en el código ni en appsettings:</b> el legacy nunca tuvo envío de
/// correo y no hay nada que reutilizar. Todo viene del <c>.env</c> y la API valida las opciones al
/// arrancar.</para>
/// <para>Cuando está deshabilitado devuelve un fallo con mensaje, nunca un éxito falso: la fila
/// sigue en la cola y el log dice por qué. Un correo que "parece enviado" es peor que uno fallido.</para>
/// </summary>
public sealed class ProveedorCorreoSmtp : IProveedorCorreo
{
    private readonly CorreoOpciones _opciones;
    private readonly ILogger<ProveedorCorreoSmtp> _log;

    public ProveedorCorreoSmtp(IOptions<CorreoOpciones> opciones, ILogger<ProveedorCorreoSmtp> log)
    {
        _opciones = opciones.Value;
        _log = log;
    }

    public async Task<ResultadoEnvioCorreo> EnviarAsync(
        CorreoSaliente correo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(correo);

        if (!_opciones.Habilitado)
        {
            return ResultadoEnvioCorreo.Fallido(
                "Correo saliente deshabilitado (Correo__Habilitado=false). " +
                "Configure el SMTP en el .env para habilitar el envío real.");
        }

        if (!TryCrearDireccion(_opciones.De, out var de))
        {
            return ResultadoEnvioCorreo.Fallido(
                $"Remitente Correo__De='{_opciones.De}' no es una dirección válida.");
        }

        if (!TryCrearDireccion(correo.Destinatario, out var para))
        {
            return ResultadoEnvioCorreo.Fallido(
                $"Destinatario '{correo.Destinatario}' no es una dirección válida.");
        }

        try
        {
            using var mensaje = new MailMessage(de, para)
            {
                Subject = correo.Asunto,
                Body = correo.CuerpoHtml,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            using var cliente = new SmtpClient(_opciones.Servidor, _opciones.Puerto)
            {
                EnableSsl = _opciones.UsarSsl,
                Timeout = _opciones.TimeoutSegundos * 1000,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_opciones.Usuario))
            {
                cliente.Credentials = new System.Net.NetworkCredential(_opciones.Usuario, _opciones.Clave);
            }

            await cliente.SendMailAsync(mensaje, cancellationToken);

            // SMTP no devuelve un id de mensaje; se genera uno para poder rastrear el envío en la
            // bitácora sin inventar datos del servidor.
            return new ResultadoEnvioCorreo(true, $"SMTP-{Guid.NewGuid():N}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // No se loguea la excepción completa: SmtpException puede traer la conversación SMTP,
            // y con autenticación fallida eso incluye datos del usuario. El mensaje que se guarda
            // en la fila es el que el usuario necesita ver.
            _log.LogWarning(
                "Falló el envío SMTP a {Destinatario} vía {Servidor}:{Puerto}. {Mensaje}",
                correo.Destinatario, _opciones.Servidor, _opciones.Puerto, ex.Message);

            return ResultadoEnvioCorreo.Fallido(ex.Message);
        }
    }

    private static bool TryCrearDireccion(string? valor, out MailAddress direccion)
    {
        direccion = null!;

        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        try
        {
            direccion = new MailAddress(valor);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
