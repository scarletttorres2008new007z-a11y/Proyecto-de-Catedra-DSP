# Sistema web de facturación electrónica de El Salvador (Proyecto de Cátedra DSP)

Aplicación web en ASP.NET Core MVC (.NET 10) y SQL Server para registrar clientes y productos y emitir tres tipos de
Documentos Tributarios Electrónicos (DTE). Cada documento se guarda en la base de datos, genera su archivo JSON y su PDF
y se envía por correo al cliente.

- **Factura de consumidor final electrónica** (tipo 01, esquema v1).
- **Comprobante de crédito fiscal electrónico** (CCF, tipo 03, esquema v3).
- **Nota de crédito electrónica** (tipo 05, esquema v3, siempre relacionada con un CCF).

> **Modo académico.** El sistema **no** se conecta con el Ministerio de Hacienda. Los documentos no tienen firma
> electrónica, sello de recepción ni validez tributaria, y así lo indican la pantalla, el PDF, el JSON (apéndice
> `modoAcademico`) y el correo. La integración oficial está prevista como interfaz (`ITransmisorHacienda`), pero no
> está implementada. Consulte [docs/03-dte-y-modo-academico.md](docs/03-dte-y-modo-academico.md).

## Funciones

| Módulo | Qué hace |
|---|---|
| Acceso | Identity con roles Administrador y Facturador, contraseñas con hash, bloqueo tras 5 intentos, límite de 10 intentos por minuto, CSRF en todos los formularios, CSP estricta |
| Panel | Indicadores reales del período: ventas, notas de crédito, documentos vigentes, envíos fallidos, últimas operaciones |
| Clientes y productos | Validaciones por tipo (consumidor final o contribuyente con NIT, NRC, actividad y dirección), precios sin IVA, inventario opcional, activar o desactivar sin borrar, control de concurrencia |
| Emisión | Captura, revisión con cálculos del servidor y confirmación; transacción única con correlativo, inventario, documento, JSON, PDF y envío pendiente; idempotente ante doble clic |
| Archivos | `CODIGO_GENERACION.json` y `CODIGO_GENERACION.pdf` fuera de `wwwroot`, con huella SHA-256 verificada antes de cada envío |
| Correo | MailKit en segundo plano con ambos adjuntos; historial por intento (pendiente, enviado al servidor SMTP, fallido) y reenvío sin reemitir |
| Anulaciones | Plazos configurables por tipo, documento de reemplazo cuando corresponde, devolución de inventario y notificación al cliente solo después de procesada |
| Historial de operaciones | Registro de solo inserción, protegido por la aplicación y por un disparador de SQL Server, con filtros |
| Copias de seguridad | Botón "Backup (fecha)" con `BACKUP DATABASE … WITH CHECKSUM` y `RESTORE VERIFYONLY`; la restauración es manual y está documentada |

## Inicio rápido

Requisitos: SDK de .NET 10 y SQL Server 2019 o posterior (Express, Developer o contenedor Docker).

```bash
git clone <url-del-repositorio>
cd Proyecto-de-Catedra-DSP
dotnet tool restore
dotnet build

# 1. Cadena de conexión (si no usa la predeterminada con autenticación de Windows)
cd src/Facturacion.Web
dotnet user-secrets set "ConnectionStrings:Facturacion" "Server=localhost;Database=FacturacionDSP;User Id=...;Password=...;TrustServerCertificate=True"

# 2. Crear la base de datos y el primer administrador
dotnet run -- migrar
dotnet run -- crear-admin --correo admin@ejemplo.com --nombre "Nombre Apellido"   # pide la contraseña sin mostrarla

# 3. Ejecutar
dotnet run
```

Abra la dirección que muestra la consola, inicie sesión y complete **Configuración** (datos del emisor) antes de emitir.
Los pasos completos, incluido el correo SMTP, están en [docs/05-instalacion-y-configuracion.md](docs/05-instalacion-y-configuracion.md).

## Pruebas

```bash
dotnet test
```

Hay 116 pruebas que usan bases SQLite temporales y aisladas, y 3 pruebas opcionales contra SQL Server real que solo se
ejecutan si se define `FACTURACION_PRUEBAS_SQLSERVER`. Ninguna prueba envía correos reales ni usa datos de personas
reales. Resultados y alcance en [docs/06-pruebas.md](docs/06-pruebas.md).

## Estructura

```
src/
  Facturacion.Domain          Entidades, catálogos y reglas de negocio puras (cálculos, validaciones, plazos)
  Facturacion.Application     Casos de uso (emisión, correo, anulación, respaldos) y generación del JSON
  Facturacion.Infrastructure  EF Core + SQL Server, migraciones, PDF (QuestPDF), MailKit, archivos, respaldos
  Facturacion.Web             ASP.NET Core MVC: controladores, vistas Razor, seguridad
tests/
  Facturacion.Tests           xUnit: dominio, integración, web y SQL Server opcional
docs/                         Documentación académica
```

## Documentación

1. [Arquitectura](docs/01-arquitectura.md)
2. [Modelo de datos](docs/02-modelo-datos.md)
3. [DTE, cálculos y modo académico](docs/03-dte-y-modo-academico.md)
4. [Copias de seguridad y restauración](docs/04-respaldos.md)
5. [Instalación y configuración](docs/05-instalacion-y-configuracion.md)
6. [Pruebas y resultados](docs/06-pruebas.md)
7. [Guía de demostración (escenarios A–F)](docs/07-guia-demostracion.md)
8. [Limitaciones, problemas y soluciones](docs/08-limitaciones-y-problemas.md)
9. [Subir el proyecto a GitHub](docs/09-subir-a-github.md)
10. [Cómo funciona el sistema, paso a paso](docs/10-como-funciona.md)

## Tecnologías y licencias

| Componente | Versión | Licencia |
|---|---|---|
| ASP.NET Core MVC, EF Core, Identity | 10.0 | MIT |
| QuestPDF | 2026.9.1 | Licencia Community (gratuita para uso académico y organizaciones con ingresos menores a 1 millón USD anuales) |
| MailKit / MimeKit | 4.18.1 | MIT |
| Serilog | 10.0 | Apache 2.0 |
| xUnit, PdfPig | 2.9 / 0.1.16 | Apache 2.0 |
