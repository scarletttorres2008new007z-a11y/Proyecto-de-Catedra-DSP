namespace Facturacion.Domain.Reglas;

public sealed record ErrorValidacion(string Campo, string Mensaje);

public sealed class ResultadoValidacion
{
    private readonly List<ErrorValidacion> _errores = new();

    public IReadOnlyList<ErrorValidacion> Errores => _errores;
    public bool EsValido => _errores.Count == 0;

    public void Agregar(string campo, string mensaje) => _errores.Add(new ErrorValidacion(campo, mensaje));

    public void Agregar(ResultadoValidacion otro) => _errores.AddRange(otro.Errores);

    public override string ToString() => string.Join(" ", _errores.Select(e => e.Mensaje));
}

/// <summary>Excepción de reglas de negocio con mensajes aptos para el usuario.</summary>
public sealed class ReglaNegocioException : Exception
{
    public ResultadoValidacion Resultado { get; }

    public ReglaNegocioException(ResultadoValidacion resultado) : base(resultado.ToString()) => Resultado = resultado;

    public ReglaNegocioException(string mensaje, string campo = "") : base(mensaje)
    {
        Resultado = new ResultadoValidacion();
        Resultado.Agregar(campo, mensaje);
    }
}
