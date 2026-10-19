using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;

namespace Facturacion.Infrastructure.Hacienda;

/// <summary>
/// Modo académico: no se conecta a ningún servicio del Ministerio de Hacienda. Deja constancia del paso del
/// flujo sin generar sellos de recepción ni afirmar aceptación oficial.
/// </summary>
public sealed class TransmisorAcademico : ITransmisorHacienda
{
    public ModoOperacion Modo => ModoOperacion.Academico;

    public Task<ResultadoTransmision> TransmitirDteAsync(DocumentoTributario documento, string json, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoTransmision(
            Simulado: true,
            Procesado: false,
            Mensaje: "Modo académico: el documento NO fue transmitido al Ministerio de Hacienda. No existe sello de recepción ni validación oficial."));

    public Task<ResultadoTransmision> InvalidarAsync(Anulacion anulacion, string json, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoTransmision(
            Simulado: true,
            Procesado: true,
            Mensaje: "Anulación procesada en modo académico (simulación). No es una invalidación oficial ante el Ministerio de Hacienda."));
}

/// <summary>
/// Marcador del modo oficial. La integración real requiere: credenciales de la API (usuario/contraseña del
/// emisor autorizado) para obtener el token de autenticación, firma JWS del DTE con el certificado del emisor
/// (firmador oficial), transmisión al servicio de recepción, manejo de la respuesta con sello de recepción,
/// contingencia e invalidación, según el Manual Técnico de Integración vigente. Nada de eso está implementado.
/// </summary>
public sealed class TransmisorOficialNoImplementado : ITransmisorHacienda
{
    public ModoOperacion Modo => ModoOperacion.Oficial;

    private static Task<ResultadoTransmision> NoDisponible() =>
        throw new NotSupportedException("La integración oficial con el Ministerio de Hacienda no está implementada en este proyecto.");

    public Task<ResultadoTransmision> TransmitirDteAsync(DocumentoTributario documento, string json, CancellationToken ct = default) => NoDisponible();

    public Task<ResultadoTransmision> InvalidarAsync(Anulacion anulacion, string json, CancellationToken ct = default) => NoDisponible();
}
