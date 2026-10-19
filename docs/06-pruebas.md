# 6. Pruebas y resultados

```bash
dotnet test                                   # 116 pruebas + 3 omitidas si no hay SQL Server
FACTURACION_PRUEBAS_SQLSERVER="Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True" dotnet test
```

## Principios

- Cada prueba de integración crea **su propia** base SQLite en una carpeta temporal y la borra al terminar.
- Las pruebas de SQL Server crean una base con nombre único `FacturacionPruebas_…`, aplican las migraciones y eliminan
  solo esa base. Nunca usan la base de la aplicación.
- El correo se sustituye por `EnviadorCorreoFalso`: ninguna prueba envía correos reales.
- Datos sintéticos (`DatosSinteticos`): nombres con "PRUEBA", NIT `00000000000017`, correos `@ejemplo.test`.
- Las contraseñas de los usuarios de prueba se generan al azar en cada ejecución.

## Cobertura

| Grupo | Qué verifica |
|---|---|
| Dominio | IVA de factura y CCF, redondeo de punto medio, descuentos, cantidades decimales, límites; número a letras; DUI, NIT, NRC, correo; reglas de cliente; receptor por tipo; NC solo sobre CCF vigente; plazos, doble anulación y reemplazo |
| Emisión | Escenarios A, B y C; estructura JSON por tipo sin sello y con aviso académico; PDF con código, número de control y aviso; idempotencia; correlativos; inventario; reversión completa si falla la escritura de archivos; instantáneas |
| Correo | Adjuntos JSON y PDF; fallos SMTP con mensaje seguro; cliente sin correo; reenvío sin reemitir; archivo faltante o modificado; almacén sin sobrescritura ni rutas `..` |
| Anulación | Escenario D; doble anulación; plazo vencido y configurable; reemplazo obligatorio; CCF con NC vigentes; notificación solo tras procesar |
| Respaldo y auditoría | Escenario F con copia legible; fallo registrado; auditoría inmodificable; filtros (escenario E); concurrencia; duplicados |
| Web | Redirección a inicio de sesión; encabezados de seguridad; cookie segura; mensaje genérico; permisos del facturador; CSRF; usuario desactivado; redirección externa; 404 sin detalles; límite de 10 intentos |
| SQL Server (opcional) | 8 emisiones simultáneas con 5 unidades en existencia (5 emitidas, correlativos 1–5); disparador de auditoría; respaldo verificado |

## Resultados registrados (19/10/2026, contenedor Linux, SQL Server 2022 en Docker)

```
Passed!  - Failed: 0, Passed: 119, Skipped: 0, Total: 119
```

La prueba de concurrencia en SQL Server se repitió 5 veces seguidas sin fallos.

Además se recorrieron los escenarios A–F en un navegador (Chromium) contra SQL Server y un servidor SMTP local
(smtp4dev): llegaron 5 correos (factura, CCF, NC, reenvío del CCF y notificación de anulación) con sus adjuntos
`CODIGO.json` y `CODIGO.pdf`; con el SMTP detenido el reenvío quedó "Fallido" con un mensaje seguro y al reactivarlo
el siguiente reenvío fue aceptado. Ver [guía de demostración](07-guia-demostracion.md).
