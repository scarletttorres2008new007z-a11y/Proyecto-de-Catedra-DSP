namespace Facturacion.Domain;

/// <summary>
/// Tipos de DTE implementados. El valor numérico coincide con el código del catálogo CAT-002
/// del Ministerio de Hacienda ("01", "03", "05").
/// </summary>
public enum TipoDte
{
    FacturaConsumidorFinal = 1,
    ComprobanteCreditoFiscal = 3,
    NotaCredito = 5
}

public enum TipoCliente
{
    /// <summary>Persona o entidad que recibe factura de consumidor final.</summary>
    ConsumidorFinal = 1,
    /// <summary>Contribuyente inscrito en IVA (tiene NIT y NRC); puede recibir CCF y notas de crédito.</summary>
    Contribuyente = 2
}

public enum EstadoDocumento
{
    /// <summary>Generado y guardado por el sistema. En modo académico no ha sido transmitido a Hacienda.</summary>
    Emitido = 1,
    /// <summary>Invalidado mediante el flujo de anulación (simulada en modo académico).</summary>
    Anulado = 2
}

public enum ModoOperacion
{
    /// <summary>Demostración académica: no hay transmisión real al Ministerio de Hacienda.</summary>
    Academico = 1,
    /// <summary>Integración oficial (no implementada: requiere credenciales, certificado y autorización).</summary>
    Oficial = 2
}

public enum EstadoEnvio
{
    Pendiente = 1,
    /// <summary>El servidor SMTP aceptó el mensaje. No implica que el cliente lo haya recibido o leído.</summary>
    EnviadoServidorSmtp = 2,
    Fallido = 3
}

public enum MotivoEnvio
{
    Emision = 1,
    Reenvio = 2,
    NotificacionAnulacion = 3
}

public enum EstadoAnulacion
{
    Solicitada = 1,
    /// <summary>Procesada en modo académico. No es una invalidación oficial.</summary>
    ProcesadaSimulada = 2,
    Rechazada = 3,
    /// <summary>Reservado para la integración oficial (respuesta de Hacienda con sello).</summary>
    ProcesadaOficial = 4
}

/// <summary>Catálogo CAT-024 (tipo de invalidación).</summary>
public enum TipoAnulacion
{
    ErrorEnInformacion = 1,
    RescindirOperacion = 2,
    Otro = 3
}

public enum PropositoArchivo
{
    DocumentoJson = 1,
    DocumentoPdf = 2,
    AnulacionJson = 3
}

public enum EstadoRespaldo
{
    Exitoso = 1,
    Fallido = 2
}

public enum ResultadoOperacion
{
    Exito = 1,
    Error = 2,
    Advertencia = 3
}
