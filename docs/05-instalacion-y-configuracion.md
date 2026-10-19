# 5. Instalación y configuración

## Requisitos

- SDK de .NET 10 (`dotnet --version` ≥ 10.0).
- SQL Server 2019+ (Express/Developer en Windows, o Docker: `mcr.microsoft.com/mssql/server:2022-latest`).

## Base de datos

`appsettings.json` trae una cadena sin secretos (`Server=localhost;Database=FacturacionDSP;Trusted_Connection=True;…`).
Para usuario y contraseña de SQL use User Secrets (no se suben a GitHub):

```bash
cd src/Facturacion.Web
dotnet user-secrets set "ConnectionStrings:Facturacion" "Server=localhost,1433;Database=FacturacionDSP;User Id=USUARIO;Password=CONTRASEÑA;TrustServerCertificate=True"
dotnet run -- migrar        # aplica las migraciones; no borra nada existente
```

En `Development` las migraciones también se aplican al iniciar (`BaseDatos:MigrarAlIniciar`). Nunca se elimina ni se
recrea una base existente.

## Primer administrador

```bash
dotnet run -- crear-admin --correo admin@ejemplo.com --nombre "Nombre Apellido"
```

Pide la contraseña sin mostrarla (o la lee de la variable `FACTURACION_ADMIN_PASSWORD`). Después, desde **Usuarios**,
el administrador crea las cuentas de Facturador.

## Correo SMTP

```bash
dotnet user-secrets set "Smtp:Servidor" "smtp.ejemplo.com"
dotnet user-secrets set "Smtp:Puerto" "587"
dotnet user-secrets set "Smtp:Seguridad" "StartTls"        # Ninguna, SslAlConectar, StartTls, StartTlsSiDisponible, Automatica
dotnet user-secrets set "Smtp:Usuario" "usuario"
dotnet user-secrets set "Smtp:Contrasena" "contraseña-o-clave-de-aplicacion"
dotnet user-secrets set "Smtp:RemitenteCorreo" "facturacion@ejemplo.com"
```

Para practicar sin enviar correos reales use un servidor de pruebas local como smtp4dev:
`docker run -d -p 5080:80 -p 2525:25 rnwood/smtp4dev` con `Servidor=localhost`, `Puerto=2525`, `Seguridad=Ninguna`.
La pantalla **Configuración** muestra si el SMTP está configurado, sin mostrar credenciales.

## Otros parámetros (`appsettings.json`)

| Clave | Por defecto | Uso |
|---|---|---|
| `Facturacion:Modo` | `Academico` | `Oficial` no está implementado y rechaza la emisión |
| `Anulaciones:Factura` / `CreditoFiscal` / `NotaCredito` | 3 Meses / 1 Dias / 1 Dias | Plazos de anulación; ajústelos a la normativa vigente |
| `Almacenamiento:RutaDocumentos` | `App_Data/Documentos` | Carpeta de JSON y PDF (fuera de `wwwroot`) |
| `Respaldos:Directorio` | vacío | Carpeta de copias en el servidor SQL |
| `Seguridad:IntentosInicioSesionPorMinuto` | 10 | Límite por IP |

En producción use variables de entorno (`ConnectionStrings__Facturacion`, `Smtp__Contrasena`, …) y HTTPS.
