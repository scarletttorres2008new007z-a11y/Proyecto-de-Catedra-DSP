# 8. Limitaciones, problemas y soluciones

## Limitaciones conocidas

- **Sin integración oficial** con Hacienda: no hay firma, transmisión, sello, contingencia ni invalidación real.
- Los esquemas se implementaron a partir de la documentación pública; deben validarse contra los JSON Schema oficiales
  vigentes antes de cualquier uso real.
- El municipio se ingresa como código de 2 dígitos (CAT-013) sin lista desplegable; la actividad económica (CAT-019)
  se escribe a mano.
- No se verifica el dígito verificador del DUI ni del NIT (el esquema solo exige el formato).
- Solo operaciones gravadas al contado, sin retenciones, percepciones, ventas exentas ni no sujetas.
- Plazos de anulación configurables: el valor de CCF y NC (1 día) debe confirmarse con la normativa o la cátedra.
- La cola de correo es en memoria; los envíos pendientes se reanudan al reiniciar, pero en varias instancias se
  necesitaría una cola compartida.
- Las copias no incluyen la carpeta de documentos JSON/PDF.
- SQLite se usa solo en pruebas automatizadas; la aplicación requiere SQL Server.

## Problemas encontrados y cómo se resolvieron

| Problema | Solución |
|---|---|
| Dos emisiones simultáneas chocaban al crear el primer correlativo del año (clave duplicada en SQL Server) | La fila se crea antes de la transacción tolerando la carrera; el incremento sigue siendo atómico. Prueba de 8 emisiones simultáneas |
| Tras iniciar sesión sin página de retorno se redirigía a una ruta inexistente | Redirección explícita al panel; prueba web |
| El resumen de validación vacío usaba un estilo en línea bloqueado por la CSP | Parcial `_ResumenErrores` que solo se dibuja con errores |
| Razor codificaba tildes y eñes como entidades HTML | `WebEncoderOptions` con todos los rangos Unicode |
| Las pruebas web chocaban con el límite de 10 intentos por minuto | Límite configurable (`Seguridad:IntentosInicioSesionPorMinuto`) y prueba propia con el límite real |
| Advertencia de vulnerabilidad en `System.Security.Cryptography.Pkcs` | Versión fijada a 10.0.13 |
