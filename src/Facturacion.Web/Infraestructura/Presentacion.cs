using System.Globalization;
using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Microsoft.AspNetCore.Html;

namespace Facturacion.Web.Infraestructura;

/// <summary>Formatos de El Salvador (USD, dd/MM/yyyy, hora local) y textos de estado.</summary>
public static class Presentacion
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-SV");

    public static string Moneda(decimal v) => v.ToString("C2", Cultura);

    public static string Cantidad(decimal v) => v.ToString("#,##0.####", Cultura);

    public static string Fecha(DateOnly f) => f.ToString("dd/MM/yyyy", Cultura);

    public static string FechaHoraLocal(DateTime utc, IReloj reloj) => reloj.ALocal(utc).ToString("dd/MM/yyyy HH:mm", Cultura);

    private static HtmlString Insignia(string texto, string clase) =>
        new($"<span class=\"insignia insignia-{clase}\">{System.Net.WebUtility.HtmlEncode(texto)}</span>");

    public static HtmlString EstadoDocumento(EstadoDocumento e, ModoOperacion modo) => e switch
    {
        Domain.EstadoDocumento.Emitido when modo == ModoOperacion.Academico => Insignia("Emitido (no transmitido)", "info"),
        Domain.EstadoDocumento.Emitido => Insignia("Emitido", "exito"),
        Domain.EstadoDocumento.Anulado => Insignia(modo == ModoOperacion.Academico ? "Anulado (simulado)" : "Anulado", "error"),
        _ => Insignia(e.ToString(), "neutra")
    };

    public static HtmlString EstadoEnvio(EstadoEnvio? e) => e switch
    {
        null => Insignia("Sin envíos", "neutra"),
        Domain.EstadoEnvio.Pendiente => Insignia("Pendiente", "aviso"),
        Domain.EstadoEnvio.EnviadoServidorSmtp => Insignia("Enviado al servidor SMTP", "exito"),
        Domain.EstadoEnvio.Fallido => Insignia("Fallido", "error"),
        _ => Insignia(e.ToString()!, "neutra")
    };

    public static string Motivo(MotivoEnvio m) => m switch
    {
        MotivoEnvio.Emision => "Emisión",
        MotivoEnvio.Reenvio => "Reenvío",
        MotivoEnvio.NotificacionAnulacion => "Notificación de anulación",
        _ => m.ToString()
    };

    public static HtmlString EstadoAnulacion(EstadoAnulacion e) => e switch
    {
        Domain.EstadoAnulacion.Solicitada => Insignia("Solicitada", "aviso"),
        Domain.EstadoAnulacion.ProcesadaSimulada => Insignia("Procesada (simulación académica)", "info"),
        Domain.EstadoAnulacion.ProcesadaOficial => Insignia("Procesada", "exito"),
        Domain.EstadoAnulacion.Rechazada => Insignia("Rechazada / fallida", "error"),
        _ => Insignia(e.ToString(), "neutra")
    };

    public static HtmlString Resultado(ResultadoOperacion r) => r switch
    {
        ResultadoOperacion.Exito => Insignia("Éxito", "exito"),
        ResultadoOperacion.Error => Insignia("Error", "error"),
        ResultadoOperacion.Advertencia => Insignia("Advertencia", "aviso"),
        _ => Insignia(r.ToString(), "neutra")
    };

    public static HtmlString EstadoRespaldo(EstadoRespaldo e) => e == Domain.EstadoRespaldo.Exitoso
        ? Insignia("Exitoso", "exito")
        : Insignia("Fallido", "error");

    public static HtmlString Activo(bool activo) => activo ? Insignia("Activo", "exito") : Insignia("Inactivo", "neutra");

    public static string TipoCliente(TipoCliente t) => t == Domain.TipoCliente.Contribuyente ? "Contribuyente" : "Consumidor final";

    public static string Tamano(long? bytes) => bytes switch
    {
        null => "—",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.0} MB"
    };
}
