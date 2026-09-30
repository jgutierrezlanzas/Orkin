using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Utilidades de normalización compartidas por todos los cargadores.
/// La interfaz <c>ICargadorCatalogo</c> vive en <see cref="Orkin.Cotizador.Core.Domain.Carga"/>.
/// </summary>
public static class Normalizacion
{
    /// <summary>
    /// Aplica el mapeo de columnas: toma los valores crudos y los deja con los nombres de
    /// campo del catálogo.
    /// <para>Con mapeo (<paramref name="mapeo"/> distinto de null) es una <b>proyección
    /// estricta</b>: las columnas sin mapeo se ignoran; las que faltan quedan en null. Que sea
    /// estricta es lo que hace que un mapeo con una columna mal escrita falle ruidosamente en
    /// vez de cargar el catálogo a medias.</para>
    /// <para>Sin mapeo (null) la fila pasa <b>tal cual</b>: la vista de origen ya trae los
    /// nombres que espera el cargador, que es el caso normal de un flujo bien configurado.</para>
    /// </summary>
    public static Dictionary<string, object?> AplicarMapeo(
        Dictionary<string, object?> fila,
        IReadOnlyDictionary<string, string>? mapeo)
    {
        if (mapeo is null)
        {
            return new Dictionary<string, object?>(fila, StringComparer.OrdinalIgnoreCase);
        }

        var resultado = new Dictionary<string, object?>(mapeo.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (columnaOrigen, campoDestino) in mapeo)
        {
            if (fila.TryGetValue(columnaOrigen, out var valor))
            {
                resultado[campoDestino] = valor;
            }
        }

        return resultado;
    }

    /// <summary>SHA-256 del contenido de la fila, para detectar cambios sin escribir.</summary>
    public static string HashContenido(Dictionary<string, object?> valores)
    {
        var estable = new Dictionary<string, string?>(valores.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (clave, valor) in valores.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            estable[clave] = valor?.ToString();
        }

        var json = JsonSerializer.Serialize(estable);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>Convierte un valor crudo a string, tratando null y vacío igual.</summary>
    public static string? AString(object? valor)
    {
        if (valor is null) return null;
        var texto = valor.ToString()?.Trim();
        return string.IsNullOrEmpty(texto) ? null : texto;
    }

    /// <summary>Convierte un valor crudo a decimal. Null o vacío → null (campo opcional).</summary>
    public static decimal? ADecimalOpcional(object? valor)
    {
        if (valor is null) return null;
        var texto = valor.ToString()?.Trim();
        if (string.IsNullOrEmpty(texto) || texto == "-") return null;

        if (decimal.TryParse(texto, out var resultado)) return resultado;

        // Excel a veces entrega valores numéricos como double
        if (valor is double d) return (decimal)d;
        if (valor is int i) return i;
        if (valor is long l) return l;

        throw new FormatException($"No se puede convertir '{valor}' a decimal.");
    }

    /// <summary>Convierte un valor crudo a decimal obligatorio. Null o vacío → excepción.</summary>
    public static decimal ADecimal(object? valor, string campo)
    {
        var resultado = ADecimalOpcional(valor)
            ?? throw new FormatException($"El campo '{campo}' es obligatorio y no tiene valor.");
        return resultado;
    }

    /// <summary>Convierte un valor crudo a int. Null o vacío → null.</summary>
    public static int? AEnteroOpcional(object? valor)
    {
        if (valor is null) return null;
        var texto = valor.ToString()?.Trim();
        if (string.IsNullOrEmpty(texto) || texto == "-") return null;

        if (int.TryParse(texto, out var resultado)) return resultado;
        if (valor is double d) return (int)d;
        if (valor is long l) return (int)l;

        throw new FormatException($"No se puede convertir '{valor}' a entero.");
    }

    /// <summary>Convierte un valor crudo a int obligatorio.</summary>
    public static int AEntero(object? valor, string campo)
    {
        return AEnteroOpcional(valor)
            ?? throw new FormatException($"El campo '{campo}' es obligatorio y no tiene valor.");
    }

    /// <summary>Convierte un valor crudo a bool. Acepta "Sí/No", "1/0", "true/false", "VERDADERO/FALSO".</summary>
    public static bool ABool(object? valor, bool porDefecto = false)
    {
        if (valor is null) return porDefecto;
        var texto = valor.ToString()?.Trim().ToUpperInvariant();

        return texto switch
        {
            "1" or "SI" or "SÍ" or "TRUE" or "VERDADERO" or "V" => true,
            "0" or "NO" or "FALSE" or "FALSO" or "F" => false,
            _ => porDefecto
        };
    }

    /// <summary>Convierte un valor crudo a DateOnly. Acepta DateTime de Excel o texto "yyyy-MM-dd".</summary>
    public static DateOnly? AFechaOpcional(object? valor)
    {
        if (valor is null) return null;

        if (valor is DateTime dt) return DateOnly.FromDateTime(dt);
        if (valor is DateOnly d) return d;

        var texto = valor.ToString()?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;

        if (DateOnly.TryParse(texto, out var fecha)) return fecha;
        if (DateTime.TryParse(texto, out var dt2)) return DateOnly.FromDateTime(dt2);

        throw new FormatException($"No se puede convertir '{valor}' a fecha.");
    }
}
