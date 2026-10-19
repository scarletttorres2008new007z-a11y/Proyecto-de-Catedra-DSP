namespace Facturacion.Domain.Entidades;

/// <summary>Correlativo anual por tipo de DTE para construir el número de control.</summary>
public class Correlativo
{
    public int Id { get; set; }
    public TipoDte Tipo { get; set; }
    public int Anio { get; set; }
    public long Ultimo { get; set; }
}
