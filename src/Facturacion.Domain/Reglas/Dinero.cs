namespace Facturacion.Domain.Reglas;

/// <summary>Reglas de redondeo monetario. Todos los importes se manejan como decimal (nunca float/double).</summary>
public static class Dinero
{
    /// <summary>Tasa del IVA en El Salvador (13 %).</summary>
    public const decimal TasaIva = 0.13m;

    /// <summary>Redondeo a centavos, mitad hacia arriba (alejándose de cero).</summary>
    public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static int Decimales(decimal valor)
    {
        valor = Math.Abs(valor);
        var escala = (decimal.GetBits(valor)[3] >> 16) & 0xFF;
        // Ignora ceros a la derecha (1.50m tiene escala 2 pero 1 decimal significativo).
        var normalizado = valor / 1.0000000000000000000000000000m;
        return Math.Min(escala, (decimal.GetBits(normalizado)[3] >> 16) & 0xFF);
    }
}
