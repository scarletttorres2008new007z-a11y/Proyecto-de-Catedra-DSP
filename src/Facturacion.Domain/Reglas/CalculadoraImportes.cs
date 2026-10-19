namespace Facturacion.Domain.Reglas;

/// <summary>Línea de entrada para el cálculo. El precio base siempre es SIN IVA.</summary>
public sealed record LineaEntrada(decimal Cantidad, decimal PrecioSinIva, decimal Descuento);

public sealed record LineaCalculada(
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal MontoDescuento,
    decimal VentaGravada,
    decimal IvaItem);

public sealed record ResumenCalculado(
    IReadOnlyList<LineaCalculada> Lineas,
    decimal TotalGravada,
    decimal TotalDescuento,
    decimal SubTotal,
    decimal TotalIva,
    decimal MontoTotalOperacion,
    decimal TotalPagar);

/// <summary>
/// Única fuente de verdad para subtotales, descuentos, IVA y totales. La usan la vista previa,
/// la emisión y las pruebas; el navegador nunca define importes.
///
/// Reglas:
///  * Factura (01): el precio unitario del documento incluye IVA = redondear(precioSinIva × 1.13).
///    ventaGravada = redondear(cantidad × precioUnitario) − descuento. El IVA de la línea es
///    redondear(ventaGravada × 0.13 / 1.13) y totalIva es la suma de los IVA por línea.
///    El total a pagar es la suma de ventas gravadas (el IVA ya está incluido).
///  * CCF (03) y nota de crédito (05): el precio unitario no incluye IVA.
///    ventaGravada = redondear(cantidad × precio) − descuento. IVA = redondear(totalGravada × 0.13)
///    y se informa como tributo "20" en el resumen. Total = totalGravada + IVA.
///  * El descuento se expresa en la misma base que el precio del documento y no puede superar el importe bruto.
/// </summary>
public static class CalculadoraImportes
{
    public const int MaxDecimalesCantidad = 4;
    public const decimal CantidadMaxima = 1_000_000m;
    public const int MaxLineas = 2000;

    public static decimal PrecioDocumento(TipoDte tipo, decimal precioSinIva) =>
        TiposDteInfo.PrecioIncluyeIva(tipo) ? Dinero.Redondear(precioSinIva * (1 + Dinero.TasaIva)) : precioSinIva;

    public static ResultadoValidacion ValidarLineas(IReadOnlyList<LineaEntrada> lineas)
    {
        var r = new ResultadoValidacion();
        if (lineas.Count == 0) r.Agregar("Lineas", "Agregue al menos un producto al documento.");
        if (lineas.Count > MaxLineas) r.Agregar("Lineas", $"El documento admite como máximo {MaxLineas} líneas.");

        for (var i = 0; i < lineas.Count; i++)
        {
            var l = lineas[i];
            var n = i + 1;
            if (l.Cantidad <= 0) r.Agregar($"Lineas[{i}].Cantidad", $"Línea {n}: la cantidad debe ser mayor que cero.");
            else if (l.Cantidad > CantidadMaxima) r.Agregar($"Lineas[{i}].Cantidad", $"Línea {n}: la cantidad supera el máximo permitido.");
            else if (Dinero.Decimales(l.Cantidad) > MaxDecimalesCantidad)
                r.Agregar($"Lineas[{i}].Cantidad", $"Línea {n}: la cantidad admite como máximo {MaxDecimalesCantidad} decimales.");
            if (l.PrecioSinIva <= 0) r.Agregar($"Lineas[{i}].Precio", $"Línea {n}: el precio debe ser mayor que cero.");
            if (l.Descuento < 0) r.Agregar($"Lineas[{i}].Descuento", $"Línea {n}: el descuento no puede ser negativo.");
            else if (Dinero.Decimales(l.Descuento) > 2) r.Agregar($"Lineas[{i}].Descuento", $"Línea {n}: el descuento admite 2 decimales.");
        }
        return r;
    }

    public static ResumenCalculado Calcular(TipoDte tipo, IReadOnlyList<LineaEntrada> lineas)
    {
        var validacion = ValidarLineas(lineas);
        var calculadas = new List<LineaCalculada>(lineas.Count);

        for (var i = 0; i < lineas.Count; i++)
        {
            var l = lineas[i];
            var precio = PrecioDocumento(tipo, l.PrecioSinIva);
            var bruto = Dinero.Redondear(l.Cantidad * precio);
            if (l.Descuento > bruto)
                validacion.Agregar($"Lineas[{i}].Descuento", $"Línea {i + 1}: el descuento no puede superar el importe de la línea ({bruto:0.00}).");
            var gravada = bruto - l.Descuento;
            var ivaItem = TiposDteInfo.PrecioIncluyeIva(tipo)
                ? Dinero.Redondear(gravada * Dinero.TasaIva / (1 + Dinero.TasaIva))
                : 0m;
            calculadas.Add(new LineaCalculada(l.Cantidad, precio, l.Descuento, gravada, ivaItem));
        }

        if (!validacion.EsValido) throw new ReglaNegocioException(validacion);

        var totalGravada = calculadas.Sum(x => x.VentaGravada);
        var totalDescuento = calculadas.Sum(x => x.MontoDescuento);
        var subTotal = totalGravada;

        decimal iva, montoTotal;
        if (TiposDteInfo.PrecioIncluyeIva(tipo))
        {
            iva = calculadas.Sum(x => x.IvaItem);
            montoTotal = subTotal;
        }
        else
        {
            iva = Dinero.Redondear(totalGravada * Dinero.TasaIva);
            montoTotal = subTotal + iva;
        }

        // Sin retenciones ni percepciones en el alcance del proyecto: total a pagar = monto total de la operación.
        return new ResumenCalculado(calculadas, totalGravada, totalDescuento, subTotal, iva, montoTotal, montoTotal);
    }
}
