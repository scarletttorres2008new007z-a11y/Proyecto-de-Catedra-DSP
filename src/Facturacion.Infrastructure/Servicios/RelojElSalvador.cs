using Facturacion.Application.Abstracciones;

namespace Facturacion.Infrastructure.Servicios;

/// <summary>
/// Reloj con zona horaria de El Salvador (America/El_Salvador, UTC−6 sin horario de verano). En la base de
/// datos se guardan instantes en UTC; la fecha y hora del documento se expresan en hora local.
/// </summary>
public sealed class RelojElSalvador : IReloj
{
    private static readonly TimeZoneInfo Zona = ObtenerZona();

    private static TimeZoneInfo ObtenerZona()
    {
        foreach (var id in new[] { "America/El_Salvador", "Central America Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        }
        return TimeZoneInfo.CreateCustomTimeZone("El Salvador", TimeSpan.FromHours(-6), "El Salvador", "El Salvador");
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime AhoraLocal => ALocal(UtcNow);

    public DateTime ALocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);
}
