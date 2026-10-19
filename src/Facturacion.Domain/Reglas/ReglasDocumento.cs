using Facturacion.Domain.Entidades;

namespace Facturacion.Domain.Reglas;

/// <summary>Validaciones específicas de cada tipo de DTE sobre el receptor y los documentos relacionados.</summary>
public static class ReglasDocumento
{
    /// <summary>
    /// Valida que el cliente pueda ser receptor del tipo de documento.
    ///  * Factura: cualquier cliente activo. Si el monto total alcanza el umbral configurado, se exige
    ///    documento de identificación del receptor.
    ///  * CCF y nota de crédito: el receptor debe ser contribuyente con NIT, NRC, actividad, dirección y correo.
    /// </summary>
    public static ResultadoValidacion ValidarReceptor(TipoDte tipo, Cliente cliente, decimal montoTotal, decimal umbralIdentificacionFactura)
    {
        var r = new ResultadoValidacion();
        if (!cliente.Activo) r.Agregar("ClienteId", "El cliente está desactivado; actívelo o seleccione otro.");

        switch (tipo)
        {
            case TipoDte.FacturaConsumidorFinal:
                if (montoTotal >= umbralIdentificacionFactura && (cliente.TipoDocumento is null || cliente.NumeroDocumento is null))
                    r.Agregar("ClienteId", $"Para facturas de {umbralIdentificacionFactura:C} o más se requiere el documento de identificación del cliente.");
                break;

            case TipoDte.ComprobanteCreditoFiscal:
            case TipoDte.NotaCredito:
                if (cliente.Tipo != TipoCliente.Contribuyente)
                {
                    r.Agregar("ClienteId", $"El {TiposDteInfo.NombreCorto(tipo)} solo puede emitirse a un cliente contribuyente (con NIT y NRC).");
                }
                else
                {
                    var datos = ReglasCliente.Validar(cliente);
                    foreach (var e in datos.Errores)
                        r.Agregar("ClienteId", $"Datos del cliente incompletos: {e.Mensaje}");
                }
                break;

            default:
                r.Agregar("Tipo", "Tipo de documento no soportado.");
                break;
        }
        return r;
    }

    /// <summary>La nota de crédito solo puede relacionarse con un CCF emitido electrónicamente y vigente.</summary>
    public static ResultadoValidacion ValidarDocumentoBaseNotaCredito(DocumentoTributario? relacionado, bool tieneAnulacionEnCurso)
    {
        var r = new ResultadoValidacion();
        if (relacionado is null)
        {
            r.Agregar("DocumentoRelacionadoId", "Seleccione el comprobante de crédito fiscal que se acredita.");
            return r;
        }
        if (relacionado.Tipo != TipoDte.ComprobanteCreditoFiscal)
            r.Agregar("DocumentoRelacionadoId", "La nota de crédito solo puede relacionarse con un comprobante de crédito fiscal.");
        if (relacionado.Estado != EstadoDocumento.Emitido)
            r.Agregar("DocumentoRelacionadoId", "El documento relacionado está anulado; no admite notas de crédito.");
        if (tieneAnulacionEnCurso)
            r.Agregar("DocumentoRelacionadoId", "El documento relacionado tiene una anulación en curso.");
        return r;
    }

    /// <summary>
    /// Valida las cantidades a acreditar contra las del CCF original menos lo ya acreditado por otras
    /// notas de crédito vigentes.
    /// </summary>
    public static ResultadoValidacion ValidarCantidadesNotaCredito(
        IReadOnlyList<(DetalleDocumento Linea, decimal YaAcreditado, decimal Solicitado)> lineas)
    {
        var r = new ResultadoValidacion();
        if (!lineas.Any(l => l.Solicitado > 0))
            r.Agregar("Lineas", "Indique la cantidad a acreditar en al menos una línea.");
        foreach (var (linea, ya, solicitado) in lineas)
        {
            if (solicitado < 0) r.Agregar("Lineas", $"Línea {linea.NumItem}: la cantidad no puede ser negativa.");
            var disponible = linea.Cantidad - ya;
            if (solicitado > disponible)
                r.Agregar("Lineas", $"Línea {linea.NumItem} ({linea.Descripcion}): solo quedan {disponible:0.####} unidades por acreditar.");
        }
        return r;
    }
}
