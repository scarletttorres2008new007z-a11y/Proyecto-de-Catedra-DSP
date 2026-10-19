namespace Facturacion.Domain.Entidades;

public class Cliente
{
    public int Id { get; set; }
    public TipoCliente Tipo { get; set; }

    /// <summary>Nombre, denominación o razón social.</summary>
    public string Nombre { get; set; } = "";
    public string? NombreComercial { get; set; }

    /// <summary>Código CAT-022 (36 = NIT, 13 = DUI, 03 = pasaporte...). En contribuyentes siempre es NIT.</summary>
    public string? TipoDocumento { get; set; }
    public string? NumeroDocumento { get; set; }

    /// <summary>Número de registro de contribuyente (solo dígitos).</summary>
    public string? Nrc { get; set; }
    public string? CodActividad { get; set; }
    public string? DescActividad { get; set; }

    public string? Departamento { get; set; }
    public string? Municipio { get; set; }
    public string? Complemento { get; set; }

    public string? Telefono { get; set; }
    public string? Correo { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime CreadoUtc { get; set; }
    public DateTime? ModificadoUtc { get; set; }

    /// <summary>Token de concurrencia optimista.</summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<DocumentoTributario> Documentos { get; set; } = new List<DocumentoTributario>();
}
