using Orkin.Cotizador.UI.Web.Client.Models;
namespace Orkin.Cotizador.UI.Web.Client.Dialogos;

/// <summary>
/// Lo que <see cref="ConexionDialogo"/> devuelve: el modelo y la contraseña tecleada, si se
/// tecleó. La contraseña viaja aparte del resto porque vacío significa "dejar la que hay" al
/// editar, y solo al crear es obligatoria.
/// </summary>
public sealed record ResultadoConexion(ErpConexionModel Modelo, string? Contrasena);

/// <summary>
/// Lo que devuelve el diálogo de usuario: el modelo y, solo en el alta, la contraseña inicial
/// tecleada. Va en un record y no en propiedades del modelo porque la clave no es un dato del
/// usuario — es material de un solo uso, y el modelo vive en la lista de la página.
/// </summary>
public sealed record ResultadoUsuario(UsuarioModel Modelo, string? ContrasenaInicial);

/// <summary>
/// Lo que <see cref="ProgramacionDialogo"/> devuelve: la programación tal como se pulsó Guardar.
/// Los opcionales llegan en null cuando la sincronización se deja en manual.
/// </summary>
public sealed record ResultadoProgramacion(
    string TipoDisparador,
    int? IntervaloSegundos,
    int? MaximoRegistrosPorCorrida,
    short? MaximoFallosConsecutivos);
