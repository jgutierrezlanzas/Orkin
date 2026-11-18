using System.Net;

namespace Orkin.Cotizador.Core.Application.Notificaciones;

/// <summary>
/// Plantillas de los avisos que salen de los SPs legacy. Viven acá porque nadie las pidió editables
/// desde Administración (ADR-018): cambiar una es un cambio de código, no un parámetro.
/// <para>Todo dato que viene del ERP se codifica en HTML antes de meterse en el cuerpo. Un nombre
/// de cliente con <c>&lt;</c> no puede convertirse en markup del correo.</para>
/// </summary>
public static class PlantillasCorreo
{
    private const string Encabezado =
        "<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#222;border-top:4px solid #E31837;padding:16px;\">" +
        "<h2 style=\"color:#E31837;margin:0 0 12px;\">ORKIN</h2>";

    private const string Pie =
        "<p style=\"font-size:11px;color:#777;margin-top:20px;\">" +
        "Este aviso se generó desde la información de programación de visitas de SINCOERPORKIN.</p></div>";

    public static string Visita(
        string cliente,
        string nombreCliente,
        string locacion,
        string servicio,
        string fechaInicio,
        string horaInicio,
        string fechaFinal,
        string horaFinal,
        string tecnico,
        string contacto)
    {
        var filas = new[]
        {
            ("Cliente", $"{cliente} — {nombreCliente}"),
            ("Locación", locacion),
            ("Servicio", servicio),
            ("Inicio", $"{fechaInicio} {horaInicio}".Trim()),
            ("Final", $"{fechaFinal} {horaFinal}".Trim()),
            ("Técnico", tecnico),
            ("Contacto", contacto)
        };

        var cuerpo = string.Concat(filas.Select(f =>
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#666;\">{Html(f.Item1)}</td>" +
            $"<td style=\"padding:4px 0;\">{Html(f.Item2)}</td></tr>"));

        return Encabezado +
            "<p>Se tiene programada la siguiente visita:</p>" +
            $"<table style=\"border-collapse:collapse;font-size:14px;\">{cuerpo}</table>" +
            "<p>Por favor confirme la recepción de este aviso.</p>" + Pie;
    }

    public static string RequisasErradas(string numero, IReadOnlyList<string> lineas)
    {
        var detalle = string.Concat(lineas.Select(l => $"<li>{Html(l)}</li>"));

        return Encabezado +
            $"<p>La requisa <strong>{Html(numero)}</strong> tiene líneas con costo unitario " +
            "menor o igual a cero, o centro de costo sin asignar.</p>" +
            $"<ul style=\"font-size:13px;\">{detalle}</ul>" +
            "<p>Revisela en el módulo de requisas antes de trasladarla a Exactus.</p>" + Pie;
    }

    private static string Html(string? valor) => WebUtility.HtmlEncode(valor ?? string.Empty);
}
