using Facturacion.Domain.Entidades;

namespace Facturacion.Domain.Reglas;

/// <summary>
/// Normalización y validación de clientes. Solo se exige lo necesario para el tipo de cliente:
///  * Consumidor final: nombre. Documento, dirección, teléfono y correo son opcionales
///    (el correo es necesario para el envío automático, pero no para emitir).
///  * Contribuyente: NIT, NRC, nombre, actividad económica, dirección completa y correo
///    (campos obligatorios del receptor en CCF v3 y nota de crédito v3).
/// </summary>
public static class ReglasCliente
{
    public static void Normalizar(Cliente c)
    {
        c.Nombre = c.Nombre?.Trim() ?? "";
        c.NombreComercial = Vacio(c.NombreComercial);
        if (c.Tipo == TipoCliente.Contribuyente) c.TipoDocumento = Catalogos.DocNit;
        c.TipoDocumento = Vacio(c.TipoDocumento);
        c.NumeroDocumento = c.TipoDocumento is null ? null : IdentificadoresFiscales.NormalizarDocumento(c.TipoDocumento, c.NumeroDocumento);
        c.Nrc = IdentificadoresFiscales.NormalizarNrc(c.Nrc);
        c.CodActividad = Vacio(c.CodActividad);
        c.DescActividad = Vacio(c.DescActividad);
        c.Departamento = Vacio(c.Departamento);
        c.Municipio = Vacio(c.Municipio);
        c.Complemento = Vacio(c.Complemento);
        c.Telefono = Vacio(c.Telefono);
        c.Correo = Vacio(c.Correo)?.ToLowerInvariant();
    }

    public static ResultadoValidacion Validar(Cliente c)
    {
        var r = new ResultadoValidacion();
        if (!Enum.IsDefined(c.Tipo)) r.Agregar(nameof(c.Tipo), "Seleccione el tipo de cliente.");
        if (string.IsNullOrWhiteSpace(c.Nombre)) r.Agregar(nameof(c.Nombre), "El nombre o razón social es obligatorio.");
        else if (c.Nombre.Length > 250) r.Agregar(nameof(c.Nombre), "El nombre admite como máximo 250 caracteres.");
        if (c.NombreComercial?.Length > 150) r.Agregar(nameof(c.NombreComercial), "El nombre comercial admite como máximo 150 caracteres.");

        var esContribuyente = c.Tipo == TipoCliente.Contribuyente;

        // Documento de identificación.
        if (esContribuyente || c.TipoDocumento is not null || c.NumeroDocumento is not null)
        {
            if (c.TipoDocumento is null) r.Agregar(nameof(c.TipoDocumento), "Seleccione el tipo de documento.");
            else
            {
                var error = IdentificadoresFiscales.ValidarDocumento(c.TipoDocumento, c.NumeroDocumento);
                if (error is not null) r.Agregar(nameof(c.NumeroDocumento), esContribuyente ? error.Replace("número de documento", "NIT") : error);
            }
        }

        // NRC y actividad económica.
        if (esContribuyente)
        {
            if (c.Nrc is null) r.Agregar(nameof(c.Nrc), "El NRC es obligatorio para contribuyentes.");
            if (c.CodActividad is null) r.Agregar(nameof(c.CodActividad), "El código de actividad económica es obligatorio para contribuyentes.");
            if (c.DescActividad is null) r.Agregar(nameof(c.DescActividad), "La descripción de la actividad económica es obligatoria para contribuyentes.");
        }
        else if (c.Nrc is not null && c.TipoDocumento != Catalogos.DocNit)
        {
            r.Agregar(nameof(c.Nrc), "El NRC solo se registra cuando el documento de identificación es NIT.");
        }
        if (c.Nrc is not null && !IdentificadoresFiscales.EsNrcValido(c.Nrc))
            r.Agregar(nameof(c.Nrc), "El NRC debe contener de 1 a 8 dígitos.");
        if (c.CodActividad is not null && !IdentificadoresFiscales.EsActividadValida(c.CodActividad))
            r.Agregar(nameof(c.CodActividad), "El código de actividad económica debe tener de 2 a 6 dígitos (catálogo CAT-019).");
        if (c.DescActividad?.Length > 150) r.Agregar(nameof(c.DescActividad), "La actividad económica admite como máximo 150 caracteres.");

        // Dirección: completa (departamento, municipio, complemento) o vacía, salvo contribuyentes.
        var algunaDireccion = c.Departamento is not null || c.Municipio is not null || c.Complemento is not null;
        if (esContribuyente || algunaDireccion)
        {
            if (!IdentificadoresFiscales.EsDepartamentoValido(c.Departamento)) r.Agregar(nameof(c.Departamento), "Seleccione el departamento.");
            if (!IdentificadoresFiscales.EsMunicipioValido(c.Municipio)) r.Agregar(nameof(c.Municipio), "Ingrese el código de municipio de 2 dígitos (catálogo CAT-013).");
            if (c.Complemento is null) r.Agregar(nameof(c.Complemento), "Ingrese el complemento de la dirección (calle, número, colonia).");
            else if (c.Complemento.Length > 200) r.Agregar(nameof(c.Complemento), "El complemento admite como máximo 200 caracteres.");
        }

        if (c.Telefono is not null && c.Telefono.Length is < 8 or > 30)
            r.Agregar(nameof(c.Telefono), "El teléfono debe tener entre 8 y 30 caracteres.");

        if (esContribuyente && c.Correo is null) r.Agregar(nameof(c.Correo), "El correo electrónico es obligatorio para contribuyentes.");
        if (c.Correo is not null && !IdentificadoresFiscales.EsCorreoValido(c.Correo))
            r.Agregar(nameof(c.Correo), "El correo electrónico no es válido.");

        return r;
    }

    private static string? Vacio(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
