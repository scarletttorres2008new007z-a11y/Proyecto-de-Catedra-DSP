namespace Facturacion.Domain.Entidades;

/// <summary>Registro de auditoría de solo inserción.</summary>
public class RegistroAuditoria
{
    public long Id { get; set; }
    public DateTime FechaUtc { get; set; }
    public string Usuario { get; set; } = "";
    public string Accion { get; set; } = "";
    public string? Entidad { get; set; }
    public string? EntidadId { get; set; }
    public ResultadoOperacion Resultado { get; set; }
    public string? Detalle { get; set; }
}
