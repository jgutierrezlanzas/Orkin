

namespace Orkin.Cotizador.Core.Domain.Catalogos;

/// <summary>
/// Un valor de catálogo, ya resuelto. Es lo que consumen la API y la UI.
/// <para><b>No lleva el tipo de la entidad</b> a propósito: a la capa de aplicación no le importa
/// de qué tabla vino, y así la UI puede pintar cualquier catálogo con el mismo componente.</para>
/// </summary>
/// <param name="Id">El <c>tinyint IDENTITY</c> que asignó la base. Nunca lo fija el código.</param>
/// <param name="Codigo">La clave estable en texto con la que el código se refiere al valor.</param>
/// <param name="Nombre">La etiqueta que ve el usuario. Vive en la base, no en un resource de C#.</param>
public sealed record ValorCatalogo(
    byte Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    byte Orden,
    bool EsActivo);

/// <summary>
/// Traduce el <c>Codigo</c> estable de un valor de catálogo a su <c>Id</c> (ADR-018).
/// <para>Es el único punto del sistema donde se consulta <c>dbo.*Catalogo</c> para código. Gracias a
/// él, ninguna otra parte de la aplicación necesita saber qué filas existen, ni sus números, ni sus
/// etiquetas.</para>
/// <para>La caché es obligatoria: sin ella, cada fila de un formulario dispara un <c>SELECT</c>.</para>
/// <para>Se invalida de forma explícita cuando un administrador agrega o desactiva un valor, para
/// que no haga falta reiniciar el proceso.</para>
/// </summary>
public interface ICatalogoResolver
{
    /// <summary>
    /// Resuelve el <c>Id</c> de un catálogo de dominio por su <c>Codigo</c>.
    /// </summary>
    /// <exception cref="CatalogoNoResueltoException">
    /// Si el código no existe o está inactivo. Es un error de arranque o de siembra, nunca algo que
    /// deba "devolver 0 y seguir": calcular en silencio con el valor equivocado es peor que caer.
    /// </exception>
    Task<byte> IdAsync<T>(string codigo, CancellationToken cancellationToken = default)
        where T : CatalogoDominioBase;

    /// <summary>Devuelve la fila completa, para pintar un desplegable o imprimir una etiqueta.</summary>
    Task<ValorCatalogo> ObtenerAsync<T>(string codigo, CancellationToken cancellationToken = default)
        where T : CatalogoDominioBase;

    /// <summary>Lista el catálogo completo, ordenado por <c>Orden</c> (nunca por texto).</summary>
    Task<IReadOnlyList<ValorCatalogo>> ListarAsync<T>(
        bool soloActivos = true,
        CancellationToken cancellationToken = default)
        where T : CatalogoDominioBase;

    /// <summary>
    /// Vacía la caché. La llama el servicio de administración después de crear, editar o desactivar
    /// un valor, para que el cambio se vea sin reiniciar.
    /// </summary>
    void InvalidarCache();
}

/// <summary>
/// Falla al resolver un código de catálogo. Es deliberadamente una excepción y no un
/// <c>byte?</c> devuelto: un <c>0</c> silencioso terminaría en una cotización calculada con el
/// método equivocado, y eso no se detecta hasta que un cliente recibe la cotización y la impugna.
/// </summary>
public sealed class CatalogoNoResueltoException : Exception
{
    public CatalogoNoResueltoException(string tabla, string codigo)
        : base($"El catálogo {tabla} no tiene una fila activa con Codigo = '{codigo}'.")
    {
        Tabla = tabla;
        Codigo = codigo;
    }

    /// <summary>Nombre de la tabla, para que el mensaje sea accionable.</summary>
    public string Tabla { get; }

    public string Codigo { get; }
}
