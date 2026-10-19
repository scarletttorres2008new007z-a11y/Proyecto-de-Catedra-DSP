using System.Globalization;
using Facturacion.Application;
using Facturacion.Application.Abstracciones;
using Facturacion.Application.Dte;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Facturacion.Infrastructure.Pdf;

/// <summary>
/// Representación gráfica del documento con QuestPDF (licencia Community: uso gratuito para proyectos
/// académicos, personas y organizaciones con ingresos anuales menores a 1 millón de USD).
/// </summary>
public sealed class GeneradorPdfQuest : IGeneradorPdf
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-SV");
    private const string ColorPrimario = "#1F3A5F";
    private const string ColorBorde = "#C9D2DC";
    private const string ColorFondo = "#F3F6F9";

    static GeneradorPdfQuest()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Version => "pdf-1";

    public byte[] Generar(DocumentoTributario d, EmisorSnapshot e)
    {
        var codigo = GeneradorDteJson.Codigo(d.CodigoGeneracion);
        var academico = d.Modo == ModoOperacion.Academico;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(8.5f).FontColor("#1F2933"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text(e.NombreComercial ?? e.Nombre).FontSize(14).Bold().FontColor(ColorPrimario);
                            if (e.NombreComercial is not null) c.Item().Text(e.Nombre).SemiBold();
                            c.Item().Text($"NIT: {e.Nit}    NRC: {e.Nrc}");
                            c.Item().Text($"Actividad: {e.CodActividad} - {e.DescActividad}");
                            c.Item().Text($"{e.Complemento}, {Catalogos.NombreDepartamento(e.Departamento)} (municipio {e.Municipio})");
                            c.Item().Text($"Tel.: {e.Telefono}    Correo: {e.Correo}");
                        });
                        row.ConstantItem(12);
                        row.RelativeItem(2).Border(1).BorderColor(ColorPrimario).Padding(6).Column(c =>
                        {
                            c.Item().AlignCenter().Text("DOCUMENTO TRIBUTARIO ELECTRÓNICO").FontSize(7.5f).SemiBold();
                            c.Item().AlignCenter().Text(TiposDteInfo.Nombre(d.Tipo).ToUpper(Cultura)).FontSize(10).Bold().FontColor(ColorPrimario);
                            c.Item().PaddingTop(4).Text(t => { t.Span("Código de generación: ").SemiBold(); t.Span(codigo); });
                            c.Item().Text(t => { t.Span("Número de control: ").SemiBold(); t.Span(d.NumeroControl); });
                            c.Item().Text(t => { t.Span("Fecha y hora: ").SemiBold(); t.Span($"{d.FechaEmision:dd/MM/yyyy} {d.HoraEmision:HH:mm:ss}"); });
                            c.Item().Text(t => { t.Span("Modelo: ").SemiBold(); t.Span("Previo · Transmisión normal"); });
                            c.Item().Text(t => { t.Span("Versión del esquema: ").SemiBold(); t.Span(d.VersionEsquema.ToString()); });
                            c.Item().Text(t =>
                            {
                                t.Span("Estado: ").SemiBold();
                                t.Span(academico ? "Generado (no transmitido a Hacienda)" : d.Estado.ToString());
                            });
                            c.Item().Text(t => { t.Span("Sello de recepción: ").SemiBold(); t.Span(academico ? "No existe (simulación)" : "-"); });
                        });
                    });

                    if (academico)
                    {
                        col.Item().PaddingTop(8).Background("#FFF4E5").Border(1).BorderColor("#E8A33D").Padding(6).Text(t =>
                        {
                            t.Span("SIMULACIÓN ACADÉMICA. ").Bold().FontColor("#8A4B00");
                            t.Span("Este documento fue generado con fines de demostración. No ha sido transmitido, validado ni aceptado por el Ministerio de Hacienda de El Salvador y no tiene validez tributaria.")
                                .FontColor("#8A4B00");
                        });
                    }
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Element(c => Receptor(c, d));
                    if (d.Tipo == TipoDte.NotaCredito && d.RelacionadoCodigoGeneracion is { } rel)
                    {
                        col.Item().Element(c => Seccion(c, "Documento relacionado", s =>
                        {
                            s.Item().Text($"Comprobante de crédito fiscal electrónico · Código de generación {GeneradorDteJson.Codigo(rel)}");
                            s.Item().Text($"Fecha de emisión: {d.RelacionadoFechaEmision:dd/MM/yyyy}");
                        }));
                    }
                    col.Item().Element(c => Detalle(c, d));
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Total en letras: ").SemiBold(); t.Span(d.TotalLetras); });
                            c.Item().Text(t =>
                            {
                                t.Span("Condición de la operación: ").SemiBold();
                                t.Span("Contado");
                                if (d.Tipo != TipoDte.NotaCredito && d.FormaPago is not null)
                                    t.Span($" · Forma de pago: {(Catalogos.FormasPago.TryGetValue(d.FormaPago, out var fp) ? fp : d.FormaPago)}");
                            });
                            if (d.Observaciones is not null)
                                c.Item().Text(t => { t.Span("Observaciones: ").SemiBold(); t.Span(d.Observaciones); });
                            c.Item().PaddingTop(4).Text(
                                TiposDteInfo.PrecioIncluyeIva(d.Tipo)
                                    ? "Precios con IVA incluido."
                                    : "Precios sin IVA. El IVA (13 %) se detalla en el resumen.").Italic().FontColor("#52606D");
                        });
                        row.ConstantItem(12);
                        row.RelativeItem(2).Element(c => Totales(c, d));
                    });
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Representación gráfica del DTE {codigo}. Archivo JSON asociado: {codigo}.json")
                        .FontSize(7).FontColor("#7B8794");
                    row.ConstantItem(80).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7).FontColor("#7B8794"));
                        t.Span("Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Seccion(IContainer c, string titulo, Action<ColumnDescriptor> contenido) =>
        c.Border(1).BorderColor(ColorBorde).Column(col =>
        {
            col.Item().Background(ColorFondo).PaddingVertical(3).PaddingHorizontal(6).Text(titulo).SemiBold().FontColor(ColorPrimario);
            col.Item().Padding(6).Column(contenido);
        });

    private static void Receptor(IContainer c, DocumentoTributario d) => Seccion(c, "Receptor", s =>
    {
        s.Item().Text(t => { t.Span("Nombre o razón social: ").SemiBold(); t.Span(d.ReceptorNombre); });
        if (d.ReceptorNombreComercial is not null)
            s.Item().Text(t => { t.Span("Nombre comercial: ").SemiBold(); t.Span(d.ReceptorNombreComercial); });
        if (d.Tipo == TipoDte.FacturaConsumidorFinal)
        {
            if (d.ReceptorNumDocumento is not null)
                s.Item().Text(t => { t.Span($"{Catalogos.NombreTipoDocumento(d.ReceptorTipoDocumento)}: ").SemiBold(); t.Span(d.ReceptorNumDocumento); });
            if (d.ReceptorNrc is not null && d.ReceptorTipoDocumento == Catalogos.DocNit)
                s.Item().Text(t => { t.Span("NRC: ").SemiBold(); t.Span(d.ReceptorNrc); });
        }
        else
        {
            s.Item().Text(t => { t.Span("NIT: ").SemiBold(); t.Span(d.ReceptorNumDocumento ?? ""); t.Span("    NRC: ").SemiBold(); t.Span(d.ReceptorNrc ?? ""); });
            s.Item().Text(t => { t.Span("Actividad económica: ").SemiBold(); t.Span($"{d.ReceptorCodActividad} - {d.ReceptorDescActividad}"); });
        }
        if (d.ReceptorComplemento is not null)
            s.Item().Text(t =>
            {
                t.Span("Dirección: ").SemiBold();
                t.Span($"{d.ReceptorComplemento}, {Catalogos.NombreDepartamento(d.ReceptorDepartamento)} (municipio {d.ReceptorMunicipio})");
            });
        if (d.ReceptorCorreo is not null || d.ReceptorTelefono is not null)
            s.Item().Text(t =>
            {
                if (d.ReceptorCorreo is not null) { t.Span("Correo: ").SemiBold(); t.Span(d.ReceptorCorreo + "    "); }
                if (d.ReceptorTelefono is not null) { t.Span("Teléfono: ").SemiBold(); t.Span(d.ReceptorTelefono); }
            });
    });

    private static void Detalle(IContainer c, DocumentoTributario d) => c.Table(t =>
    {
        t.ColumnsDefinition(cols =>
        {
            cols.ConstantColumn(22);
            cols.ConstantColumn(48);
            cols.ConstantColumn(48);
            cols.ConstantColumn(58);
            cols.RelativeColumn();
            cols.ConstantColumn(58);
            cols.ConstantColumn(52);
            cols.ConstantColumn(62);
        });
        t.Header(h =>
        {
            foreach (var titulo in new[] { "N.°", "Cantidad", "Unidad", "Código", "Descripción", "Precio unit.", "Descuento", "Ventas gravadas" })
                h.Cell().Background(ColorPrimario).Padding(3).Text(titulo).FontColor(Colors.White).SemiBold().FontSize(7.5f);
        });
        foreach (var l in d.Detalles.OrderBy(x => x.NumItem))
        {
            Celda(t, l.NumItem.ToString());
            Celda(t, l.Cantidad.ToString("0.####", Cultura), true);
            Celda(t, Catalogos.NombreUnidad(l.UnidadMedida));
            Celda(t, l.Codigo);
            Celda(t, l.Descripcion);
            Celda(t, Moneda(l.PrecioUnitario, 4), true);
            Celda(t, Moneda(l.MontoDescuento), true);
            Celda(t, Moneda(l.VentaGravada), true);
        }
    });

    private static void Celda(TableDescriptor t, string texto, bool derecha = false)
    {
        var celda = t.Cell().BorderBottom(0.5f).BorderColor(ColorBorde).Padding(3);
        (derecha ? celda.AlignRight() : celda).Text(texto);
    }

    private static void Totales(IContainer c, DocumentoTributario d) => c.Border(1).BorderColor(ColorBorde).Padding(6).Column(col =>
    {
        void Fila(string etiqueta, decimal valor, bool destacado = false) => col.Item().Row(r =>
        {
            var e = r.RelativeItem().Text(etiqueta);
            var v = r.ConstantItem(80).AlignRight().Text(Moneda(valor));
            if (destacado) { e.Bold(); v.Bold(); }
        });

        Fila("Suma de ventas gravadas", d.TotalGravada);
        Fila("Total descuentos (informativo)", d.TotalDescuento);
        if (TiposDteInfo.PrecioIncluyeIva(d.Tipo))
        {
            Fila("Subtotal", d.SubTotal);
            Fila("IVA incluido (13 %)", d.TotalIva);
            Fila("Total a pagar", d.TotalPagar, true);
        }
        else
        {
            Fila("Subtotal", d.SubTotal);
            Fila("IVA 13 %", d.TotalIva);
            Fila("Monto total de la operación", d.MontoTotalOperacion, d.Tipo == TipoDte.NotaCredito);
            if (d.Tipo != TipoDte.NotaCredito) Fila("Total a pagar", d.TotalPagar, true);
        }
    });

    private static string Moneda(decimal v, int decimales = 2) =>
        "$" + v.ToString(decimales == 2 ? "#,##0.00" : "#,##0.00##", Cultura);
}
