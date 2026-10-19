namespace Facturacion.Domain.Entidades;

public class Anulacion
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public DocumentoTributario? Documento { get; set; }

    /// <summary>Código de generación del evento de invalidación.</summary>
    public Guid CodigoGeneracion { get; set; }
    public TipoAnulacion Tipo { get; set; }
    public string Motivo { get; set; } = "";
    public int? DocumentoReemplazoId { get; set; }
    public Guid? CodigoGeneracionReemplazo { get; set; }

    public string NombreResponsable { get; set; } = "";
    public string TipoDocResponsable { get; set; } = "";
    public string NumDocResponsable { get; set; } = "";
    public string NombreSolicita { get; set; } = "";
    public string TipoDocSolicita { get; set; } = "";
    public string NumDocSolicita { get; set; } = "";

    public EstadoAnulacion Estado { get; set; } = EstadoAnulacion.Solicitada;
    public ModoOperacion Modo { get; set; } = ModoOperacion.Academico;
    public DateTime SolicitadaUtc { get; set; }
    public DateTime? ResueltaUtc { get; set; }
    public string? MensajeResultado { get; set; }
    public string Usuario { get; set; } = "";
}
