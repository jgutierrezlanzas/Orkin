using Microsoft.Extensions.Options;

namespace Orkin.Cotizador.Core.Application.Notificaciones;

/// <summary>
/// Validación de la configuración del SMTP, ejecutada al arrancar por la API.
/// <para><b>Qué se valida, y qué no:</b> se exigen servidor y remitente solo cuando el envío está
/// habilitado, y usuario y clave tienen que ir juntos o ninguno de los dos. Un servidor sin
/// autenticación es una configuración válida; exigir clave siempre dejaría fuera a ese caso
/// legítimo. La clave nunca se comprueba contra el servidor al arrancar: eso sería un envío de
/// prueba con credenciales reales, y no es cosa de un inicio de aplicación.</para>
/// <para>Está como clase y no como lambda en el <c>Program.cs</c> para que la regla se pueda probar
/// sin levantar un host: una opción mal validada se descubre al primer correo, no en el arranque.</para>
/// </summary>
public sealed class CorreoOpcionesValidador : IValidateOptions<CorreoOpciones>
{
    public ValidateOptionsResult Validate(string? name, CorreoOpciones opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        if (opciones.Puerto is < 1 or > 65535)
        {
            return Fallo($"{CorreoOpciones.Seccion}:Puerto debe estar entre 1 y 65535.");
        }

        if (opciones.TimeoutSegundos is < 1 or > 300)
        {
            return Fallo($"{CorreoOpciones.Seccion}:TimeoutSegundos debe estar entre 1 y 300.");
        }

        if (opciones.Habilitado && string.IsNullOrWhiteSpace(opciones.Servidor))
        {
            return Fallo($"Falta {CorreoOpciones.Seccion}:Servidor; el envío está habilitado.");
        }

        if (opciones.Habilitado && string.IsNullOrWhiteSpace(opciones.De))
        {
            return Fallo($"Falta {CorreoOpciones.Seccion}:De; el envío está habilitado.");
        }

        if (opciones.Habilitado &&
            (!opciones.De.Contains('@') || opciones.De.Any(char.IsWhiteSpace)))
        {
            return Fallo($"{CorreoOpciones.Seccion}:De no parece una dirección de correo.");
        }

        if (string.IsNullOrWhiteSpace(opciones.Usuario) != string.IsNullOrWhiteSpace(opciones.Clave))
        {
            return Fallo(
                $"{CorreoOpciones.Seccion}:Usuario y {CorreoOpciones.Seccion}:Clave " +
                "deben ir juntos, o ninguno de los dos.");
        }

        return ValidateOptionsResult.Success;
    }

    private static ValidateOptionsResult Fallo(string mensaje) => ValidateOptionsResult.Fail(mensaje);
}

/// <summary>Validación de los parámetros del job y de la bandeja al arrancar.</summary>
public sealed class NotificacionesOpcionesValidador : IValidateOptions<NotificacionesOpciones>
{
    public ValidateOptionsResult Validate(string? name, NotificacionesOpciones opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        if (opciones.IntervaloSegundos is < 5 or > 3600)
        {
            return Fallo($"{NotificacionesOpciones.Seccion}:IntervaloSegundos debe estar entre 5 y 3600.");
        }

        if (opciones.TamanoLote is < 1 or > 500)
        {
            return Fallo($"{NotificacionesOpciones.Seccion}:TamanoLote debe estar entre 1 y 500.");
        }

        if (opciones.MaximoIntentos is < 1 or > 10)
        {
            return Fallo($"{NotificacionesOpciones.Seccion}:MaximoIntentos debe estar entre 1 y 10.");
        }

        if (opciones.BackoffBaseSegundos is < 1 or > 3600)
        {
            return Fallo($"{NotificacionesOpciones.Seccion}:BackoffBaseSegundos debe estar entre 1 y 3600.");
        }

        if (opciones.TamanoBandeja is < 1 or > 200)
        {
            return Fallo($"{NotificacionesOpciones.Seccion}:TamanoBandeja debe estar entre 1 y 200.");
        }

        if (!string.IsNullOrWhiteSpace(opciones.CorreoInterno) &&
            (!opciones.CorreoInterno.Contains('@') || opciones.CorreoInterno.Any(char.IsWhiteSpace)))
        {
            return Fallo($"{NotificacionesOpciones.Seccion}:CorreoInterno no parece una dirección de correo.");
        }

        return ValidateOptionsResult.Success;
    }

    private static ValidateOptionsResult Fallo(string mensaje) => ValidateOptionsResult.Fail(mensaje);
}
