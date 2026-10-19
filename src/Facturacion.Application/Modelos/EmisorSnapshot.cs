using System.Text.Json;
using Facturacion.Domain.Entidades;

namespace Facturacion.Application;

/// <summary>Copia inmutable de los datos del emisor guardada con cada documento.</summary>
public sealed record EmisorSnapshot(
    string Nit,
    string Nrc,
    string Nombre,
    string CodActividad,
    string DescActividad,
    string? NombreComercial,
    string TipoEstablecimiento,
    string Departamento,
    string Municipio,
    string Complemento,
    string Telefono,
    string Correo,
    string CodEstable,
    string CodPuntoVenta)
{
    public static EmisorSnapshot Desde(ConfiguracionEmisor c) => new(
        c.Nit, c.Nrc, c.Nombre, c.CodActividad, c.DescActividad, c.NombreComercial, c.TipoEstablecimiento,
        c.Departamento, c.Municipio, c.Complemento, c.Telefono, c.Correo, c.CodEstable, c.CodPuntoVenta);

    public string Serializar() => JsonSerializer.Serialize(this);

    public static EmisorSnapshot Deserializar(string json) =>
        JsonSerializer.Deserialize<EmisorSnapshot>(json) ?? throw new InvalidOperationException("Snapshot del emisor inválido.");
}
