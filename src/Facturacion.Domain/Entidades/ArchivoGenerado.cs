namespace Facturacion.Domain.Entidades;

public class ArchivoGenerado
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public DocumentoTributario? Documento { get; set; }
    public int? AnulacionId { get; set; }
    public PropositoArchivo Proposito { get; set; }
    public string NombreArchivo { get; set; } = "";
    /// <summary>Ruta relativa a la carpeta de almacenamiento configurada (nunca dentro de wwwroot).</summary>
    public string RutaRelativa { get; set; } = "";
    public long TamanoBytes { get; set; }
    public string Sha256 { get; set; } = "";
    /// <summary>Versión del esquema (JSON) o del formato de representación (PDF).</summary>
    public string Version { get; set; } = "";
    public DateTime GeneradoUtc { get; set; }
}
