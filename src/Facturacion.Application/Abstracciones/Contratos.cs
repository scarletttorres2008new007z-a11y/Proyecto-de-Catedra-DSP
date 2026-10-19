using Facturacion.Domain;
using Facturacion.Domain.Entidades;

namespace Facturacion.Application.Abstracciones;

/// <summary>Usuario autenticado que ejecuta la operación (para auditoría).</summary>
public interface IUsuarioActual
{
    string Nombre { get; }
}

/// <summary>Reloj del sistema con conversión a la hora de El Salvador (UTC−6, sin horario de verano).</summary>
public interface IReloj
{
    DateTime UtcNow { get; }
    DateTime AhoraLocal { get; }
    DateTime ALocal(DateTime utc);
}

public sealed record ArchivoEscrito(string RutaRelativa, long TamanoBytes, string Sha256);

/// <summary>Almacenamiento de archivos generados en una carpeta controlada fuera de wwwroot.</summary>
public interface IAlmacenArchivos
{
    /// <summary>Escribe un archivo nuevo. Falla si ya existe (no sobrescribe) y verifica lo escrito.</summary>
    Task<ArchivoEscrito> GuardarNuevoAsync(string rutaRelativa, byte[] contenido, CancellationToken ct = default);
    Task<byte[]?> LeerAsync(string rutaRelativa, CancellationToken ct = default);
    void Eliminar(string rutaRelativa);
}

public interface IGeneradorPdf
{
    /// <summary>Versión del formato de representación gráfica (se registra junto al archivo).</summary>
    string Version { get; }
    byte[] Generar(DocumentoTributario documento, EmisorSnapshot emisor);
}

public sealed record AdjuntoCorreo(string NombreArchivo, string TipoContenido, byte[] Contenido);

public sealed record MensajeCorreo(
    string Destinatario,
    string? NombreDestinatario,
    string Asunto,
    string CuerpoTexto,
    string CuerpoHtml,
    IReadOnlyList<AdjuntoCorreo> Adjuntos);

public enum TipoFalloCorreo
{
    Configuracion,
    DestinatarioInvalido,
    Autenticacion,
    Conexion,
    TiempoAgotado,
    RechazoServidor,
    Desconocido
}

/// <summary>Error de envío con un mensaje seguro (sin credenciales ni trazas internas).</summary>
public sealed class CorreoException : Exception
{
    public TipoFalloCorreo Tipo { get; }

    public CorreoException(TipoFalloCorreo tipo, string mensajeSeguro, Exception? interna = null) : base(mensajeSeguro, interna)
        => Tipo = tipo;
}

/// <summary>Envío SMTP de bajo nivel. Devuelve el identificador del mensaje o la respuesta del servidor.</summary>
public interface IEnviadorCorreo
{
    Task<string?> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default);
}

/// <summary>Cola en memoria para procesar envíos sin bloquear la interfaz.</summary>
public interface IColaCorreo
{
    void Encolar(int envioId);
}

public sealed record ResultadoTransmision(bool Simulado, bool Procesado, string Mensaje, string? SelloRecepcion = null);

/// <summary>
/// Punto de integración con el Ministerio de Hacienda. La implementación académica nunca devuelve sellos
/// ni afirma aceptación oficial; la implementación oficial requiere credenciales, certificado y autorización.
/// </summary>
public interface ITransmisorHacienda
{
    ModoOperacion Modo { get; }
    Task<ResultadoTransmision> TransmitirDteAsync(DocumentoTributario documento, string json, CancellationToken ct = default);
    Task<ResultadoTransmision> InvalidarAsync(Anulacion anulacion, string json, CancellationToken ct = default);
}

public sealed record ResultadoRespaldoMotor(bool Exitoso, string RutaArchivo, long? TamanoBytes, bool Verificado, string Mensaje);

/// <summary>Copia de seguridad de la base de datos con el procedimiento nativo del motor configurado.</summary>
public interface IProveedorRespaldo
{
    string Motor { get; }
    string DirectorioDestino { get; }
    Task<ResultadoRespaldoMotor> CrearAsync(string nombreArchivo, CancellationToken ct = default);
}

/// <summary>Registro de auditoría.</summary>
public interface IAuditoria
{
    /// <summary>Agrega el registro a la unidad de trabajo actual (se guarda con el resto de cambios).</summary>
    void Agregar(string accion, string? entidad, string? entidadId, ResultadoOperacion resultado, string? detalle = null);

    /// <summary>Guarda el registro de inmediato en una unidad de trabajo independiente (útil tras un error).</summary>
    Task RegistrarAsync(string accion, string? entidad, string? entidadId, ResultadoOperacion resultado, string? detalle = null, CancellationToken ct = default);
}
