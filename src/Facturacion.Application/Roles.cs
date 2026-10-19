namespace Facturacion.Application;

/// <summary>Roles del sistema.</summary>
public static class Roles
{
    /// <summary>Acceso total: configuración, usuarios, anulaciones, auditoría y copias de seguridad.</summary>
    public const string Administrador = "Administrador";

    /// <summary>Operación diaria: clientes, productos, emisión, consulta y reenvío de documentos.</summary>
    public const string Facturador = "Facturador";

    public static readonly string[] Todos = { Administrador, Facturador };
}
