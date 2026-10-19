namespace Facturacion.Application;

public sealed record Pagina<T>(IReadOnlyList<T> Elementos, int NumeroPagina, int TamanoPagina, int Total)
{
    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)TamanoPagina));
    public bool TieneAnterior => NumeroPagina > 1;
    public bool TieneSiguiente => NumeroPagina < TotalPaginas;
}

public static class PaginacionExtensions
{
    public const int TamanoPorDefecto = 20;

    public static int Normalizar(int pagina) => pagina < 1 ? 1 : pagina;
}
