using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;

namespace Facturacion.Tests.Soporte;

/// <summary>
/// Datos SINTÉTICOS para pruebas. Ningún nombre, NIT, NRC, DUI ni correo corresponde a una persona o empresa
/// real: los nombres llevan el prefijo "PRUEBA" y los correos usan el dominio reservado ejemplo.test.
/// </summary>
public static class DatosSinteticos
{
    public static ConfiguracionEmisor Emisor() => new()
    {
        Nit = "00000000000017",
        Nrc = "100001",
        Nombre = "PRUEBA EMISOR ACADÉMICO, S.A. DE C.V.",
        NombreComercial = "PRUEBA Comercial",
        CodActividad = "46900",
        DescActividad = "Venta al por mayor de otros productos",
        TipoEstablecimiento = "02",
        Departamento = "06",
        Municipio = "14",
        Complemento = "Calle de prueba 123, San Salvador",
        Telefono = "2200-0000",
        Correo = "emisor@ejemplo.test",
        CodEstable = "M001",
        CodPuntoVenta = "P001",
        ModificadoPor = "pruebas",
    };

    public static Cliente ConsumidorFinal(string? correo = "consumidor@ejemplo.test") => new()
    {
        Tipo = TipoCliente.ConsumidorFinal,
        Nombre = "PRUEBA Consumidor Final",
        TipoDocumento = Catalogos.DocDui,
        NumeroDocumento = "00000000-0",
        Correo = correo,
    };

    public static Cliente Contribuyente(string nit = "00000000000025", string? correo = "contribuyente@ejemplo.test") => new()
    {
        Tipo = TipoCliente.Contribuyente,
        Nombre = "PRUEBA Contribuyente, S.A. de C.V.",
        NombreComercial = "PRUEBA Contribuyente",
        TipoDocumento = Catalogos.DocNit,
        NumeroDocumento = nit,
        Nrc = "100002",
        CodActividad = "47190",
        DescActividad = "Venta al por menor en comercios no especializados",
        Departamento = "05",
        Municipio = "01",
        Complemento = "Avenida de prueba 45, Santa Tecla",
        Telefono = "2222-0000",
        Correo = correo,
    };

    public static Producto Producto(string codigo = "PRB-001", decimal precio = 10m, bool inventario = false, decimal existencias = 0m) => new()
    {
        Codigo = codigo,
        Nombre = $"PRUEBA Producto {codigo}",
        PrecioSinIva = precio,
        UnidadMedida = 59,
        ControlaInventario = inventario,
        Existencias = existencias,
    };

    public static SolicitudEmision Venta(TipoDte tipo, int clienteId, params (int ProductoId, decimal Cantidad, decimal Descuento)[] lineas) => new()
    {
        Tipo = tipo,
        ClienteId = clienteId,
        FormaPago = "01",
        Lineas = lineas.Select(l => new LineaSolicitud { ProductoId = l.ProductoId, Cantidad = l.Cantidad, Descuento = l.Descuento }).ToList(),
    };

    public static SolicitudAnulacion Anulacion(int documentoId, TipoAnulacion tipo = TipoAnulacion.RescindirOperacion, int? reemplazo = null) => new()
    {
        DocumentoId = documentoId,
        Tipo = tipo,
        Motivo = "PRUEBA: operación rescindida por el cliente",
        DocumentoReemplazoId = reemplazo,
        NombreResponsable = "PRUEBA Responsable",
        TipoDocResponsable = Catalogos.DocDui,
        NumDocResponsable = "000000001",
        NombreSolicita = "PRUEBA Solicitante",
        TipoDocSolicita = Catalogos.DocDui,
        NumDocSolicita = "00000002-0",
    };
}
