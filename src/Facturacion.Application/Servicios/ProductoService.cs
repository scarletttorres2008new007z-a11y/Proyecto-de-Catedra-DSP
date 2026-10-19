using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Domain.Reglas;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Application.Servicios;

public sealed record FiltroProductos(string? Busqueda, bool? Activo, bool? SoloConInventarioBajo, int Pagina = 1);

/// <summary>
/// Gestión de productos. Política de inventario: solo los productos con "Controla inventario" llevan
/// existencias; la emisión de facturas y CCF las descuenta dentro de la misma transacción del documento
/// y la anulación de esos documentos las devuelve. Las notas de crédito no mueven inventario (las
/// devoluciones físicas se registran con un ajuste manual de existencias).
/// </summary>
public sealed class ProductoService(IAppDbContext db, IReloj reloj, IAuditoria auditoria)
{
    public const decimal UmbralInventarioBajo = 5m;

    public async Task<Pagina<Producto>> ListarAsync(FiltroProductos filtro, CancellationToken ct = default)
    {
        var q = db.Productos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var b = filtro.Busqueda.Trim();
            q = q.Where(p => p.Codigo.Contains(b) || p.Nombre.Contains(b) || (p.Descripcion != null && p.Descripcion.Contains(b)));
        }
        if (filtro.Activo is { } a) q = q.Where(p => p.Activo == a);
        if (filtro.SoloConInventarioBajo == true) q = q.Where(p => p.ControlaInventario && p.Existencias <= UmbralInventarioBajo);

        var pagina = PaginacionExtensions.Normalizar(filtro.Pagina);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(p => p.Nombre).ThenBy(p => p.Id)
            .Skip((pagina - 1) * PaginacionExtensions.TamanoPorDefecto).Take(PaginacionExtensions.TamanoPorDefecto)
            .ToListAsync(ct);
        return new Pagina<Producto>(items, pagina, PaginacionExtensions.TamanoPorDefecto, total);
    }

    public Task<Producto?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<Producto>> ActivosAsync(CancellationToken ct = default) =>
        db.Productos.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync(ct);

    public static void Normalizar(Producto p)
    {
        p.Codigo = (p.Codigo ?? "").Trim().ToUpperInvariant();
        p.Nombre = (p.Nombre ?? "").Trim();
        p.Descripcion = string.IsNullOrWhiteSpace(p.Descripcion) ? null : p.Descripcion.Trim();
        if (!p.ControlaInventario) p.Existencias = 0;
    }

    public static ResultadoValidacion Validar(Producto p)
    {
        var r = new ResultadoValidacion();
        if (string.IsNullOrWhiteSpace(p.Codigo)) r.Agregar(nameof(p.Codigo), "El código es obligatorio.");
        else if (p.Codigo.Length > 25) r.Agregar(nameof(p.Codigo), "El código admite como máximo 25 caracteres.");
        if (string.IsNullOrWhiteSpace(p.Nombre)) r.Agregar(nameof(p.Nombre), "El nombre es obligatorio.");
        else if (p.Nombre.Length > 200) r.Agregar(nameof(p.Nombre), "El nombre admite como máximo 200 caracteres.");
        if (p.Descripcion?.Length > 800) r.Agregar(nameof(p.Descripcion), "La descripción admite como máximo 800 caracteres.");
        if (p.PrecioSinIva <= 0) r.Agregar(nameof(p.PrecioSinIva), "El precio debe ser mayor que cero.");
        else if (p.PrecioSinIva > 1_000_000m) r.Agregar(nameof(p.PrecioSinIva), "El precio supera el máximo permitido.");
        else if (Dinero.Decimales(p.PrecioSinIva) > 2) r.Agregar(nameof(p.PrecioSinIva), "El precio admite como máximo 2 decimales.");
        if (!Catalogos.UnidadesMedida.ContainsKey(p.UnidadMedida)) r.Agregar(nameof(p.UnidadMedida), "Seleccione una unidad de medida válida.");
        if (p.ControlaInventario)
        {
            if (p.Existencias < 0) r.Agregar(nameof(p.Existencias), "Las existencias no pueden ser negativas.");
            else if (Dinero.Decimales(p.Existencias) > CalculadoraImportes.MaxDecimalesCantidad)
                r.Agregar(nameof(p.Existencias), "Las existencias admiten como máximo 4 decimales.");
        }
        return r;
    }

    public async Task<Producto> CrearAsync(Producto p, CancellationToken ct = default)
    {
        Normalizar(p);
        await ValidarAsync(p, null, ct);
        p.Id = 0;
        p.Activo = true;
        p.CreadoUtc = reloj.UtcNow;
        p.Version = Guid.NewGuid();
        db.Productos.Add(p);
        await db.SaveChangesAsync(ct);
        auditoria.Agregar("Producto creado", nameof(Producto), p.Id.ToString(), ResultadoOperacion.Exito, $"Código {p.Codigo}");
        await db.SaveChangesAsync(ct);
        return p;
    }

    public async Task ActualizarAsync(Producto datos, CancellationToken ct = default)
    {
        var actual = await db.Productos.FirstOrDefaultAsync(p => p.Id == datos.Id, ct)
                     ?? throw new ReglaNegocioException("El producto no existe.");
        if (actual.Version != datos.Version)
            throw new ReglaNegocioException("Otro usuario modificó este producto. Recargue la página e intente de nuevo.");
        Normalizar(datos);
        await ValidarAsync(datos, datos.Id, ct);

        var cambios = new List<string>();
        if (actual.PrecioSinIva != datos.PrecioSinIva) cambios.Add($"precio {actual.PrecioSinIva:0.00} → {datos.PrecioSinIva:0.00}");
        if (actual.Existencias != datos.Existencias || actual.ControlaInventario != datos.ControlaInventario)
            cambios.Add($"existencias {actual.Existencias:0.####} → {datos.Existencias:0.####} (ajuste manual)");

        actual.Codigo = datos.Codigo;
        actual.Nombre = datos.Nombre;
        actual.Descripcion = datos.Descripcion;
        actual.PrecioSinIva = datos.PrecioSinIva;
        actual.UnidadMedida = datos.UnidadMedida;
        actual.ControlaInventario = datos.ControlaInventario;
        actual.Existencias = datos.Existencias;
        actual.ModificadoUtc = reloj.UtcNow;
        actual.Version = Guid.NewGuid();
        auditoria.Agregar("Producto modificado", nameof(Producto), actual.Id.ToString(), ResultadoOperacion.Exito,
            cambios.Count > 0 ? string.Join("; ", cambios) : null);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReglaNegocioException("Otro usuario modificó este producto. Recargue la página e intente de nuevo.");
        }
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default)
    {
        var p = await db.Productos.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new ReglaNegocioException("El producto no existe.");
        if (p.Activo == activo) return;
        p.Activo = activo;
        p.ModificadoUtc = reloj.UtcNow;
        p.Version = Guid.NewGuid();
        auditoria.Agregar(activo ? "Producto activado" : "Producto desactivado", nameof(Producto), id.ToString(), ResultadoOperacion.Exito);
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidarAsync(Producto p, int? idActual, CancellationToken ct)
    {
        var r = Validar(p);
        if (!string.IsNullOrWhiteSpace(p.Codigo) && await db.Productos.AnyAsync(x => x.Codigo == p.Codigo && x.Id != (idActual ?? 0), ct))
            r.Agregar(nameof(p.Codigo), "Ya existe un producto con ese código.");
        if (!r.EsValido) throw new ReglaNegocioException(r);
    }
}
