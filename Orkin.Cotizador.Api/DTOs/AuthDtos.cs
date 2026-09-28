namespace Orkin.Cotizador.Api.DTOs;

/// <summary>
/// Lo que se manda a <c>POST /api/auth/login</c>.
/// <para><b>Por qué se pide el correo y no el nombre de cuenta:</b> una persona que entra a cotizar
/// se acuerda de su correo, no de un nombre de usuario que le dio el administrador. Guardar las dos
/// cosas es lo que permite que el mismo usuario escriba cualquiera de las dos. Aceptar las dos en el
/// mismo campo evita duplicar el endpoint y el formulario, y el backend decide con cuál comparar.</para>
/// </summary>
/// <param name="Credencial">Correo electrónico o nombre de cuenta, sin distinguir.</param>
/// <param name="Contrasena">La contraseña. No se registra en el log en ningún caso.</param>
/// <param name="Recordar">Reservado para cuando exista la opción de "mantener la sesión". Hoy no hace
/// nada, y está declarado para que la interfaz pueda mandarlo sin romperse el día que exista.</param>
public sealed record SolicitudIngreso(
    string Credencial,
    string Contrasena,
    bool Recordar = false);

/// <summary>
/// Lo que devuelve un ingreso exitoso.
/// <para><b>Por qué viaja la lista de permisos:</b> la interfaz la usa para mostrar u ocultar menús
/// y botones sin preguntar a la API. Sin ella, la UI tendría que pedirla por separado y esperar a
/// que llegue, con la pantalla en blanco mientras tanto. Y si la UI los guardara para decidir por su
/// cuenta, tendría que meterlos en el navegador, que es exactamente el lugar donde un usuario puede
/// cambiarlos.</para>
/// </summary>
/// <param name="Token">El <c>JWT</c> en texto, para el encabezado <c>Authorization</c>.</param>
/// <param name="ExpiraUtc">Cuándo deja de servir. La UI avisa antes de que pase, no después.</param>
/// <param name="Usuario">Quién entró. La UI lo muestra en el encabezado.</param>
/// <param name="Permisos">Códigos de <c>seg.Permiso</c> efectivos. Ver <c>Claims.Permiso</c>.</param>
public sealed record RespuestaIngreso(
    string Token,
    DateTime ExpiraUtc,
    DatosUsuario Usuario,
    IReadOnlyList<string> Permisos);

/// <summary>
/// Quién es el usuario, sin nada sensible.
/// <para>No lleva el correo ni la contraseña hasheada porque estos datos se dibujan en el
/// encabezado, y un encabezado se fotografía: se usa en capturas, en reportes y en pantallas
/// compartidas por videollamada. Lo mínimo que hace falta para reconocer a la persona es el nombre,
/// el identificador para las llamadas, y si tiene que cambiar la clave.</para>
/// </summary>
/// <param name="Id">Identificador del usuario, el mismo <c>Guid</c> de <c>seg.Usuario</c>.</param>
/// <param name="NombreCompleto">Lo que se muestra arriba.</param>
/// <param name="Cargo">Lo que se muestra debajo del nombre, si hay.</param>
/// <param name="DebeCambiarContrasena">Si es <c>true</c>, la UI lleva al cambio de clave antes de
/// dejarlo hacer cualquier otra cosa.</param>
public sealed record DatosUsuario(
    Guid Id,
    string NombreCompleto,
    string? Cargo,
    bool DebeCambiarContrasena);

/// <summary>Lo que devuelve <c>POST /api/auth/salir</c>: nada, solo la confirmación.</summary>
/// <param name="Mensaje">Texto para avisarle a la persona que salió.</param>
public sealed record RespuestaSalida(string Mensaje);

/// <summary>
/// Lo que se manda a <c>POST /api/auth/cambiar-contrasena</c>.
/// <para><b>Por qué hay que mandar la contraseña actual:</b> porque el endpoint exige un token
/// válido, y un token válido puede estar en la mano de otra persona: un equipo compartido, una
/// sesión abierta que se olvidó cerrar, un token copiado. Sin la contraseña actual, quien tuviera
/// el token podría cambiar la clave y quedarse con la cuenta. Ese es el motivo por el que existe,
/// y no por comodidad de la interfaz.</para>
/// <para><b>Por qué la nueva no se manda dos veces:</b> el formulario la pide una y la confirma en el
/// cliente. El servidor no necesita las dos, y una segunda contraseña en el cuerpo sería un segundo
/// valor que puede no coincidir con el primero sin que nadie lo note. Que no coincidan es un error
/// de la pantalla, no del servidor.</para>
/// </summary>
/// <param name="ContrasenaActual">La contraseña con la que se está entrando ahora.</param>
/// <param name="ContrasenaNueva">La que se quiere poner. La policy decide si sirve.</param>
public sealed record SolicitudCambioContrasena(
    string ContrasenaActual,
    string ContrasenaNueva);

/// <summary>
/// Lo que devuelve un cambio de contraseña exitoso.
/// <para><b>Por qué viaja el token nuevo y no solo un mensaje:</b> el cambio mata la sesión que lo
/// hizo, y hay que decirlo. Si la respuesta fuera solo "listo", la interfaz tendría que recibir un 401
/// en la siguiente llamada, adivinar que fue por el cambio, tirar el token y pedir un ingreso nuevo. Devolviendo el token ya emitido, la persona sigue trabajando sin volver a escribir su
/// contraseña, y las otras sesiones —que sí mueren— no se pueden distinguir desde el navegador, que
/// es lo correcto: nadie debería enterarse por la sesión de otro.</para>
/// </summary>
/// <param name="Token">Un token nuevo, ya válido. El anterior queda revocado.</param>
/// <param name="ExpiraUtc">Cuándo deja de servir este token nuevo.</param>
public sealed record RespuestaCambioContrasena(
    string Token,
    DateTime ExpiraUtc);
