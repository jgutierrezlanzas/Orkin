using System.Globalization;

using Microsoft.AspNetCore.Identity;

namespace Orkin.Cotizador.Api.Seguridad;

/// <summary>
/// Traduce al español los errores de Identity que llegan al usuario.
/// <para><b>Por qué existe:</b> el <c>IdentityErrorDescriber</c> por defecto escribe "Passwords must
/// have at least one digit" y el ingreso en el resto de la API responde en español. Los errores de
/// política de contraseña no pasan por los textos del controlador —los genera Identity dentro del
/// <c>UserManager</c>—, así que el único lugar donde se pueden traducir para siempre es acá, y no en
/// un <c>if</c> por cada endpoint.</para>
/// <para><b>Por qué se registra en DI y no se crea a mano:</b> el <c>UserManager</c> resuelve el
/// describer de la inyección de dependencias, de forma que registrarlo acá lo cambia en todos los
/// lugares que usan Identity —ingreso, cambio de contraseña, creación de usuarios, semilla— sin
/// tocar nada más.</para>
/// <para><b>Qué se traduce y qué no:</b> solo lo que puede ver una persona. Los códigos
/// (<c>PasswordTooShort</c>, <c>DuplicateEmail</c>...) no se tocan: los compara el código por
/// cadena exacta, y cambiar un código rompería, por ejemplo, el reconocimiento de
/// <c>PasswordMismatch</c> en el cambio de contraseña.</para>
/// </summary>
public sealed class IdentityErrorDescriberEspanol : IdentityErrorDescriber
{
    private static IdentityError Nuevo(string codigo, string descripcion)
        => new() { Code = codigo, Description = descripcion };

    // ── Fallos generales ────────────────────────────────────────────────────────

    public override IdentityError DefaultError()
        => Nuevo(nameof(DefaultError), "No se pudo completar la operación. Vuelva a intentarlo.");

    public override IdentityError ConcurrencyFailure()
        => Nuevo(nameof(ConcurrencyFailure), "Los datos cambiaron entre la lectura y el guardado. Recargue e intente de nuevo.");

    public override IdentityError InvalidToken()
        => Nuevo(nameof(InvalidToken), "El enlace o el código no son válidos. Pida uno nuevo.");

    public override IdentityError LoginAlreadyAssociated()
        => Nuevo(nameof(LoginAlreadyAssociated), "Esa cuenta externa ya está vinculada a otro usuario.");

    public override IdentityError RecoveryCodeRedemptionFailed()
        => Nuevo(nameof(RecoveryCodeRedemptionFailed), "El código de recuperación no es válido.");

    // ── Usuario, correo y rol ───────────────────────────────────────────────────

    public override IdentityError InvalidUserName(string? userName)
        => Nuevo(nameof(InvalidUserName), $"El nombre de usuario '{userName}' no es válido.");

    public override IdentityError InvalidEmail(string? email)
        => Nuevo(nameof(InvalidEmail), $"El correo '{email}' no es válido.");

    public override IdentityError DuplicateUserName(string? userName)
        => Nuevo(nameof(DuplicateUserName), $"El nombre de usuario '{userName}' ya está en uso.");

    public override IdentityError DuplicateEmail(string? email)
        => Nuevo(nameof(DuplicateEmail), $"El correo '{email}' ya está en uso.");

    public override IdentityError InvalidRoleName(string? role)
        => Nuevo(nameof(InvalidRoleName), $"El rol '{role}' no es válido.");

    public override IdentityError DuplicateRoleName(string? role)
        => Nuevo(nameof(DuplicateRoleName), $"El rol '{role}' ya existe.");

    public override IdentityError UserAlreadyHasPassword()
        => Nuevo(nameof(UserAlreadyHasPassword), "Este usuario ya tiene una contraseña.");

    public override IdentityError UserLockoutNotEnabled()
        => Nuevo(nameof(UserLockoutNotEnabled), "El bloqueo no está habilitado para este usuario.");

    public override IdentityError UserAlreadyInRole(string? role)
        => Nuevo(nameof(UserAlreadyInRole), $"El usuario ya tiene el rol '{role}'.");

    public override IdentityError UserNotInRole(string? role)
        => Nuevo(nameof(UserNotInRole), $"El usuario no tiene el rol '{role}'.");

    // ── Política de contraseña ──────────────────────────────────────────────────

    public override IdentityError PasswordTooShort(int length)
        => Nuevo(nameof(PasswordTooShort),
            $"La contraseña tiene que tener al menos {length.ToString(CultureInfo.CurrentCulture)} caracteres.");

    public override IdentityError PasswordRequiresDigit()
        => Nuevo(nameof(PasswordRequiresDigit), "La contraseña tiene que tener al menos un dígito (0-9).");

    public override IdentityError PasswordRequiresUpper()
        => Nuevo(nameof(PasswordRequiresUpper), "La contraseña tiene que tener al menos una letra mayúscula (A-Z).");

    public override IdentityError PasswordRequiresLower()
        => Nuevo(nameof(PasswordRequiresLower), "La contraseña tiene que tener al menos una letra minúscula (a-z).");

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => Nuevo(nameof(PasswordRequiresNonAlphanumeric), "La contraseña tiene que tener al menos un carácter especial.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => Nuevo(nameof(PasswordRequiresUniqueChars),
            $"La contraseña tiene que tener al menos {uniqueChars.ToString(CultureInfo.CurrentCulture)} caracteres distintos.");

    public override IdentityError PasswordMismatch()
        => Nuevo(nameof(PasswordMismatch), "La contraseña no es correcta.");
}