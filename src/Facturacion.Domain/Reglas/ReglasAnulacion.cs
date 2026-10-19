using Facturacion.Domain.Entidades;

namespace Facturacion.Domain.Reglas;

public enum UnidadPlazo
{
    Dias = 1,
    Meses = 2
}

/// <summary>Plazo de invalidación configurable para un tipo de DTE.</summary>
public sealed record PlazoAnulacion(int Valor, UnidadPlazo Unidad)
{
    public override string ToString() => Unidad == UnidadPlazo.Dias
        ? (Valor == 1 ? "1 día" : $"{Valor} días")
        : (Valor == 1 ? "1 mes" : $"{Valor} meses");
}

/// <summary>
/// Reglas de anulación (invalidación). Los plazos no están fijos en el código: se leen de la configuración
/// (sección "Anulaciones:Plazos") para poder ajustarlos a la normativa vigente o a lo que indique la cátedra.
/// El plazo vence al final del día (hora de El Salvador) resultante de sumar el plazo a la fecha de emisión.
/// </summary>
public static class ReglasAnulacion
{
    public static DateTime FechaLimiteLocal(DateOnly fechaEmision, PlazoAnulacion plazo)
    {
        var fecha = plazo.Unidad == UnidadPlazo.Dias ? fechaEmision.AddDays(plazo.Valor) : fechaEmision.AddMonths(plazo.Valor);
        return fecha.ToDateTime(new TimeOnly(23, 59, 59));
    }

    public static ResultadoValidacion Validar(
        DocumentoTributario documento,
        DateTime ahoraLocal,
        PlazoAnulacion plazo,
        TipoAnulacion tipo,
        string? motivo,
        DocumentoTributario? reemplazo,
        bool tieneAnulacionVigente,
        bool tieneNotasCreditoVigentes)
    {
        var r = new ResultadoValidacion();

        if (documento.Estado == EstadoDocumento.Anulado || tieneAnulacionVigente)
        {
            r.Agregar("DocumentoId", "El documento ya fue anulado o tiene una anulación en curso.");
            return r;
        }

        var limite = FechaLimiteLocal(documento.FechaEmision, plazo);
        if (ahoraLocal > limite)
            r.Agregar("DocumentoId", $"El plazo de anulación para {TiposDteInfo.NombreCorto(documento.Tipo)} ({plazo}) venció el {limite:dd/MM/yyyy HH:mm}.");

        if (tieneNotasCreditoVigentes)
            r.Agregar("DocumentoId", "El comprobante tiene notas de crédito vigentes; anúlelas antes de anular el comprobante.");

        if (!Enum.IsDefined(tipo)) r.Agregar("Tipo", "Seleccione el tipo de anulación.");

        if (string.IsNullOrWhiteSpace(motivo)) r.Agregar("Motivo", "Describa el motivo de la anulación.");
        else if (motivo.Trim().Length is < 5 or > 250) r.Agregar("Motivo", "El motivo debe tener entre 5 y 250 caracteres.");

        var requiereReemplazo = tipo is TipoAnulacion.ErrorEnInformacion or TipoAnulacion.Otro;
        if (requiereReemplazo)
        {
            if (reemplazo is null)
                r.Agregar("DocumentoReemplazoId", "Este tipo de anulación requiere indicar el documento que reemplaza al anulado.");
            else
            {
                if (reemplazo.Id == documento.Id) r.Agregar("DocumentoReemplazoId", "El documento de reemplazo no puede ser el mismo documento.");
                if (reemplazo.Tipo != documento.Tipo) r.Agregar("DocumentoReemplazoId", "El documento de reemplazo debe ser del mismo tipo.");
                if (reemplazo.Estado != EstadoDocumento.Emitido) r.Agregar("DocumentoReemplazoId", "El documento de reemplazo no puede estar anulado.");
            }
        }
        else if (reemplazo is not null)
        {
            r.Agregar("DocumentoReemplazoId", "Al rescindir la operación no se indica documento de reemplazo.");
        }

        return r;
    }
}
