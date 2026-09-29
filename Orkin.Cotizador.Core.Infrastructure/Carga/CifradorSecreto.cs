using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Orkin.Cotizador.Core.Infrastructure.Carga;

/// <summary>
/// Cifra y descifra los secretos de la conexión al ERP del cliente (la contraseña de
/// <c>int.ErpConexion.ContrasenaCifrada</c>) con ASP.NET Core Data Protection.
/// <para><b>Por qué cifrar en vez de guardar la contraseña en claro:</b> la base del cotizador
/// guarda la del ERP del cliente, y un respaldo de esa base, o un <c>SELECT</c> de un
/// administrador, no debería entregar acceso al ERP de un cliente. Con Data Protection la
/// contraseña nunca existe en la base en texto legible.</para>
/// <para><b>El propósito (purpose string) fija para qué es el cifrado:</b> Data Protection impide
/// que un texto cifrado para "contraseña de conexión" se reutilice como si fuera, por ejemplo, un
/// token de sesión. El propósito va dentro del texto cifrado, así que un <c>Unprotect</c> con
/// otro propósito falla en vez de devolver algo válido.</para>
/// <para>El descifrado devuelve <c>null</c> cuando el texto no se puede descifrar (clave rotada,
/// dato de otra base). La conexión se considera inválida y se vuelve a pedir la contraseña, en
/// vez de tragar la excepción y seguir con una conexión rota.</para>
/// </summary>
public sealed class CifradorSecreto
{
    /// <summary>
    /// Propósito del cifrado. Cambiarlo invalida todas las contraseñas ya guardadas, así que
    /// forma parte de la configuración y no se escribe en tres lugares distintos.
    /// </summary>
    private const string Proposito = "Orkin.Cotizador.ErpConexion.Contrasena.v1";

    private readonly IDataProtector _protector;

    public CifradorSecreto(IDataProtectionProvider proveedor)
    {
        _protector = proveedor.CreateProtector(Proposito);
    }

    /// <summary>Cifra un secreto. <c>null</c> o vacío se devuelven tal cual, sin cifrar.</summary>
    public byte[]? Cifrar(string? valor)
        => string.IsNullOrEmpty(valor) ? null : Encoding.UTF8.GetBytes(_protector.Protect(valor));

    /// <summary>
    /// Descifra un secreto. Devuelve <c>null</c> si no hay nada que descifrar o si el texto no
    /// se puede descifrar (clave rotada o dato de otra instalación).
    /// </summary>
    public string? Descifrar(byte[]? cifrado)
    {
        if (cifrado is null or { Length: 0 })
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(Encoding.UTF8.GetString(cifrado));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Clave rotada o texto que no es de esta instalación: la conexión no se puede usar
            // hasta que se vuelva a guardar la contraseña. Se deja en null y se reporta.
            return null;
        }
    }
}