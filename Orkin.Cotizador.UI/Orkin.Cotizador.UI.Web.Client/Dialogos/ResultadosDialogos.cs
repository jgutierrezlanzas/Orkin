using Orkin.Cotizador.UI.Web.Client.Models;

namespace Orkin.Cotizador.UI.Web.Client.Dialogos;

/// <summary>
/// Lo que <see cref="ConexionDialogo"/> devuelve: el modelo y la contraseña tecleada, si se
/// tecleó. La contraseña viaja aparte del resto porque vacío significa "dejar la que hay" al
/// editar, y solo al crear es obligatoria.
/// </summary>
public sealed record ResultadoConexion(ErpConexionModel Modelo, string? Contrasena);

/// <summary>
/// Lo que <see cref="ProgramacionDialogo"/> devuelve: la programación tal como se pulsó Guardar.
/// Los opcionales llegan en null cuando la sincronización se deja en manual.
/// </summary>
public sealed record ResultadoProgramacion(
    string TipoDisparador,
    int? IntervaloSegundos,
    int? MaximoRegistrosPorCorrida,
    short? MaximoFallosConsecutivos);
