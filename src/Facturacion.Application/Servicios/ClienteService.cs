using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Domain.Reglas;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Application.Servicios;

public sealed record FiltroClientes(string? Busqueda, TipoCliente? Tipo, bool? Activo, int Pagina = 1);

public sealed class ClienteService(IAppDbContext db, IReloj reloj, IAuditoria auditoria)
{
    public async Task<Pagina<Cliente>> ListarAsync(FiltroClientes filtro, CancellationToken ct = default)
    {
        var q = db.Clientes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var b = filtro.Busqueda.Trim();
            var bSinGuiones = b.Replace("-", "");
            q = q.Where(c => c.Nombre.Contains(b)
                             || (c.NombreComercial != null && c.NombreComercial.Contains(b))
                             || (c.NumeroDocumento != null && (c.NumeroDocumento.Contains(b) || c.NumeroDocumento.Contains(bSinGuiones)))
                             || (c.Nrc != null && c.Nrc.Contains(bSinGuiones))
                             || (c.Correo != null && c.Correo.Contains(b)));
        }
        if (filtro.Tipo is { } t) q = q.Where(c => c.Tipo == t);
        if (filtro.Activo is { } a) q = q.Where(c => c.Activo == a);

        var pagina = PaginacionExtensions.Normalizar(filtro.Pagina);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(c => c.Nombre).ThenBy(c => c.Id)
            .Skip((pagina - 1) * PaginacionExtensions.TamanoPorDefecto).Take(PaginacionExtensions.TamanoPorDefecto)
            .ToListAsync(ct);
        return new Pagina<Cliente>(items, pagina, PaginacionExtensions.TamanoPorDefecto, total);
    }

    public Task<Cliente?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <summary>Clientes activos aptos para el tipo de documento (para la pantalla de emisión).</summary>
    public Task<List<Cliente>> ActivosParaAsync(TipoDte tipo, CancellationToken ct = default)
    {
        var q = db.Clientes.AsNoTracking().Where(c => c.Activo);
        if (tipo != TipoDte.FacturaConsumidorFinal) q = q.Where(c => c.Tipo == TipoCliente.Contribuyente);
        return q.OrderBy(c => c.Nombre).ToListAsync(ct);
    }

    public async Task<Cliente> CrearAsync(Cliente datos, CancellationToken ct = default)
    {
        ReglasCliente.Normalizar(datos);
        await ValidarAsync(datos, null, ct);
        datos.Id = 0;
        datos.Activo = true;
        datos.CreadoUtc = reloj.UtcNow;
        datos.Version = Guid.NewGuid();
        db.Clientes.Add(datos);
        await db.SaveChangesAsync(ct);
        auditoria.Agregar("Cliente creado", nameof(Cliente), datos.Id.ToString(), ResultadoOperacion.Exito, $"Tipo: {datos.Tipo}");
        await db.SaveChangesAsync(ct);
        return datos;
    }

    public async Task ActualizarAsync(Cliente datos, CancellationToken ct = default)
    {
        var actual = await db.Clientes.FirstOrDefaultAsync(c => c.Id == datos.Id, ct)
                     ?? throw new ReglaNegocioException("El cliente no existe.");
        if (actual.Version != datos.Version)
            throw new ReglaNegocioException("Otro usuario modificó este cliente. Recargue la página e intente de nuevo.");

        ReglasCliente.Normalizar(datos);
        await ValidarAsync(datos, datos.Id, ct);

        actual.Tipo = datos.Tipo;
        actual.Nombre = datos.Nombre;
        actual.NombreComercial = datos.NombreComercial;
        actual.TipoDocumento = datos.TipoDocumento;
        actual.NumeroDocumento = datos.NumeroDocumento;
        actual.Nrc = datos.Nrc;
        actual.CodActividad = datos.CodActividad;
        actual.DescActividad = datos.DescActividad;
        actual.Departamento = datos.Departamento;
        actual.Municipio = datos.Municipio;
        actual.Complemento = datos.Complemento;
        actual.Telefono = datos.Telefono;
        actual.Correo = datos.Correo;
        actual.ModificadoUtc = reloj.UtcNow;
        actual.Version = Guid.NewGuid();
        auditoria.Agregar("Cliente modificado", nameof(Cliente), actual.Id.ToString(), ResultadoOperacion.Exito);
        await GuardarConcurrenciaAsync(ct);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default)
    {
        var c = await db.Clientes.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new ReglaNegocioException("El cliente no existe.");
        if (c.Activo == activo) return;
        c.Activo = activo;
        c.ModificadoUtc = reloj.UtcNow;
        c.Version = Guid.NewGuid();
        auditoria.Agregar(activo ? "Cliente activado" : "Cliente desactivado", nameof(Cliente), id.ToString(), ResultadoOperacion.Exito);
        await GuardarConcurrenciaAsync(ct);
    }

    public Task<List<DocumentoTributario>> HistorialAsync(int clienteId, CancellationToken ct = default) =>
        db.Documentos.AsNoTracking().Where(d => d.ClienteId == clienteId)
            .OrderByDescending(d => d.EmitidoUtc).ToListAsync(ct);

    private async Task ValidarAsync(Cliente c, int? idActual, CancellationToken ct)
    {
        var r = ReglasCliente.Validar(c);
        if (c.TipoDocumento is not null && c.NumeroDocumento is not null)
        {
            var duplicado = await db.Clientes.AnyAsync(x => x.TipoDocumento == c.TipoDocumento
                                                         && x.NumeroDocumento == c.NumeroDocumento
                                                         && x.Id != (idActual ?? 0), ct);
            if (duplicado) r.Agregar(nameof(c.NumeroDocumento), "Ya existe un cliente con ese documento de identificación.");
        }
        if (!r.EsValido) throw new ReglaNegocioException(r);
    }

    private async Task GuardarConcurrenciaAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReglaNegocioException("Otro usuario modificó este registro. Recargue la página e intente de nuevo.");
        }
    }
}
