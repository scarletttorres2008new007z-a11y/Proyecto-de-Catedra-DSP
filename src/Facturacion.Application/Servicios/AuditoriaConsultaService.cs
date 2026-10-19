using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Application.Servicios;

public sealed record FiltroAuditoria(DateOnly? Desde, DateOnly? Hasta, string? Accion, ResultadoOperacion? Resultado, string? Identificador, string? Usuario, int Pagina = 1);

/// <summary>Consulta (solo lectura) del historial de operaciones.</summary>
public sealed class AuditoriaConsultaService(IAppDbContext db, IReloj reloj)
{
    public async Task<Pagina<RegistroAuditoria>> ListarAsync(FiltroAuditoria f, CancellationToken ct = default)
    {
        var q = db.Auditoria.AsNoTracking();
        // Las fechas del filtro son locales (El Salvador); se convierten al rango UTC equivalente.
        var desfase = reloj.AhoraLocal - reloj.UtcNow;
        if (f.Desde is { } d) q = q.Where(a => a.FechaUtc >= d.ToDateTime(TimeOnly.MinValue) - desfase);
        if (f.Hasta is { } h) q = q.Where(a => a.FechaUtc < h.AddDays(1).ToDateTime(TimeOnly.MinValue) - desfase);
        if (!string.IsNullOrWhiteSpace(f.Accion)) q = q.Where(a => a.Accion.Contains(f.Accion.Trim()));
        if (f.Resultado is { } r) q = q.Where(a => a.Resultado == r);
        if (!string.IsNullOrWhiteSpace(f.Identificador))
        {
            var id = f.Identificador.Trim();
            q = q.Where(a => a.EntidadId == id || (a.Detalle != null && a.Detalle.Contains(id)));
        }
        if (!string.IsNullOrWhiteSpace(f.Usuario)) q = q.Where(a => a.Usuario.Contains(f.Usuario.Trim()));

        var pagina = PaginacionExtensions.Normalizar(f.Pagina);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Id)
            .Skip((pagina - 1) * 50).Take(50).ToListAsync(ct);
        return new Pagina<RegistroAuditoria>(items, pagina, 50, total);
    }

    public Task<List<string>> AccionesAsync(CancellationToken ct = default) =>
        db.Auditoria.AsNoTracking().Select(a => a.Accion).Distinct().OrderBy(a => a).ToListAsync(ct);
}
