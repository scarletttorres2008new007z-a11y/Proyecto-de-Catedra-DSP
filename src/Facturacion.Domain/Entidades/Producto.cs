namespace Facturacion.Domain.Entidades;

public class Producto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }

    /// <summary>
    /// Precio de venta unitario SIN IVA (USD, 2 decimales). Es la base de cálculo: en la factura de
    /// consumidor final se presenta con IVA incluido; en CCF y nota de crédito se usa tal cual.
    /// </summary>
    public decimal PrecioSinIva { get; set; }

    /// <summary>Código de unidad de medida CAT-014.</summary>
    public int UnidadMedida { get; set; } = 59;

    /// <summary>Si es verdadero, las emisiones descuentan existencias y no permiten vender más de lo disponible.</summary>
    public bool ControlaInventario { get; set; }
    public decimal Existencias { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime CreadoUtc { get; set; }
    public DateTime? ModificadoUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
