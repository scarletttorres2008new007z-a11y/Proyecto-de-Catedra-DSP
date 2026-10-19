using System.ComponentModel.DataAnnotations;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;

namespace Facturacion.Web.Models;

/// <summary>Formulario de cliente (evita la asignación masiva de campos internos de la entidad).</summary>
public sealed class ClienteFormulario
{
    public int Id { get; set; }
    public Guid Version { get; set; }

    [Display(Name = "Tipo de cliente")]
    public TipoCliente Tipo { get; set; } = TipoCliente.ConsumidorFinal;

    [Display(Name = "Nombre o razón social")]
    [StringLength(250)]
    public string? Nombre { get; set; }

    [Display(Name = "Nombre comercial")]
    [StringLength(150)]
    public string? NombreComercial { get; set; }

    [Display(Name = "Tipo de documento")]
    public string? TipoDocumento { get; set; }

    [Display(Name = "Número de documento")]
    [StringLength(25)]
    public string? NumeroDocumento { get; set; }

    [Display(Name = "NRC")]
    [StringLength(10)]
    public string? Nrc { get; set; }

    [Display(Name = "Código de actividad económica")]
    [StringLength(6)]
    public string? CodActividad { get; set; }

    [Display(Name = "Actividad económica")]
    [StringLength(150)]
    public string? DescActividad { get; set; }

    [Display(Name = "Departamento")]
    public string? Departamento { get; set; }

    [Display(Name = "Municipio (código CAT-013)")]
    [StringLength(2)]
    public string? Municipio { get; set; }

    [Display(Name = "Dirección (complemento)")]
    [StringLength(200)]
    public string? Complemento { get; set; }

    [Display(Name = "Teléfono")]
    [StringLength(30)]
    public string? Telefono { get; set; }

    [Display(Name = "Correo electrónico")]
    [StringLength(100)]
    public string? Correo { get; set; }

    public string? VolverA { get; set; }

    public Cliente AEntidad() => new()
    {
        Id = Id, Version = Version, Tipo = Tipo, Nombre = Nombre ?? "", NombreComercial = NombreComercial,
        TipoDocumento = TipoDocumento, NumeroDocumento = NumeroDocumento, Nrc = Nrc, CodActividad = CodActividad,
        DescActividad = DescActividad, Departamento = Departamento, Municipio = Municipio, Complemento = Complemento,
        Telefono = Telefono, Correo = Correo
    };

    public static ClienteFormulario Desde(Cliente c) => new()
    {
        Id = c.Id, Version = c.Version, Tipo = c.Tipo, Nombre = c.Nombre, NombreComercial = c.NombreComercial,
        TipoDocumento = c.TipoDocumento, NumeroDocumento = c.NumeroDocumento, Nrc = c.Nrc, CodActividad = c.CodActividad,
        DescActividad = c.DescActividad, Departamento = c.Departamento, Municipio = c.Municipio, Complemento = c.Complemento,
        Telefono = c.Telefono, Correo = c.Correo
    };
}

public sealed class ProductoFormulario
{
    public int Id { get; set; }
    public Guid Version { get; set; }

    [Display(Name = "Código")]
    [StringLength(25)]
    public string? Codigo { get; set; }

    [Display(Name = "Nombre")]
    [StringLength(200)]
    public string? Nombre { get; set; }

    [Display(Name = "Descripción (opcional)")]
    [StringLength(800)]
    public string? Descripcion { get; set; }

    [Display(Name = "Precio de venta sin IVA (US$)")]
    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "Ingrese un precio válido.")]
    public decimal PrecioSinIva { get; set; }

    [Display(Name = "Unidad de medida")]
    public int UnidadMedida { get; set; } = 59;

    [Display(Name = "Controlar inventario")]
    public bool ControlaInventario { get; set; }

    [Display(Name = "Existencias")]
    public decimal Existencias { get; set; }

    public Producto AEntidad() => new()
    {
        Id = Id, Version = Version, Codigo = Codigo ?? "", Nombre = Nombre ?? "", Descripcion = Descripcion,
        PrecioSinIva = PrecioSinIva, UnidadMedida = UnidadMedida, ControlaInventario = ControlaInventario, Existencias = Existencias
    };

    public static ProductoFormulario Desde(Producto p) => new()
    {
        Id = p.Id, Version = p.Version, Codigo = p.Codigo, Nombre = p.Nombre, Descripcion = p.Descripcion,
        PrecioSinIva = p.PrecioSinIva, UnidadMedida = p.UnidadMedida, ControlaInventario = p.ControlaInventario, Existencias = p.Existencias
    };
}

public sealed class EmisorFormulario
{
    [Display(Name = "NIT (sin guiones)")] public string? Nit { get; set; }
    [Display(Name = "NRC")] public string? Nrc { get; set; }
    [Display(Name = "Nombre o razón social")] public string? Nombre { get; set; }
    [Display(Name = "Código de actividad económica")] public string? CodActividad { get; set; }
    [Display(Name = "Actividad económica")] public string? DescActividad { get; set; }
    [Display(Name = "Nombre comercial")] public string? NombreComercial { get; set; }
    [Display(Name = "Tipo de establecimiento")] public string TipoEstablecimiento { get; set; } = "02";
    [Display(Name = "Departamento")] public string? Departamento { get; set; }
    [Display(Name = "Municipio (código CAT-013)")] public string? Municipio { get; set; }
    [Display(Name = "Dirección (complemento)")] public string? Complemento { get; set; }
    [Display(Name = "Teléfono")] public string? Telefono { get; set; }
    [Display(Name = "Correo electrónico")] public string? Correo { get; set; }
    [Display(Name = "Código de establecimiento (4 caracteres)")] public string CodEstable { get; set; } = "M001";
    [Display(Name = "Código de punto de venta (4 caracteres)")] public string CodPuntoVenta { get; set; } = "P001";

    public ConfiguracionEmisor AEntidad() => new()
    {
        Nit = Nit ?? "", Nrc = Nrc ?? "", Nombre = Nombre ?? "", CodActividad = CodActividad ?? "", DescActividad = DescActividad ?? "",
        NombreComercial = NombreComercial, TipoEstablecimiento = TipoEstablecimiento, Departamento = Departamento ?? "",
        Municipio = Municipio ?? "", Complemento = Complemento ?? "", Telefono = Telefono ?? "", Correo = Correo ?? "",
        CodEstable = CodEstable, CodPuntoVenta = CodPuntoVenta
    };

    public static EmisorFormulario Desde(ConfiguracionEmisor c) => new()
    {
        Nit = c.Nit, Nrc = c.Nrc, Nombre = c.Nombre, CodActividad = c.CodActividad, DescActividad = c.DescActividad,
        NombreComercial = c.NombreComercial, TipoEstablecimiento = c.TipoEstablecimiento, Departamento = c.Departamento,
        Municipio = c.Municipio, Complemento = c.Complemento, Telefono = c.Telefono, Correo = c.Correo,
        CodEstable = c.CodEstable, CodPuntoVenta = c.CodPuntoVenta
    };
}

public sealed class UsuarioFormulario
{
    [Required(ErrorMessage = "Ingrese el nombre completo.")]
    [StringLength(150)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [Required(ErrorMessage = "Ingrese el correo.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = "";

    [Required(ErrorMessage = "Seleccione el rol.")]
    [Display(Name = "Rol")]
    public string Rol { get; set; } = Facturacion.Application.Roles.Facturador;

    [Required(ErrorMessage = "Ingrese una contraseña inicial.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña inicial")]
    public string Contrasena { get; set; } = "";
}
