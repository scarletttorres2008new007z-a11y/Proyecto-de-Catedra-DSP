using System.Text.Json;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;

namespace Facturacion.Application.Dte;

/// <summary>Construye el JSON del evento de invalidación (estructura de anulación v2).</summary>
public static class GeneradorAnulacionJson
{
    public const int Version = 2;

    public static AnulacionDte ConstruirModelo(Anulacion a, DocumentoTributario doc, EmisorSnapshot emisor, DateTime fechaLocal) => new()
    {
        Identificacion = new IdentificacionAnulacionDte
        {
            Version = Version,
            Ambiente = doc.Ambiente,
            CodigoGeneracion = GeneradorDteJson.Codigo(a.CodigoGeneracion),
            FecAnula = GeneradorDteJson.Fecha(DateOnly.FromDateTime(fechaLocal)),
            HorAnula = GeneradorDteJson.Hora(TimeOnly.FromDateTime(fechaLocal))
        },
        Emisor = new EmisorAnulacionDte
        {
            Nit = emisor.Nit,
            Nombre = emisor.Nombre,
            TipoEstablecimiento = emisor.TipoEstablecimiento,
            NomEstablecimiento = emisor.NombreComercial,
            CodEstable = emisor.CodEstable,
            CodPuntoVenta = emisor.CodPuntoVenta,
            Telefono = emisor.Telefono,
            Correo = emisor.Correo
        },
        Documento = new DocumentoAnuladoDte
        {
            TipoDte = TiposDteInfo.Codigo(doc.Tipo),
            CodigoGeneracion = GeneradorDteJson.Codigo(doc.CodigoGeneracion),
            SelloRecibido = null,
            NumeroControl = doc.NumeroControl,
            FecEmi = GeneradorDteJson.Fecha(doc.FechaEmision),
            MontoIva = doc.TotalIva,
            CodigoGeneracionR = a.CodigoGeneracionReemplazo is { } r ? GeneradorDteJson.Codigo(r) : null,
            TipoDocumento = doc.ReceptorTipoDocumento,
            NumDocumento = doc.ReceptorNumDocumento,
            Nombre = doc.ReceptorNombre,
            Telefono = doc.ReceptorTelefono,
            Correo = doc.ReceptorCorreo
        },
        Motivo = new MotivoAnulacionDte
        {
            TipoAnulacion = (int)a.Tipo,
            MotivoAnulacion = a.Motivo,
            NombreResponsable = a.NombreResponsable,
            TipDocResponsable = a.TipoDocResponsable,
            NumDocResponsable = a.NumDocResponsable,
            NombreSolicita = a.NombreSolicita,
            TipDocSolicita = a.TipoDocSolicita,
            NumDocSolicita = a.NumDocSolicita
        }
    };

    public static string Generar(Anulacion a, DocumentoTributario doc, EmisorSnapshot emisor, DateTime fechaLocal) =>
        JsonSerializer.Serialize(ConstruirModelo(a, doc, emisor, fechaLocal), GeneradorDteJson.Opciones);
}
