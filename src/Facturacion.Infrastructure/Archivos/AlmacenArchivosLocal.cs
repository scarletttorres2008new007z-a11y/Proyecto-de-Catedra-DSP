using System.Security.Cryptography;
using Facturacion.Application.Abstracciones;

namespace Facturacion.Infrastructure.Archivos;

/// <summary>
/// Almacena archivos en una carpeta del servidor fuera de wwwroot. Las rutas se validan para impedir salir
/// de la carpeta raíz, los archivos se crean con FileMode.CreateNew (nunca se sobrescriben) y se verifica
/// el contenido escrito comparando su huella SHA-256.
/// </summary>
public sealed class AlmacenArchivosLocal : IAlmacenArchivos
{
    private readonly string _raiz;

    public AlmacenArchivosLocal(string raiz)
    {
        _raiz = Path.GetFullPath(raiz);
        Directory.CreateDirectory(_raiz);
    }

    public string Raiz => _raiz;

    private string RutaAbsoluta(string rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa) || Path.IsPathRooted(rutaRelativa))
            throw new ArgumentException("Ruta de archivo no válida.", nameof(rutaRelativa));
        var completa = Path.GetFullPath(Path.Combine(_raiz, rutaRelativa.Replace('/', Path.DirectorySeparatorChar)));
        var raizConSeparador = _raiz.EndsWith(Path.DirectorySeparatorChar) ? _raiz : _raiz + Path.DirectorySeparatorChar;
        if (!completa.StartsWith(raizConSeparador, StringComparison.Ordinal))
            throw new ArgumentException("La ruta sale de la carpeta de almacenamiento.", nameof(rutaRelativa));
        return completa;
    }

    public async Task<ArchivoEscrito> GuardarNuevoAsync(string rutaRelativa, byte[] contenido, CancellationToken ct = default)
    {
        if (contenido.Length == 0) throw new InvalidOperationException("No se puede guardar un archivo vacío.");
        var ruta = RutaAbsoluta(rutaRelativa);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);

        await using (var fs = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await fs.WriteAsync(contenido, ct);
            await fs.FlushAsync(ct);
        }

        var hash = Convert.ToHexString(SHA256.HashData(contenido));
        var escrito = await File.ReadAllBytesAsync(ruta, ct);
        if (escrito.Length != contenido.Length || Convert.ToHexString(SHA256.HashData(escrito)) != hash)
            throw new IOException("El archivo escrito no coincide con el contenido generado.");

        return new ArchivoEscrito(rutaRelativa.Replace('\\', '/'), escrito.Length, hash);
    }

    public async Task<byte[]?> LeerAsync(string rutaRelativa, CancellationToken ct = default)
    {
        var ruta = RutaAbsoluta(rutaRelativa);
        return File.Exists(ruta) ? await File.ReadAllBytesAsync(ruta, ct) : null;
    }

    public void Eliminar(string rutaRelativa)
    {
        var ruta = RutaAbsoluta(rutaRelativa);
        if (File.Exists(ruta)) File.Delete(ruta);
    }
}
