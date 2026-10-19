using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Domain.Reglas;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Application.Servicios;

/// <summary>Datos del emisor (no secretos) editables por el administrador. Cada cambio queda auditado.</summary>
public sealed class ConfiguracionEmisorService(IAppDbContext db, IReloj reloj, IUsuarioActual usuario, IAuditoria auditoria)
{
    public Task<ConfiguracionEmisor?> ObtenerAsync(CancellationToken ct = default) =>
        db.ConfiguracionEmisor.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync(ct);

    public static void Normalizar(ConfiguracionEmisor c)
    {
        c.Nit = IdentificadoresFiscales.NormalizarNit(c.Nit) ?? "";
        c.Nrc = IdentificadoresFiscales.NormalizarNrc(c.Nrc) ?? "";
        c.Nombre = (c.Nombre ?? "").Trim();
        c.CodActividad = (c.CodActividad ?? "").Trim();
        c.DescActividad = (c.DescActividad ?? "").Trim();
        c.NombreComercial = string.IsNullOrWhiteSpace(c.NombreComercial) ? null : c.NombreComercial.Trim();
        c.Complemento = (c.Complemento ?? "").Trim();
        c.Telefono = (c.Telefono ?? "").Trim();
        c.Correo = (c.Correo ?? "").Trim().ToLowerInvariant();
        c.CodEstable = (c.CodEstable ?? "").Trim().ToUpperInvariant();
        c.CodPuntoVenta = (c.CodPuntoVenta ?? "").Trim().ToUpperInvariant();
    }

    public static ResultadoValidacion Validar(ConfiguracionEmisor c)
    {
        var r = new ResultadoValidacion();
        if (!IdentificadoresFiscales.EsNitValido(c.Nit)) r.Agregar(nameof(c.Nit), "El NIT debe tener 14 dígitos (o 9 si es DUI homologado), sin guiones.");
        if (!IdentificadoresFiscales.EsNrcValido(c.Nrc)) r.Agregar(nameof(c.Nrc), "El NRC debe tener de 1 a 8 dígitos.");
        if (string.IsNullOrWhiteSpace(c.Nombre) || c.Nombre.Length > 250) r.Agregar(nameof(c.Nombre), "Ingrese el nombre o razón social (máximo 250 caracteres).");
        if (!IdentificadoresFiscales.EsActividadValida(c.CodActividad)) r.Agregar(nameof(c.CodActividad), "El código de actividad debe tener de 2 a 6 dígitos.");
        if (string.IsNullOrWhiteSpace(c.DescActividad) || c.DescActividad.Length > 150) r.Agregar(nameof(c.DescActividad), "Ingrese la descripción de la actividad (máximo 150 caracteres).");
        if (c.NombreComercial?.Length > 150) r.Agregar(nameof(c.NombreComercial), "El nombre comercial admite como máximo 150 caracteres.");
        if (!Catalogos.TiposEstablecimiento.ContainsKey(c.TipoEstablecimiento)) r.Agregar(nameof(c.TipoEstablecimiento), "Seleccione el tipo de establecimiento.");
        if (!IdentificadoresFiscales.EsDepartamentoValido(c.Departamento)) r.Agregar(nameof(c.Departamento), "Seleccione el departamento.");
        if (!IdentificadoresFiscales.EsMunicipioValido(c.Municipio)) r.Agregar(nameof(c.Municipio), "Ingrese el código de municipio de 2 dígitos (CAT-013).");
        if (string.IsNullOrWhiteSpace(c.Complemento) || c.Complemento.Length > 200) r.Agregar(nameof(c.Complemento), "Ingrese el complemento de la dirección (máximo 200 caracteres).");
        if (c.Telefono.Length is < 8 or > 30) r.Agregar(nameof(c.Telefono), "El teléfono debe tener entre 8 y 30 caracteres.");
        if (!IdentificadoresFiscales.EsCorreoValido(c.Correo)) r.Agregar(nameof(c.Correo), "Ingrese un correo válido.");
        if (!IdentificadoresFiscales.EsCodigoEstablecimientoValido(c.CodEstable)) r.Agregar(nameof(c.CodEstable), "El código de establecimiento debe tener 4 letras o dígitos.");
        if (!IdentificadoresFiscales.EsCodigoEstablecimientoValido(c.CodPuntoVenta)) r.Agregar(nameof(c.CodPuntoVenta), "El código de punto de venta debe tener 4 letras o dígitos.");
        return r;
    }

    public async Task GuardarAsync(ConfiguracionEmisor datos, CancellationToken ct = default)
    {
        Normalizar(datos);
        var r = Validar(datos);
        if (!r.EsValido) throw new ReglaNegocioException(r);

        var actual = await db.ConfiguracionEmisor.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        var cambios = new List<string>();
        if (actual is null)
        {
            actual = new ConfiguracionEmisor();
            db.ConfiguracionEmisor.Add(actual);
            cambios.Add("configuración inicial");
        }
        else
        {
            void Comparar(string campo, string? a, string? b) { if (a != b) cambios.Add(campo); }
            Comparar("NIT", actual.Nit, datos.Nit);
            Comparar("NRC", actual.Nrc, datos.Nrc);
            Comparar("nombre", actual.Nombre, datos.Nombre);
            Comparar("actividad", actual.CodActividad + actual.DescActividad, datos.CodActividad + datos.DescActividad);
            Comparar("nombre comercial", actual.NombreComercial, datos.NombreComercial);
            Comparar("establecimiento", actual.TipoEstablecimiento + actual.CodEstable + actual.CodPuntoVenta,
                datos.TipoEstablecimiento + datos.CodEstable + datos.CodPuntoVenta);
            Comparar("dirección", actual.Departamento + actual.Municipio + actual.Complemento, datos.Departamento + datos.Municipio + datos.Complemento);
            Comparar("teléfono", actual.Telefono, datos.Telefono);
            Comparar("correo", actual.Correo, datos.Correo);
        }

        actual.Nit = datos.Nit;
        actual.Nrc = datos.Nrc;
        actual.Nombre = datos.Nombre;
        actual.CodActividad = datos.CodActividad;
        actual.DescActividad = datos.DescActividad;
        actual.NombreComercial = datos.NombreComercial;
        actual.TipoEstablecimiento = datos.TipoEstablecimiento;
        actual.Departamento = datos.Departamento;
        actual.Municipio = datos.Municipio;
        actual.Complemento = datos.Complemento;
        actual.Telefono = datos.Telefono;
        actual.Correo = datos.Correo;
        actual.CodEstable = datos.CodEstable;
        actual.CodPuntoVenta = datos.CodPuntoVenta;
        actual.ModificadoUtc = reloj.UtcNow;
        actual.ModificadoPor = usuario.Nombre;

        auditoria.Agregar("Configuración del emisor modificada", nameof(ConfiguracionEmisor), null, ResultadoOperacion.Exito,
            cambios.Count == 0 ? "sin cambios" : "Campos: " + string.Join(", ", cambios));
        await db.SaveChangesAsync(ct);
    }
}
