using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Domain.Reglas;

namespace Facturacion.Application.Dte;

/// <summary>
/// Construye, valida y serializa (System.Text.Json) la representación JSON de cada tipo de DTE a partir del
/// documento guardado. Cada tipo usa su propia estructura. El resultado NO incluye firma electrónica ni
/// sello de recepción: en modo académico se marca con un apéndice de aviso.
/// </summary>
public static partial class GeneradorDteJson
{
    public const string AvisoAcademico =
        "Representación de demostración académica. No transmitido ni aceptado por el Ministerio de Hacienda.";

    public static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Conserva tildes y eñes legibles; System.Text.Json sigue escapando caracteres de control.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [GeneratedRegex(@"^DTE-(01|03|05)-[A-Z0-9]{8}-[0-9]{15}$")] private static partial Regex NumeroControlRegex();
    [GeneratedRegex(@"^[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}$")] private static partial Regex CodigoGeneracionRegex();

    public static string Codigo(Guid codigoGeneracion) => codigoGeneracion.ToString("D").ToUpperInvariant();

    public static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Hora(TimeOnly hora) => hora.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static object ConstruirModelo(DocumentoTributario doc, EmisorSnapshot emisor) => doc.Tipo switch
    {
        TipoDte.FacturaConsumidorFinal => ConstruirFactura(doc, emisor),
        TipoDte.ComprobanteCreditoFiscal => ConstruirCreditoFiscal(doc, emisor),
        TipoDte.NotaCredito => ConstruirNotaCredito(doc, emisor),
        _ => throw new ArgumentOutOfRangeException(nameof(doc), "Tipo de DTE no soportado.")
    };

    /// <summary>Construye el modelo, lo valida y devuelve el JSON serializado.</summary>
    public static string Generar(DocumentoTributario doc, EmisorSnapshot emisor)
    {
        var modelo = ConstruirModelo(doc, emisor);
        var validacion = Validar(modelo);
        if (!validacion.EsValido)
            throw new ReglaNegocioException(validacion);
        return JsonSerializer.Serialize(modelo, modelo.GetType(), Opciones);
    }

    private static IdentificacionDte Identificacion(DocumentoTributario doc) => new()
    {
        Version = doc.VersionEsquema,
        Ambiente = doc.Ambiente,
        TipoDte = TiposDteInfo.Codigo(doc.Tipo),
        NumeroControl = doc.NumeroControl,
        CodigoGeneracion = Codigo(doc.CodigoGeneracion),
        FecEmi = Fecha(doc.FechaEmision),
        HorEmi = Hora(doc.HoraEmision)
    };

    private static List<ApendiceDte>? Apendice(DocumentoTributario doc) => doc.Modo == ModoOperacion.Academico
        ? new List<ApendiceDte> { new() { Campo = "modoAcademico", Etiqueta = "Aviso", Valor = AvisoAcademico } }
        : null;

    private static DireccionDte Direccion(string? dep, string? mun, string? comp) =>
        new() { Departamento = dep ?? "", Municipio = mun ?? "", Complemento = comp ?? "" };

    private static EmisorEstablecimientoDte EmisorConEstablecimiento(EmisorSnapshot e) => new()
    {
        Nit = e.Nit,
        Nrc = e.Nrc,
        Nombre = e.Nombre,
        CodActividad = e.CodActividad,
        DescActividad = e.DescActividad,
        NombreComercial = e.NombreComercial,
        TipoEstablecimiento = e.TipoEstablecimiento,
        Direccion = Direccion(e.Departamento, e.Municipio, e.Complemento),
        Telefono = e.Telefono,
        Correo = e.Correo,
        CodEstableMH = null,
        CodEstable = e.CodEstable,
        CodPuntoVentaMH = null,
        CodPuntoVenta = e.CodPuntoVenta
    };

    private static ReceptorContribuyenteDte ReceptorContribuyente(DocumentoTributario d) => new()
    {
        Nit = d.ReceptorNumDocumento ?? "",
        Nrc = d.ReceptorNrc ?? "",
        Nombre = d.ReceptorNombre,
        CodActividad = d.ReceptorCodActividad ?? "",
        DescActividad = d.ReceptorDescActividad ?? "",
        NombreComercial = d.ReceptorNombreComercial,
        Direccion = Direccion(d.ReceptorDepartamento, d.ReceptorMunicipio, d.ReceptorComplemento),
        Telefono = d.ReceptorTelefono,
        Correo = d.ReceptorCorreo ?? ""
    };

    private static List<PagoDte> Pagos(DocumentoTributario d) => new()
    {
        new PagoDte { Codigo = d.FormaPago ?? "01", MontoPago = d.TotalPagar }
    };

    private static FacturaDte ConstruirFactura(DocumentoTributario d, EmisorSnapshot e) => new()
    {
        Identificacion = Identificacion(d),
        Emisor = EmisorConEstablecimiento(e),
        Receptor = new ReceptorFacturaDte
        {
            TipoDocumento = d.ReceptorTipoDocumento,
            NumDocumento = d.ReceptorNumDocumento,
            Nrc = d.ReceptorTipoDocumento == Catalogos.DocNit ? d.ReceptorNrc : null,
            Nombre = d.ReceptorNombre,
            CodActividad = d.ReceptorCodActividad,
            DescActividad = d.ReceptorDescActividad,
            Direccion = d.ReceptorDepartamento is null ? null : Direccion(d.ReceptorDepartamento, d.ReceptorMunicipio, d.ReceptorComplemento),
            Telefono = d.ReceptorTelefono,
            Correo = d.ReceptorCorreo
        },
        CuerpoDocumento = d.Detalles.OrderBy(x => x.NumItem).Select(x => new ItemFacturaDte
        {
            NumItem = x.NumItem,
            TipoItem = Catalogos.TipoItemBienes,
            Cantidad = x.Cantidad,
            Codigo = x.Codigo,
            UniMedida = x.UnidadMedida,
            Descripcion = x.Descripcion,
            PrecioUni = x.PrecioUnitario,
            MontoDescu = x.MontoDescuento,
            VentaGravada = x.VentaGravada,
            Tributos = null,
            IvaItem = x.IvaItem
        }).ToList(),
        Resumen = new ResumenFacturaDte
        {
            TotalGravada = d.TotalGravada,
            SubTotalVentas = d.TotalGravada,
            TotalDescu = d.TotalDescuento,
            SubTotal = d.SubTotal,
            MontoTotalOperacion = d.MontoTotalOperacion,
            TotalPagar = d.TotalPagar,
            TotalLetras = d.TotalLetras,
            TotalIva = d.TotalIva,
            CondicionOperacion = d.CondicionOperacion,
            Pagos = Pagos(d)
        },
        Apendice = Apendice(d)
    };

    private static CreditoFiscalDte ConstruirCreditoFiscal(DocumentoTributario d, EmisorSnapshot e) => new()
    {
        Identificacion = Identificacion(d),
        Emisor = EmisorConEstablecimiento(e),
        Receptor = ReceptorContribuyente(d),
        CuerpoDocumento = d.Detalles.OrderBy(x => x.NumItem).Select(x => new ItemCreditoFiscalDte
        {
            NumItem = x.NumItem,
            TipoItem = Catalogos.TipoItemBienes,
            Codigo = x.Codigo,
            Descripcion = x.Descripcion,
            Cantidad = x.Cantidad,
            UniMedida = x.UnidadMedida,
            PrecioUni = x.PrecioUnitario,
            MontoDescu = x.MontoDescuento,
            VentaGravada = x.VentaGravada,
            Tributos = new List<string> { Catalogos.TributoIva }
        }).ToList(),
        Resumen = new ResumenCreditoFiscalDte
        {
            TotalGravada = d.TotalGravada,
            SubTotalVentas = d.TotalGravada,
            TotalDescu = d.TotalDescuento,
            Tributos = new List<TributoResumenDte>
            {
                new() { Codigo = Catalogos.TributoIva, Descripcion = Catalogos.TributoIvaDescripcion, Valor = d.TotalIva }
            },
            SubTotal = d.SubTotal,
            MontoTotalOperacion = d.MontoTotalOperacion,
            TotalPagar = d.TotalPagar,
            TotalLetras = d.TotalLetras,
            CondicionOperacion = d.CondicionOperacion,
            Pagos = Pagos(d)
        },
        Apendice = Apendice(d)
    };

    private static NotaCreditoDte ConstruirNotaCredito(DocumentoTributario d, EmisorSnapshot e)
    {
        var relacionado = d.RelacionadoCodigoGeneracion is { } cg ? Codigo(cg) : "";
        return new NotaCreditoDte
        {
            Identificacion = Identificacion(d),
            DocumentoRelacionado = d.RelacionadoCodigoGeneracion is null
                ? new List<DocumentoRelacionadoDte>()
                : new List<DocumentoRelacionadoDte>
                {
                    new()
                    {
                        TipoDocumento = TiposDteInfo.Codigo(d.RelacionadoTipo ?? TipoDte.ComprobanteCreditoFiscal),
                        TipoGeneracion = Catalogos.TipoGeneracionElectronico,
                        NumeroDocumento = relacionado,
                        FechaEmision = d.RelacionadoFechaEmision is { } f ? Fecha(f) : ""
                    }
                },
            Emisor = new EmisorNotaCreditoDte
            {
                Nit = e.Nit,
                Nrc = e.Nrc,
                Nombre = e.Nombre,
                CodActividad = e.CodActividad,
                DescActividad = e.DescActividad,
                NombreComercial = e.NombreComercial,
                TipoEstablecimiento = e.TipoEstablecimiento,
                Direccion = Direccion(e.Departamento, e.Municipio, e.Complemento),
                Telefono = e.Telefono,
                Correo = e.Correo
            },
            Receptor = ReceptorContribuyente(d),
            CuerpoDocumento = d.Detalles.OrderBy(x => x.NumItem).Select(x => new ItemNotaCreditoDte
            {
                NumItem = x.NumItem,
                TipoItem = Catalogos.TipoItemBienes,
                NumeroDocumento = relacionado,
                Codigo = x.Codigo,
                Descripcion = x.Descripcion,
                Cantidad = x.Cantidad,
                UniMedida = x.UnidadMedida,
                PrecioUni = x.PrecioUnitario,
                MontoDescu = x.MontoDescuento,
                VentaGravada = x.VentaGravada,
                Tributos = new List<string> { Catalogos.TributoIva }
            }).ToList(),
            Resumen = new ResumenNotaCreditoDte
            {
                TotalGravada = d.TotalGravada,
                SubTotalVentas = d.TotalGravada,
                TotalDescu = d.TotalDescuento,
                Tributos = new List<TributoResumenDte>
                {
                    new() { Codigo = Catalogos.TributoIva, Descripcion = Catalogos.TributoIvaDescripcion, Valor = d.TotalIva }
                },
                SubTotal = d.SubTotal,
                MontoTotalOperacion = d.MontoTotalOperacion,
                TotalLetras = d.TotalLetras,
                CondicionOperacion = d.CondicionOperacion
            },
            Apendice = Apendice(d)
        };
    }

    /// <summary>
    /// Validación estructural previa a la serialización: campos obligatorios y patrones de los esquemas,
    /// y coherencia aritmética del resumen. No sustituye la validación oficial de Hacienda.
    /// </summary>
    public static ResultadoValidacion Validar(object modelo)
    {
        var r = new ResultadoValidacion();
        switch (modelo)
        {
            case FacturaDte f:
                ValidarIdentificacion(f.Identificacion, "01", r);
                ValidarEmisor(f.Emisor.Nit, f.Emisor.Nrc, f.Emisor.Nombre, f.Emisor.CodActividad, f.Emisor.Direccion, f.Emisor.Correo, r);
                if (string.IsNullOrWhiteSpace(f.Receptor.Nombre)) r.Agregar("receptor.nombre", "Factura: falta el nombre del receptor.");
                ValidarItems(f.CuerpoDocumento.Select(i => (i.NumItem, i.Cantidad, i.VentaGravada)).ToList(), r);
                ValidarSuma(f.Resumen.TotalGravada, f.CuerpoDocumento.Sum(i => i.VentaGravada), "totalGravada", r);
                ValidarSuma(f.Resumen.TotalIva, f.CuerpoDocumento.Sum(i => i.IvaItem), "totalIva", r);
                ValidarSuma(f.Resumen.MontoTotalOperacion, f.Resumen.SubTotal, "montoTotalOperacion", r);
                break;
            case CreditoFiscalDte c:
                ValidarIdentificacion(c.Identificacion, "03", r);
                ValidarEmisor(c.Emisor.Nit, c.Emisor.Nrc, c.Emisor.Nombre, c.Emisor.CodActividad, c.Emisor.Direccion, c.Emisor.Correo, r);
                ValidarReceptorContribuyente(c.Receptor, "CCF", r);
                ValidarItems(c.CuerpoDocumento.Select(i => (i.NumItem, i.Cantidad, i.VentaGravada)).ToList(), r);
                ValidarSuma(c.Resumen.TotalGravada, c.CuerpoDocumento.Sum(i => i.VentaGravada), "totalGravada", r);
                ValidarSuma(c.Resumen.MontoTotalOperacion, c.Resumen.SubTotal + (c.Resumen.Tributos?.Sum(t => t.Valor) ?? 0), "montoTotalOperacion", r);
                break;
            case NotaCreditoDte n:
                ValidarIdentificacion(n.Identificacion, "05", r);
                ValidarEmisor(n.Emisor.Nit, n.Emisor.Nrc, n.Emisor.Nombre, n.Emisor.CodActividad, n.Emisor.Direccion, n.Emisor.Correo, r);
                ValidarReceptorContribuyente(n.Receptor, "Nota de crédito", r);
                if (n.DocumentoRelacionado.Count == 0)
                    r.Agregar("documentoRelacionado", "Nota de crédito: debe indicar el documento relacionado.");
                foreach (var rel in n.DocumentoRelacionado)
                {
                    if (rel.TipoDocumento != "03") r.Agregar("documentoRelacionado", "Nota de crédito: el documento relacionado debe ser un CCF.");
                    if (!CodigoGeneracionRegex().IsMatch(rel.NumeroDocumento)) r.Agregar("documentoRelacionado", "Nota de crédito: el código del documento relacionado no es válido.");
                }
                if (n.CuerpoDocumento.Any(i => n.DocumentoRelacionado.All(rel => rel.NumeroDocumento != i.NumeroDocumento)))
                    r.Agregar("cuerpoDocumento", "Nota de crédito: cada línea debe referirse a un documento relacionado.");
                ValidarItems(n.CuerpoDocumento.Select(i => (i.NumItem, i.Cantidad, i.VentaGravada)).ToList(), r);
                ValidarSuma(n.Resumen.TotalGravada, n.CuerpoDocumento.Sum(i => i.VentaGravada), "totalGravada", r);
                ValidarSuma(n.Resumen.MontoTotalOperacion, n.Resumen.SubTotal + (n.Resumen.Tributos?.Sum(t => t.Valor) ?? 0), "montoTotalOperacion", r);
                break;
            default:
                r.Agregar("", "Modelo de DTE no soportado.");
                break;
        }
        return r;
    }

    private static void ValidarIdentificacion(IdentificacionDte i, string tipo, ResultadoValidacion r)
    {
        if (i.TipoDte != tipo) r.Agregar("identificacion.tipoDte", "Tipo de DTE incorrecto.");
        if (!NumeroControlRegex().IsMatch(i.NumeroControl) || !i.NumeroControl.StartsWith($"DTE-{tipo}-"))
            r.Agregar("identificacion.numeroControl", "El número de control no cumple el formato DTE-TT-XXXXXXXX-NNNNNNNNNNNNNNN.");
        if (!CodigoGeneracionRegex().IsMatch(i.CodigoGeneracion))
            r.Agregar("identificacion.codigoGeneracion", "El código de generación no es un UUID en mayúsculas.");
        if (i.Ambiente is not ("00" or "01")) r.Agregar("identificacion.ambiente", "Ambiente no válido.");
        if (!DateOnly.TryParseExact(i.FecEmi, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            r.Agregar("identificacion.fecEmi", "Fecha de emisión no válida.");
        if (!TimeOnly.TryParseExact(i.HorEmi, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            r.Agregar("identificacion.horEmi", "Hora de emisión no válida.");
    }

    private static void ValidarEmisor(string nit, string nrc, string nombre, string actividad, DireccionDte dir, string correo, ResultadoValidacion r)
    {
        if (!IdentificadoresFiscales.EsNitValido(nit)) r.Agregar("emisor.nit", "El NIT del emisor no es válido. Revise la configuración del emisor.");
        if (!IdentificadoresFiscales.EsNrcValido(nrc)) r.Agregar("emisor.nrc", "El NRC del emisor no es válido. Revise la configuración del emisor.");
        if (string.IsNullOrWhiteSpace(nombre)) r.Agregar("emisor.nombre", "Falta el nombre del emisor.");
        if (!IdentificadoresFiscales.EsActividadValida(actividad)) r.Agregar("emisor.codActividad", "La actividad económica del emisor no es válida.");
        if (!IdentificadoresFiscales.EsDepartamentoValido(dir.Departamento) || !IdentificadoresFiscales.EsMunicipioValido(dir.Municipio) || string.IsNullOrWhiteSpace(dir.Complemento))
            r.Agregar("emisor.direccion", "La dirección del emisor está incompleta.");
        if (!IdentificadoresFiscales.EsCorreoValido(correo)) r.Agregar("emisor.correo", "El correo del emisor no es válido.");
    }

    private static void ValidarReceptorContribuyente(ReceptorContribuyenteDte rc, string doc, ResultadoValidacion r)
    {
        if (!IdentificadoresFiscales.EsNitValido(rc.Nit)) r.Agregar("receptor.nit", $"{doc}: el NIT del receptor no es válido.");
        if (!IdentificadoresFiscales.EsNrcValido(rc.Nrc)) r.Agregar("receptor.nrc", $"{doc}: el NRC del receptor no es válido.");
        if (string.IsNullOrWhiteSpace(rc.Nombre)) r.Agregar("receptor.nombre", $"{doc}: falta el nombre del receptor.");
        if (!IdentificadoresFiscales.EsActividadValida(rc.CodActividad) || string.IsNullOrWhiteSpace(rc.DescActividad))
            r.Agregar("receptor.codActividad", $"{doc}: falta la actividad económica del receptor.");
        if (!IdentificadoresFiscales.EsDepartamentoValido(rc.Direccion.Departamento) || !IdentificadoresFiscales.EsMunicipioValido(rc.Direccion.Municipio) || string.IsNullOrWhiteSpace(rc.Direccion.Complemento))
            r.Agregar("receptor.direccion", $"{doc}: la dirección del receptor está incompleta.");
        if (!IdentificadoresFiscales.EsCorreoValido(rc.Correo)) r.Agregar("receptor.correo", $"{doc}: el correo del receptor no es válido.");
    }

    private static void ValidarItems(List<(int NumItem, decimal Cantidad, decimal VentaGravada)> items, ResultadoValidacion r)
    {
        if (items.Count == 0) r.Agregar("cuerpoDocumento", "El documento no tiene líneas.");
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].NumItem != i + 1) r.Agregar("cuerpoDocumento", "La numeración de líneas no es correlativa.");
            if (items[i].Cantidad <= 0) r.Agregar("cuerpoDocumento", $"Línea {i + 1}: cantidad no válida.");
            if (items[i].VentaGravada < 0) r.Agregar("cuerpoDocumento", $"Línea {i + 1}: importe negativo.");
        }
    }

    private static void ValidarSuma(decimal declarado, decimal calculado, string campo, ResultadoValidacion r)
    {
        if (declarado != calculado) r.Agregar($"resumen.{campo}", $"El resumen no cuadra en {campo} ({declarado} ≠ {calculado}).");
    }
}
