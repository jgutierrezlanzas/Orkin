using Orkin.Cotizador.Core.Application.Carga;

namespace Orkin.Cotizador.Tests.Carga;

/// <summary>
/// Pruebas del mapeo de columnas (<c>§12.13</c>).
/// <para>El mapeo es la pieza donde más cosas pueden salir bien en la pantalla y mal en la
/// corrida. Estas pruebas fijan las dos reglas que la tabla de homologación da por ciertas:
/// sin mapeo la fila pasa tal cual, y con mapeo es una proyección estricta.</para>
/// <para>La prueba que da nombre al archivo no es hipotética: se encontró porque un ERP de
/// pruebas con columnas abreviadas en mayúsculas (<c>DESCRIPCION</c>) rechazaba las 111 filas
/// del catálogo de productos con «El campo 'Nombre' es obligatorio», sin un solo error que
/// señalara la causa.</para>
/// </summary>
public sealed class MapeoColumnasTests
{
    /// <summary>
    /// Sin mapeo la fila pasa tal cual: es el caso normal de una vista bien construida, donde
    /// las columnas ya se llaman como los datos que espera el cargador.
    /// </summary>
    [Fact]
    public void Sin_mapeo_la_fila_pasa_tal_cual()
    {
        var fila = Fila(("Codigo", "P-001"), ("Nombre", "Trampa para ratas"));

        var mapeada = Normalizacion.AplicarMapeo(fila, null);

        Assert.Equal("P-001", mapeada["Codigo"]);
        Assert.Equal("Trampa para ratas", mapeada["Nombre"]);
    }

    /// <summary>
    /// Con mapeo es una proyección estricta: lo que no está en el mapeo no llega. Por eso la
    /// pantalla escribe todos los campos y no solo los renombrados —esto es lo que lo hace
    /// necesario, y lo que un mapeo "pueblo" rompería.
    /// </summary>
    [Fact]
    public void Con_mapeo_lo_que_no_esta_mapeado_no_llega()
    {
        var fila = Fila(("CODE", "P-001"), ("DESCRIPCION", "Trampa para ratas"), ("COSTO", 250m));

        var mapeada = Normalizacion.AplicarMapeo(fila, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CODE"] = "Codigo",
            ["DESCRIPCION"] = "Nombre"
        });

        Assert.Equal("P-001", mapeada["Codigo"]);
        Assert.Equal("Trampa para ratas", mapeada["Nombre"]);
        Assert.False(mapeada.ContainsKey("CostoActual"));
    }

    /// <summary>
    /// El nombre del destino se busca sin distinguir mayúsculas, porque los nombres de la
    /// plataforma son PascalCase y el cargador los pide con esa capitalización exacta. Un mapeo
    /// escrito en minúsculas tiene que funcionar igual.
    /// </summary>
    [Fact]
    public void El_mapeo_se_escribe_en_cualquier_mayuscula()
    {
        var fila = Fila(("codigo_producto", "P-001"), ("nombre", "Trampa"));

        var mapeada = Normalizacion.AplicarMapeo(fila, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["codigo_producto"] = "Codigo",
            ["nombre"] = "Nombre"
        });

        Assert.Equal("P-001", mapeada.GetValueOrDefault("Codigo"));
        Assert.Equal("Trampa", mapeada.GetValueOrDefault("Nombre"));
    }

    /// <summary>
    /// El caso que se encontró en producción: <c>DESCRIPCION</c> y <c>Descripcion</c> son la
    /// misma palabra sin distinguir mayúsculas, y ambos nombres son normales —el primero en
    /// cualquier ERP que abrevie en mayúsculas, el segundo porque es un campo de la plataforma.
    /// Con las claves del mapeo comparadas sin distinguir mayúsculas, una borra a la otra y el
    /// <c>Nombre</c> de todas las filas llega vacío.
    /// <para>Por eso las claves del mapeo se comparan <b>distinguiéndolas</b>: son columnas del
    /// ERP, y el ERP sí las distingue.</para>
    /// </summary>
    [Fact]
    public void Dos_columnas_que_solo_difieren_en_mayusculas_conviven()
    {
        var fila = Fila(("DESCRIPCION", "Trampa para ratas"), ("Descripcion", "Con.envase.azul"));

        var mapeada = Normalizacion.AplicarMapeo(fila, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DESCRIPCION"] = "Nombre",
            ["Descripcion"] = "Descripcion"
        });

        Assert.Equal("Trampa para ratas", mapeada.GetValueOrDefault("Nombre"));
        Assert.Equal("Con.envase.azul", mapeada.GetValueOrDefault("Descripcion"));
    }

    /// <summary>
    /// Muchas columnas apuntando al mismo dato sí es legítimo, y es lo que hace el mapeo del
    /// Excel de ORKIN con "Producto", "Nombre" y "Descripción del producto": son alias, y solo
    /// uno existe en cada archivo. No debe romperse por rechazar la repetición.
    /// </summary>
    [Fact]
    public void Varias_columnas_pueden_alimentar_el_mismo_dato()
    {
        var fila = Fila(("Producto", "Trampa para ratas"));

        var mapeada = Normalizacion.AplicarMapeo(fila, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Nombre"] = "Nombre",
            ["Producto"] = "Nombre",
            ["Descripción del producto"] = "Nombre"
        });

        Assert.Equal("Trampa para ratas", mapeada.GetValueOrDefault("Nombre"));
    }

    /// <summary>
    /// Arma una fila como la arman los lectores de verdad: <b>distinguiendo</b> mayúsculas.
    /// Con un diccionario que no las distinguiera, el caso de <c>DESCRIPCION</c> y
    /// <c>Descripcion</c> reventaría en el helper antes de llegar al mapeo, y la prueba
    /// estaría midiendo el helper y no lo que dice medir.
    /// </summary>
    private static Dictionary<string, object?> Fila(params (string Columna, object? Valor)[] pares)
        => pares.ToDictionary(p => p.Columna, p => p.Valor, StringComparer.Ordinal);
}
