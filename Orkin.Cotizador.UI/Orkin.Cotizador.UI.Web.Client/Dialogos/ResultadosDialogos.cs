using Orkin.Cotizador.UI.Web.Client.Models;
namespace Orkin.Cotizador.UI.Web.Client.Dialogos;

/// <summary>
/// Lo que devuelve el diálogo de usuario: el modelo y, solo en el alta, la contraseña inicial
/// tecleada. Va en un record y no en propiedades del modelo porque la clave no es un dato del
/// usuario — es material de un solo uso, y el modelo vive en la lista de la página.
/// </summary>
public sealed record ResultadoUsuario(UsuarioModel Modelo, string? ContrasenaInicial);
