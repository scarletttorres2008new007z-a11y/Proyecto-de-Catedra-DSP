using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Infrastructure.Persistencia;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Infrastructure.Servicios;

public sealed class AuditoriaEf(AppDbContext db, IReloj reloj, IUsuarioActual usuario, IServiceScopeFactory scopes) : IAuditoria
{
    private RegistroAuditoria Crear(string accion, string? entidad, string? entidadId, ResultadoOperacion resultado, string? detalle) => new()
    {
        FechaUtc = reloj.UtcNow,
        Usuario = Recortar(usuario.Nombre, 256)!,
        Accion = Recortar(accion, 100)!,
        Entidad = Recortar(entidad, 60),
        EntidadId = Recortar(entidadId, 60),
        Resultado = resultado,
        Detalle = Recortar(detalle, 1000)
    };

    public void Agregar(string accion, string? entidad, string? entidadId, ResultadoOperacion resultado, string? detalle = null) =>
        db.Auditoria.Add(Crear(accion, entidad, entidadId, resultado, detalle));

    public async Task RegistrarAsync(string accion, string? entidad, string? entidadId, ResultadoOperacion resultado, string? detalle = null, CancellationToken ct = default)
    {
        // Contexto independiente: el registro se guarda aunque la unidad de trabajo actual haya fallado.
        var registro = Crear(accion, entidad, entidadId, resultado, detalle);
        using var scope = scopes.CreateScope();
        var otro = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        otro.Auditoria.Add(registro);
        await otro.SaveChangesAsync(ct);
    }

    private static string? Recortar(string? v, int max) => v is null ? null : v.Length <= max ? v : v[..max];
}
