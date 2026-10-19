namespace Facturacion.Domain.Entidades;

/// <summary>Datos del emisor (configuración no secreta). Solo existe una fila.</summary>
public class ConfiguracionEmisor
{
    public int Id { get; set; }
    public string Nit { get; set; } = "";
    public string Nrc { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string CodActividad { get; set; } = "";
    public string DescActividad { get; set; } = "";
    public string? NombreComercial { get; set; }
    public string TipoEstablecimiento { get; set; } = "02";
    public string Departamento { get; set; } = "";
    public string Municipio { get; set; } = "";
    public string Complemento { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Correo { get; set; } = "";
    /// <summary>Código de establecimiento (4 caracteres) usado en el número de control.</summary>
    public string CodEstable { get; set; } = "M001";
    /// <summary>Código de punto de venta (4 caracteres) usado en el número de control.</summary>
    public string CodPuntoVenta { get; set; } = "P001";
    public DateTime ModificadoUtc { get; set; }
    public string ModificadoPor { get; set; } = "";
}
