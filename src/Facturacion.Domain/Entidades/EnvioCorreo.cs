namespace Facturacion.Domain.Entidades;

public class EnvioCorreo
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public DocumentoTributario? Documento { get; set; }
    public int? AnulacionId { get; set; }
    public TipoDte TipoDocumento { get; set; }
    public MotivoEnvio Motivo { get; set; }
    public string Destinatario { get; set; } = "";
    public int NumeroIntento { get; set; }
    public EstadoEnvio Estado { get; set; } = EstadoEnvio.Pendiente;
    public DateTime CreadoUtc { get; set; }
    public DateTime? ProcesadoUtc { get; set; }
    /// <summary>Mensaje de error apto para mostrar (sin credenciales ni detalles internos).</summary>
    public string? MensajeError { get; set; }
    /// <summary>Message-ID o respuesta del servidor SMTP, si está disponible.</summary>
    public string? IdProveedor { get; set; }
    public string Usuario { get; set; } = "";
}
