using System.Text;

namespace Facturacion.Domain.Reglas;

/// <summary>Convierte importes a letras para el campo totalLetras (ej.: "CIENTO VEINTE 50/100 DÓLARES").</summary>
public static class NumeroALetras
{
    private static readonly string[] Unidades =
    {
        "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", "DIEZ",
        "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE", "VEINTE",
        "VEINTIUNO", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    };

    private static readonly string[] Decenas = { "", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };

    private static readonly string[] Centenas =
    {
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    };

    public static string Convertir(decimal monto)
    {
        if (monto < 0) throw new ArgumentOutOfRangeException(nameof(monto), "El monto no puede ser negativo.");
        monto = Dinero.Redondear(monto);
        if (monto >= 1_000_000_000_000m) throw new ArgumentOutOfRangeException(nameof(monto), "Monto fuera de rango.");

        var entero = (long)Math.Truncate(monto);
        var centavos = (int)((monto - entero) * 100);
        var letras = entero == 0 ? "CERO" : Entero(entero);
        var moneda = entero == 1 ? "DÓLAR" : "DÓLARES";
        return $"{letras} {centavos:00}/100 {moneda}";
    }

    private static string Entero(long n)
    {
        var sb = new StringBuilder();
        var millones = n / 1_000_000;
        var miles = n / 1000 % 1000;
        var resto = (int)(n % 1000);

        if (millones > 0)
        {
            sb.Append(millones == 1 ? "UN MILLÓN" : $"{Apocopar(Entero(millones))} MILLONES");
        }
        if (miles > 0)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(miles == 1 ? "MIL" : $"{Apocopar(Cientos((int)miles))} MIL");
        }
        if (resto > 0)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(Cientos(resto));
        }
        return sb.ToString();
    }

    /// <summary>"VEINTIUNO MIL" → "VEINTIÚN MIL", "UNO MILLONES" → "UN MILLONES".</summary>
    private static string Apocopar(string texto)
    {
        if (texto.EndsWith("VEINTIUNO")) return texto[..^"VEINTIUNO".Length] + "VEINTIÚN";
        if (texto.EndsWith("UNO")) return texto[..^3] + "UN";
        return texto;
    }

    private static string Cientos(int n)
    {
        if (n == 100) return "CIEN";
        var c = n / 100;
        var r = n % 100;
        var partes = new List<string>();
        if (c > 0) partes.Add(Centenas[c]);
        if (r > 0)
        {
            if (r < 30) partes.Add(Unidades[r]);
            else
            {
                var d = r / 10;
                var u = r % 10;
                partes.Add(u == 0 ? Decenas[d] : $"{Decenas[d]} Y {Unidades[u]}");
            }
        }
        return string.Join(' ', partes);
    }
}
