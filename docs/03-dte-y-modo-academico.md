# 3. DTE, cálculos y modo académico

## Estructura de cada tipo

| Campo | Factura (01) v1 | CCF (03) v3 | Nota de crédito (05) v3 |
|---|---|---|---|
| Receptor | Opcional salvo nombre; documento obligatorio desde el umbral configurado (`Facturacion:UmbralIdentificacionFactura`, 25 000 por defecto) | Contribuyente: NIT, NRC, actividad, dirección y correo | El mismo receptor del CCF |
| Precio unitario | Incluye IVA | Sin IVA | Sin IVA |
| IVA | `ivaItem` por línea y `totalIva` | Tributo `"20"` por línea y en `resumen.tributos` | Igual que CCF |
| `documentoRelacionado` | null | null | Obligatorio: CCF (03), `tipoGeneracion` 2, código de generación |
| Número de control | `DTE-01-M001P001-000000000000001` | `DTE-03-…` | `DTE-05-…` |

Formato común: `codigoGeneracion` es un UUID en mayúsculas generado por el emisor; `ambiente` es `"00"` (pruebas);
`numeroControl` cumple `^DTE-(01|03|05)-[A-Z0-9]{8}-[0-9]{15}$` con correlativo anual por tipo. El JSON se valida
estructuralmente antes de escribirlo (`GeneradorDteJson.Validar`): patrones, campos obligatorios y que las sumas del
resumen coincidan con las líneas.

**Fuentes.** Los nombres de campos siguen los esquemas JSON publicados por el Ministerio de Hacienda en
factura.gob.sv (FE v1, CCF v3, NC v3, anulación v2). Antes de cualquier uso real hay que validar el JSON contra los
esquemas oficiales vigentes y la normativa, porque pueden cambiar.

## Reglas de cálculo (`CalculadoraImportes`)

- Los productos guardan el precio **sin IVA** con 2 decimales; IVA 13 %.
- Redondeo a 2 decimales con `MidpointRounding.AwayFromZero` (0.065 → 0.07).
- Factura: precio = redondear(precio × 1.13); venta gravada = redondear(cantidad × precio) − descuento;
  IVA de línea = redondear(gravada × 0.13 / 1.13); total = suma de ventas gravadas.
- CCF y NC: venta gravada = redondear(cantidad × precio) − descuento; IVA = redondear(total gravado × 0.13);
  total = gravado + IVA.
- Cantidades hasta 4 decimales; descuento no negativo ni mayor que la línea; máximo 2000 líneas.
- Nota de crédito: no puede acreditar más unidades de las disponibles en el CCF (descontando otras NC vigentes) ni
  superar su total.

## Modo académico frente a modo oficial

`Facturacion:Modo` vale `Academico` por defecto. En ese modo `TransmisorAcademico` **no envía nada** y devuelve
`Simulado = true` sin sello. Pantalla, PDF ("Sello de recepción: No existe (simulación)"), JSON (apéndice
`modoAcademico`) y correo lo advierten. Si se configura `Oficial`, la emisión se rechaza porque la integración no
existe (`TransmisorOficialNoImplementado`).

Una integración oficial real necesitaría, como mínimo y según la documentación vigente de Hacienda:

1. Registro como emisor y credenciales de la API otorgadas por Hacienda.
2. Certificado del emisor y firma del JSON (JWS) con el firmador oficial.
3. Autenticación para obtener un token y envío al servicio de recepción del ambiente correspondiente.
4. Guardar el **sello de recepción** devuelto; solo entonces el documento es válido.
5. Contingencia, consulta de estado e invalidación (evento de anulación firmado) por los servicios oficiales.

Este proyecto **no inventa** direcciones de servicio, credenciales, certificados ni sellos: basta con implementar
`ITransmisorHacienda` con los datos reales cuando se disponga de autorización.
