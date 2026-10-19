using Facturacion.Application.Servicios;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFacturacionApplication(this IServiceCollection services)
    {
        services.AddScoped<ClienteService>();
        services.AddScoped<ProductoService>();
        services.AddScoped<ConfiguracionEmisorService>();
        services.AddScoped<EmisionService>();
        services.AddScoped<CorreoService>();
        services.AddScoped<AnulacionService>();
        services.AddScoped<RespaldoService>();
        services.AddScoped<DocumentoService>();
        services.AddScoped<PanelService>();
        services.AddScoped<AuditoriaConsultaService>();
        return services;
    }
}
