namespace Facturacion.Domain.Entidades;

public class Respaldo
{
    public int Id { get; set; }
    public DateTime IniciadoUtc { get; set; }
    public DateTime? FinalizadoUtc { get; set; }
    public string Usuario { get; set; } = "";
    public string NombreArchivo { get; set; } = "";
    public string RutaArchivo { get; set; } = "";
    public long? TamanoBytes { get; set; }
    public EstadoRespaldo Estado { get; set; }
    public bool Verificado { get; set; }
    public string? Mensaje { get; set; }
}
