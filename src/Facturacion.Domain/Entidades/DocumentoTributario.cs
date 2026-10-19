namespace Facturacion.Domain.Entidades;

/// <summary>
/// Documento tributario emitido por el sistema. Conserva una copia (snapshot) de los datos del emisor,
/// del receptor y de los productos tal como estaban al emitirlo, para que cambios posteriores en los
/// catálogos no alteren el contenido histórico.
/// </summary>
public class DocumentoTributario
{
    public int Id { get; set; }
    public TipoDte Tipo { get; set; }

    /// <summary>Código de generación (UUID) generado por el emisor según la especificación de Hacienda.</summary>
    public Guid CodigoGeneracion { get; set; }
    public string NumeroControl { get; set; } = "";
    public int VersionEsquema { get; set; }
    public string Ambiente { get; set; } = Catalogos.AmbientePruebas;
    public ModoOperacion Modo { get; set; } = ModoOperacion.Academico;
    public EstadoDocumento Estado { get; set; } = EstadoDocumento.Emitido;

    public DateTime EmitidoUtc { get; set; }
    /// <summary>Fecha de emisión en hora de El Salvador (fecEmi).</summary>
    public DateOnly FechaEmision { get; set; }
    /// <summary>Hora de emisión en hora de El Salvador (horEmi).</summary>
    public TimeOnly HoraEmision { get; set; }

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // Snapshot del receptor al momento de la emisión.
    public TipoCliente ReceptorTipoCliente { get; set; }
    public string ReceptorNombre { get; set; } = "";
    public string? ReceptorNombreComercial { get; set; }
    public string? ReceptorTipoDocumento { get; set; }
    public string? ReceptorNumDocumento { get; set; }
    public string? ReceptorNrc { get; set; }
    public string? ReceptorCodActividad { get; set; }
    public string? ReceptorDescActividad { get; set; }
    public string? ReceptorDepartamento { get; set; }
    public string? ReceptorMunicipio { get; set; }
    public string? ReceptorComplemento { get; set; }
    public string? ReceptorTelefono { get; set; }
    public string? ReceptorCorreo { get; set; }

    /// <summary>Snapshot serializado de los datos del emisor usados en el documento.</summary>
    public string EmisorJson { get; set; } = "";

    public int CondicionOperacion { get; set; } = Catalogos.CondicionContado;
    public string? FormaPago { get; set; }

    // Resumen (importes en USD con 2 decimales).
    public decimal TotalGravada { get; set; }
    public decimal TotalDescuento { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TotalIva { get; set; }
    public decimal MontoTotalOperacion { get; set; }
    public decimal TotalPagar { get; set; }
    public string TotalLetras { get; set; } = "";

    // Documento relacionado (nota de crédito -> CCF).
    public int? DocumentoRelacionadoId { get; set; }
    public DocumentoTributario? DocumentoRelacionado { get; set; }
    public TipoDte? RelacionadoTipo { get; set; }
    public Guid? RelacionadoCodigoGeneracion { get; set; }
    public DateOnly? RelacionadoFechaEmision { get; set; }

    public string? Observaciones { get; set; }
    public string UsuarioEmisor { get; set; } = "";

    /// <summary>Clave enviada por el formulario de confirmación para impedir emisiones duplicadas.</summary>
    public Guid ClaveIdempotencia { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<DetalleDocumento> Detalles { get; set; } = new List<DetalleDocumento>();
    public ICollection<ArchivoGenerado> Archivos { get; set; } = new List<ArchivoGenerado>();
    public ICollection<EnvioCorreo> Envios { get; set; } = new List<EnvioCorreo>();
    public ICollection<Anulacion> Anulaciones { get; set; } = new List<Anulacion>();
}
