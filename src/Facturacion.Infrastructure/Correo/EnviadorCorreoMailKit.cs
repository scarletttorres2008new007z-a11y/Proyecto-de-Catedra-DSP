using System.Net.Sockets;
using Facturacion.Application.Abstracciones;
using Facturacion.Domain.Reglas;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Facturacion.Infrastructure.Correo;

/// <summary>
/// Sección "Smtp" de la configuración. Usuario y contraseña deben venir de User Secrets o variables de entorno
/// (Smtp__Usuario, Smtp__Contrasena); nunca del repositorio.
/// </summary>
public sealed class OpcionesSmtp
{
    public const string Seccion = "Smtp";

    public string Servidor { get; set; } = "";
    public int Puerto { get; set; } = 587;
    /// <summary>Ninguna, SslAlConectar, StartTls, StartTlsSiDisponible o Automatica.</summary>
    public string Seguridad { get; set; } = "StartTls";
    public string? Usuario { get; set; }
    public string? Contrasena { get; set; }
    public string RemitenteCorreo { get; set; } = "";
    public string RemitenteNombre { get; set; } = "";
    public int TiempoEsperaSegundos { get; set; } = 30;
}

/// <summary>Envío SMTP con MailKit. Traduce los fallos a mensajes seguros (sin credenciales ni trazas).</summary>
public sealed class EnviadorCorreoMailKit(IOptionsMonitor<OpcionesSmtp> opciones) : IEnviadorCorreo
{
    public async Task<string?> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        var o = opciones.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.Servidor) || o.Puerto is < 1 or > 65535)
            throw new CorreoException(TipoFalloCorreo.Configuracion, "El servidor SMTP no está configurado.");
        if (!IdentificadoresFiscales.EsCorreoValido(o.RemitenteCorreo))
            throw new CorreoException(TipoFalloCorreo.Configuracion, "La dirección remitente no está configurada o no es válida.");

        MailboxAddress destinatario;
        try
        {
            destinatario = new MailboxAddress(mensaje.NombreDestinatario ?? "", mensaje.Destinatario);
        }
        catch (ParseException ex)
        {
            throw new CorreoException(TipoFalloCorreo.DestinatarioInvalido, "El correo del destinatario no es válido.", ex);
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(o.RemitenteNombre, o.RemitenteCorreo));
        mime.To.Add(destinatario);
        mime.Subject = mensaje.Asunto;
        var cuerpo = new BodyBuilder { TextBody = mensaje.CuerpoTexto, HtmlBody = mensaje.CuerpoHtml };
        foreach (var a in mensaje.Adjuntos)
            cuerpo.Attachments.Add(a.NombreArchivo, a.Contenido, ContentType.Parse(a.TipoContenido));
        mime.Body = cuerpo.ToMessageBody();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(o.TiempoEsperaSegundos, 5, 300)));
        using var cliente = new SmtpClient { Timeout = Math.Clamp(o.TiempoEsperaSegundos, 5, 300) * 1000 };

        try
        {
            await cliente.ConnectAsync(o.Servidor, o.Puerto, Seguridad(o.Seguridad), timeout.Token);
            if (!string.IsNullOrEmpty(o.Usuario))
                await cliente.AuthenticateAsync(o.Usuario, o.Contrasena ?? "", timeout.Token);
            var respuesta = await cliente.SendAsync(mime, timeout.Token);
            await cliente.DisconnectAsync(true, CancellationToken.None);
            return $"{mime.MessageId} | {respuesta}".Trim();
        }
        catch (AuthenticationException ex)
        {
            throw new CorreoException(TipoFalloCorreo.Autenticacion, "El servidor SMTP rechazó las credenciales configuradas.", ex);
        }
        catch (SmtpCommandException ex)
        {
            var detalle = ex.ErrorCode switch
            {
                SmtpErrorCode.RecipientNotAccepted => "el servidor no aceptó la dirección del destinatario",
                SmtpErrorCode.SenderNotAccepted => "el servidor no aceptó la dirección remitente",
                SmtpErrorCode.MessageNotAccepted => "el servidor no aceptó el mensaje",
                _ => "el servidor rechazó la operación"
            };
            throw new CorreoException(TipoFalloCorreo.RechazoServidor, $"Rechazo del servidor SMTP ({(int)ex.StatusCode}): {detalle}.", ex);
        }
        catch (SmtpProtocolException ex)
        {
            throw new CorreoException(TipoFalloCorreo.RechazoServidor, "Error de protocolo con el servidor SMTP.", ex);
        }
        catch (SslHandshakeException ex)
        {
            throw new CorreoException(TipoFalloCorreo.Conexion, "No se pudo establecer la conexión segura (TLS) con el servidor SMTP. Revise el puerto y el modo de seguridad.", ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new CorreoException(TipoFalloCorreo.TiempoAgotado, "Se agotó el tiempo de espera con el servidor SMTP.", ex);
        }
        catch (TimeoutException ex)
        {
            throw new CorreoException(TipoFalloCorreo.TiempoAgotado, "Se agotó el tiempo de espera con el servidor SMTP.", ex);
        }
        catch (SocketException ex)
        {
            throw new CorreoException(TipoFalloCorreo.Conexion, "No se pudo conectar con el servidor SMTP (servidor inaccesible o puerto cerrado).", ex);
        }
        catch (IOException ex)
        {
            throw new CorreoException(TipoFalloCorreo.Conexion, "Se perdió la conexión con el servidor SMTP.", ex);
        }
        catch (ServiceNotConnectedException ex)
        {
            throw new CorreoException(TipoFalloCorreo.Conexion, "No hay conexión con el servidor SMTP.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new CorreoException(TipoFalloCorreo.Configuracion, "El servidor SMTP no admite el modo de seguridad configurado.", ex);
        }
    }

    private static SecureSocketOptions Seguridad(string valor) => valor?.Trim().ToLowerInvariant() switch
    {
        "ninguna" or "none" => SecureSocketOptions.None,
        "sslalconectar" or "sslonconnect" => SecureSocketOptions.SslOnConnect,
        "starttls" => SecureSocketOptions.StartTls,
        "starttlssidisponible" or "starttlswhenavailable" => SecureSocketOptions.StartTlsWhenAvailable,
        _ => SecureSocketOptions.Auto
    };
}
