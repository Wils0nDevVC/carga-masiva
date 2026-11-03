using System.Globalization;
using System.Net;
using System.Text;

namespace Acity.Application.Notificaciones.Correo;

/// <summary>
/// Correo de notificación (Feature 016; constitución §8 v3.9.0): HTML con estilos en línea y colores, más texto plano
/// equivalente. Valores del usuario escapados; "finalizó" nunca se presenta como "sin errores".
/// </summary>
public static class PlantillaCorreo
{
    private const string AzulCabecera = "#1d4ed8";

    public static CorreoNotificacion Construir(long idCarga, DateTime fechaFin, ResumenCarga resumen, string? urlFrontend)
    {
        var asunto = $"Carga {idCarga} finalizada";
        var fecha = fechaFin.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var enlace = string.IsNullOrWhiteSpace(urlFrontend) ? null : $"{urlFrontend.TrimEnd('/')}/historial/{idCarga}";
        var restantes = resumen.TotalIncidencias - resumen.Primeras.Count;
        return new CorreoNotificacion(asunto, Html(idCarga, fecha, resumen, enlace, restantes), Texto(idCarga, fecha, resumen, enlace, restantes));
    }

    private static (string Texto, string Fondo) Color(string resultado) => resultado switch
    {
        "Completado" => ("#166534", "#dcfce7"),
        "Rechazado" or "Fallido" => ("#991b1b", "#fee2e2"),
        "Bloqueado" or "NoProcesado" => ("#92400e", "#fef3c7"),
        _ => ("#374151", "#f3f4f6"),
    };

    private static string E(string? valor) => WebUtility.HtmlEncode(valor ?? string.Empty);

    private static string Html(long idCarga, string fecha, ResumenCarga r, string? enlace, int restantes)
    {
        const string celda = "padding:6px 10px;border-bottom:1px solid #e5e7eb;font-size:13px;";
        const string cabecera = "padding:6px 10px;background:#f9fafb;text-align:left;font-size:12px;color:#6b7280;text-transform:uppercase;";
        var h = new StringBuilder();
        h.Append("<!doctype html><html><body style=\"margin:0;padding:0;background:#f3f4f6;font-family:Arial,Helvetica,sans-serif;color:#111827;\">");
        h.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f3f4f6;padding:24px 0;\"><tr><td align=\"center\">");
        h.Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#ffffff;border-radius:8px;overflow:hidden;\">");
        h.Append($"<tr><td style=\"background:{AzulCabecera};color:#ffffff;padding:20px 24px;font-size:20px;font-weight:bold;\">Carga {idCarga} finalizada</td></tr>");
        h.Append("<tr><td style=\"padding:20px 24px;\">");
        h.Append($"<p style=\"margin:0 0 12px;font-size:14px;\">La carga <b>{idCarga}</b> finalizó.</p>");
        h.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:14px;margin-bottom:20px;\">");
        h.Append($"<tr><td style=\"padding:2px 12px 2px 0;color:#6b7280;\">Archivo</td><td>{E(r.NombreArchivo)}</td></tr>");
        h.Append($"<tr><td style=\"padding:2px 12px 2px 0;color:#6b7280;\">Fecha de fin (UTC)</td><td>{fecha}</td></tr></table>");

        h.Append("<h2 style=\"font-size:16px;margin:0 0 8px;\">Resultados por periodo</h2>");
        if (r.Periodos.Count == 0)
        {
            h.Append("<p style=\"font-size:13px;color:#6b7280;\">Sin periodos procesados</p>");
        }
        else
        {
            h.Append($"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;margin-bottom:20px;\"><tr><th style=\"{cabecera}\">Periodo</th><th style=\"{cabecera}\">Resultado</th></tr>");
            foreach (var p in r.Periodos)
            {
                var (texto, fondo) = Color(p.Resultado);
                h.Append($"<tr><td style=\"{celda}\">{E(p.Periodo)}</td><td style=\"{celda}\"><span style=\"display:inline-block;padding:2px 8px;border-radius:4px;background:{fondo};color:{texto};font-weight:bold;font-size:12px;\">{E(p.Resultado)}</span></td></tr>");
            }
            h.Append("</table>");
        }

        h.Append("<h2 style=\"font-size:16px;margin:0 0 8px;\">Incidencias</h2>");
        if (r.TotalIncidencias == 0)
        {
            h.Append("<p style=\"font-size:13px;color:#6b7280;\">Sin incidencias</p>");
        }
        else
        {
            h.Append($"<p style=\"font-size:13px;margin:0 0 8px;\">Total: <b>{r.TotalIncidencias}</b> · ");
            h.Append(string.Join(" · ", r.PorTipo.Select(t => $"{E(t.Tipo)}: {t.Cantidad}")));
            h.Append("</p>");
            h.Append($"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;\"><tr><th style=\"{cabecera}\">Tipo</th><th style=\"{cabecera}\">Fila</th><th style=\"{cabecera}\">Código</th><th style=\"{cabecera}\">Periodo</th><th style=\"{cabecera}\">Motivo</th></tr>");
            foreach (var i in r.Primeras)
            {
                h.Append($"<tr><td style=\"{celda}\">{E(i.Tipo)}</td><td style=\"{celda}\">{i.NumeroFila}</td><td style=\"{celda}\">{E(i.CodigoProducto)}</td><td style=\"{celda}\">{E(i.PeriodoTexto)}</td><td style=\"{celda}\">{E(i.Motivo)}</td></tr>");
            }
            h.Append("</table>");
            if (restantes > 0)
            {
                h.Append($"<p style=\"font-size:13px;color:#6b7280;margin:8px 0 0;\">y {restantes} más; consulte el detalle de la carga.</p>");
            }
        }

        if (enlace is not null)
        {
            h.Append($"<p style=\"margin:24px 0 0;\"><a href=\"{E(enlace)}\" style=\"display:inline-block;background:{AzulCabecera};color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:6px;font-size:14px;font-weight:bold;\">Ver detalle</a></p>");
        }

        h.Append("</td></tr></table></td></tr></table></body></html>");
        return h.ToString();
    }

    private static string Texto(long idCarga, string fecha, ResumenCarga r, string? enlace, int restantes)
    {
        var t = new StringBuilder();
        t.AppendLine($"La carga {idCarga} finalizó.");
        t.AppendLine($"Archivo: {r.NombreArchivo}");
        t.AppendLine($"Fecha de fin (UTC): {fecha}");
        t.AppendLine();
        t.AppendLine("Resultados por periodo:");
        if (r.Periodos.Count == 0)
        {
            t.AppendLine("  Sin periodos procesados");
        }
        foreach (var p in r.Periodos)
        {
            t.AppendLine($"  {p.Periodo}: {p.Resultado}");
        }

        t.AppendLine();
        t.AppendLine("Incidencias:");
        if (r.TotalIncidencias == 0)
        {
            t.AppendLine("  Sin incidencias");
        }
        else
        {
            t.AppendLine($"  Total: {r.TotalIncidencias} ({string.Join(", ", r.PorTipo.Select(x => $"{x.Tipo}: {x.Cantidad}"))})");
            foreach (var i in r.Primeras)
            {
                var datos = new[]
                {
                    i.NumeroFila is null ? null : $"fila {i.NumeroFila}",
                    string.IsNullOrEmpty(i.CodigoProducto) ? null : $"código {i.CodigoProducto}",
                    string.IsNullOrEmpty(i.PeriodoTexto) ? null : $"periodo {i.PeriodoTexto}",
                }.Where(d => d is not null).ToList();
                var contexto = datos.Count == 0 ? "" : $" ({string.Join(", ", datos)})";
                t.AppendLine($"  - {i.Tipo}{contexto}: {i.Motivo}");
            }
            if (restantes > 0)
            {
                t.AppendLine($"  y {restantes} más; consulte el detalle de la carga.");
            }
        }

        if (enlace is not null)
        {
            t.AppendLine();
            t.AppendLine($"Ver detalle: {enlace}");
        }

        return t.ToString();
    }
}
