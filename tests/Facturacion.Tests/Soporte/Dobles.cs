using System.Collections.Concurrent;
using Facturacion.Application.Abstracciones;

namespace Facturacion.Tests.Soporte;

/// <summary>Reloj controlable. La hora local de El Salvador es UTC−6 (sin horario de verano).</summary>
public sealed class RelojPrueba : IReloj
{
    public DateTime UtcNow { get; set; } = new(2026, 3, 10, 16, 0, 0, DateTimeKind.Utc);
    public DateTime AhoraLocal => ALocal(UtcNow);
    public DateTime ALocal(DateTime utc) => DateTime.SpecifyKind(utc.AddHours(-6), DateTimeKind.Unspecified);
    public void Avanzar(TimeSpan t) => UtcNow = UtcNow.Add(t);
}

public sealed class UsuarioPrueba : IUsuarioActual
{
    public string Nombre { get; set; } = "facturador.prueba@ejemplo.test";
}

/// <summary>
/// Enviador de correo falso: no se conecta a ningún servidor. Guarda los mensajes "enviados" o lanza el
/// fallo configurado. Las pruebas nunca envían correos reales.
/// </summary>
public sealed class EnviadorCorreoFalso : IEnviadorCorreo
{
    public ConcurrentQueue<MensajeCorreo> Enviados { get; } = new();
    public CorreoException? FalloSiguiente { get; set; }
    public bool FallarSiempre { get; set; }

    public Task<string?> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        if (FalloSiguiente is { } f)
        {
            if (!FallarSiempre) FalloSiguiente = null;
            throw f;
        }
        Enviados.Enqueue(mensaje);
        return Task.FromResult<string?>($"250 2.0.0 Ok: aceptado por el servidor de pruebas {Enviados.Count}");
    }
}

/// <summary>Cola que solo registra los identificadores encolados (el trabajador está deshabilitado en pruebas).</summary>
public sealed class ColaCorreoPrueba : IColaCorreo
{
    public ConcurrentQueue<int> Encolados { get; } = new();
    public void Encolar(int envioId) => Encolados.Enqueue(envioId);
}
