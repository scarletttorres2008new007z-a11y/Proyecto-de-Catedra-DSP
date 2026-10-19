using System.Text.RegularExpressions;

namespace Facturacion.Domain.Reglas;

/// <summary>
/// Normalización y validación de identificadores según los patrones de los esquemas JSON de los DTE:
///  * DUI (CAT-022 "13"): ^[0-9]{8}-[0-9]$
///  * NIT (CAT-022 "36"): ^([0-9]{14}|[0-9]{9})$  (14 dígitos, o 9 dígitos cuando el DUI está homologado como NIT)
///  * NRC: ^[0-9]{1,8}$
///  * Otros documentos (pasaporte, carnet de residente, otro): entre 3 y 20 caracteres.
/// La única normalización aplicada es quitar espacios y guiones de separación (NIT/NRC) o insertar
/// el guion del DUI; no se rellenan dígitos ni se alteran valores para hacerlos pasar por válidos.
/// No se verifica el dígito verificador del DUI/NIT porque el esquema oficial solo exige el formato.
/// </summary>
public static partial class IdentificadoresFiscales
{
    [GeneratedRegex(@"^[0-9]{8}-[0-9]$")] private static partial Regex DuiRegex();
    [GeneratedRegex(@"^([0-9]{14}|[0-9]{9})$")] private static partial Regex NitRegex();
    [GeneratedRegex(@"^[0-9]{1,8}$")] private static partial Regex NrcRegex();
    [GeneratedRegex(@"^[0-9]{2,6}$")] private static partial Regex ActividadRegex();
    [GeneratedRegex(@"^[0-9]{2}$")] private static partial Regex MunicipioRegex();
    [GeneratedRegex(@"^[A-Za-z0-9]{4}$")] private static partial Regex CodigoEstablecimientoRegex();
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")] private static partial Regex CorreoRegex();

    public static string? NormalizarDui(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim().Replace(" ", "");
        if (v.Length == 9 && v.All(char.IsAsciiDigit)) v = $"{v[..8]}-{v[8]}";
        return v;
    }

    public static string? NormalizarNit(string? valor) => SoloSeparadores(valor);

    public static string? NormalizarNrc(string? valor) => SoloSeparadores(valor);

    private static string? SoloSeparadores(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return valor.Trim().Replace("-", "").Replace(" ", "");
    }

    public static string? NormalizarDocumento(string? tipo, string? numero) => tipo switch
    {
        Catalogos.DocDui => NormalizarDui(numero),
        Catalogos.DocNit => NormalizarNit(numero),
        _ => string.IsNullOrWhiteSpace(numero) ? null : numero.Trim()
    };

    public static bool EsDuiValido(string? v) => v is not null && DuiRegex().IsMatch(v);
    public static bool EsNitValido(string? v) => v is not null && NitRegex().IsMatch(v);
    public static bool EsNrcValido(string? v) => v is not null && NrcRegex().IsMatch(v);
    public static bool EsActividadValida(string? v) => v is not null && ActividadRegex().IsMatch(v);
    public static bool EsMunicipioValido(string? v) => v is not null && MunicipioRegex().IsMatch(v);
    public static bool EsDepartamentoValido(string? v) => v is not null && Catalogos.Departamentos.ContainsKey(v);
    public static bool EsCodigoEstablecimientoValido(string? v) => v is not null && CodigoEstablecimientoRegex().IsMatch(v);

    public static bool EsCorreoValido(string? v) =>
        v is not null && v.Length <= 100 && CorreoRegex().IsMatch(v);

    /// <summary>Valida un número de documento ya normalizado según su tipo CAT-022.</summary>
    public static string? ValidarDocumento(string tipo, string? numero)
    {
        if (!Catalogos.TiposDocumentoIdentificacion.ContainsKey(tipo)) return "El tipo de documento no es válido.";
        if (string.IsNullOrWhiteSpace(numero)) return "Ingrese el número de documento.";
        return tipo switch
        {
            Catalogos.DocDui when !EsDuiValido(numero) => "El DUI debe tener 8 dígitos, guion y 1 dígito (00000000-0).",
            Catalogos.DocNit when !EsNitValido(numero) => "El NIT debe tener 14 dígitos (o 9 dígitos si es un DUI homologado), sin guiones.",
            _ when numero.Length is < 3 or > 20 => "El número de documento debe tener entre 3 y 20 caracteres.",
            _ => null
        };
    }
}
