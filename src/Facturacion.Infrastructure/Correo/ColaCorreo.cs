using System.Threading.Channels;
using Facturacion.Application.Abstracciones;
using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Facturacion.Infrastructure.Correo;

public sealed class ColaCorreo : IColaCorreo
{
    private readonly Channel<int> _canal = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<int> Lector => _canal.Reader;

    public void Encolar(int envioId) => _canal.Writer.TryWrite(envioId);
}

/// <summary>
/// Procesa los envíos en segundo plano para no bloquear la interfaz. Al iniciar, vuelve a encolar los
/// envíos que quedaron pendientes (por ejemplo, si la aplicación se detuvo antes de procesarlos).
/// </summary>
public sealed class TrabajadorCorreo(ColaCorreo cola, IServiceScopeFactory scopes, ILogger<TrabajadorCorreo> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendientes = await db.EnviosCorreo.AsNoTracking().Where(e => e.Estado == EstadoEnvio.Pendiente)
                .OrderBy(e => e.Id).Select(e => e.Id).ToListAsync(stoppingToken);
            foreach (var id in pendientes) cola.Encolar(id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudieron recuperar los envíos pendientes al iniciar");
        }

        await foreach (var envioId in cola.Lector.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var servicio = scope.ServiceProvider.GetRequiredService<CorreoService>();
                await servicio.ProcesarAsync(envioId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al procesar el envío {EnvioId}", envioId);
            }
        }
    }
}
