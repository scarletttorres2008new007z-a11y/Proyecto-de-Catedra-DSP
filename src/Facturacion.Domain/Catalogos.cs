namespace Facturacion.Domain;

/// <summary>
/// Subconjunto de los catálogos del Sistema de Transmisión de DTE (Ministerio de Hacienda) que usa el proyecto.
/// Los códigos deben contrastarse con el documento "Catálogos – Facturación Electrónica" vigente
/// publicado en factura.gob.sv antes de una integración oficial.
/// </summary>
public static class Catalogos
{
    /// <summary>CAT-001 Ambiente de destino.</summary>
    public const string AmbientePruebas = "00";

    /// <summary>CAT-012 Departamentos.</summary>
    public static readonly IReadOnlyDictionary<string, string> Departamentos = new Dictionary<string, string>
    {
        ["01"] = "Ahuachapán",
        ["02"] = "Santa Ana",
        ["03"] = "Sonsonate",
        ["04"] = "Chalatenango",
        ["05"] = "La Libertad",
        ["06"] = "San Salvador",
        ["07"] = "Cuscatlán",
        ["08"] = "La Paz",
        ["09"] = "Cabañas",
        ["10"] = "San Vicente",
        ["11"] = "Usulután",
        ["12"] = "San Miguel",
        ["13"] = "Morazán",
        ["14"] = "La Unión"
    };

    /// <summary>CAT-022 Tipo de documento de identificación del receptor.</summary>
    public static readonly IReadOnlyDictionary<string, string> TiposDocumentoIdentificacion = new Dictionary<string, string>
    {
        ["36"] = "NIT",
        ["13"] = "DUI",
        ["03"] = "Pasaporte",
        ["02"] = "Carnet de residente",
        ["37"] = "Otro"
    };

    public const string DocNit = "36";
    public const string DocDui = "13";

    /// <summary>CAT-014 Unidades de medida (subconjunto).</summary>
    public static readonly IReadOnlyDictionary<int, string> UnidadesMedida = new Dictionary<int, string>
    {
        [59] = "Unidad",
        [58] = "Docena",
        [34] = "Kilogramo",
        [36] = "Libra",
        [23] = "Litro",
        [99] = "Otra"
    };

    /// <summary>CAT-017 Forma de pago (subconjunto).</summary>
    public static readonly IReadOnlyDictionary<string, string> FormasPago = new Dictionary<string, string>
    {
        ["01"] = "Billetes y monedas",
        ["02"] = "Tarjeta débito",
        ["03"] = "Tarjeta crédito",
        ["04"] = "Cheque",
        ["05"] = "Transferencia - depósito bancario",
        ["99"] = "Otros"
    };

    /// <summary>CAT-016 Condición de la operación. El proyecto solo emite operaciones al contado.</summary>
    public const int CondicionContado = 1;

    /// <summary>CAT-009 Tipo de establecimiento.</summary>
    public static readonly IReadOnlyDictionary<string, string> TiposEstablecimiento = new Dictionary<string, string>
    {
        ["01"] = "Sucursal / agencia",
        ["02"] = "Casa matriz",
        ["04"] = "Bodega",
        ["07"] = "Predio y/o patio",
        ["20"] = "Otro"
    };

    /// <summary>CAT-011 Tipo de ítem: 1 = Bienes.</summary>
    public const int TipoItemBienes = 1;

    /// <summary>CAT-015 Tributo IVA 13 %.</summary>
    public const string TributoIva = "20";
    public const string TributoIvaDescripcion = "Impuesto al Valor Agregado 13%";

    /// <summary>CAT-007 Tipo de generación del documento relacionado: 2 = electrónico.</summary>
    public const int TipoGeneracionElectronico = 2;

    public static readonly IReadOnlyDictionary<TipoAnulacion, string> TiposAnulacion = new Dictionary<TipoAnulacion, string>
    {
        [TipoAnulacion.ErrorEnInformacion] = "Error en la información del DTE (requiere documento de reemplazo)",
        [TipoAnulacion.RescindirOperacion] = "Rescindir la operación realizada",
        [TipoAnulacion.Otro] = "Otro (requiere documento de reemplazo)"
    };

    public static string NombreDepartamento(string? codigo) =>
        codigo is not null && Departamentos.TryGetValue(codigo, out var n) ? n : codigo ?? "";

    public static string NombreUnidad(int codigo) =>
        UnidadesMedida.TryGetValue(codigo, out var n) ? n : codigo.ToString();

    public static string NombreTipoDocumento(string? codigo) =>
        codigo is not null && TiposDocumentoIdentificacion.TryGetValue(codigo, out var n) ? n : codigo ?? "";
}
