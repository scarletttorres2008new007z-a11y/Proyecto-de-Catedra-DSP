# 4. Copias de seguridad y restauración

## Crear una copia ("Backup (fecha)")

Menú **Copias de seguridad** (solo administrador) → botón **Backup (dd/MM/aaaa)**. La aplicación ejecuta:

```sql
BACKUP DATABASE [FacturacionDSP] TO DISK = @ruta WITH INIT, CHECKSUM;
RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM;
```

El archivo se llama `Backup_aaaa-MM-dd_HHmmss.bak`. La pantalla muestra el resultado que devolvió SQL Server (ruta,
tamaño leído de `msdb.dbo.backupset` y verificación). Si falla, queda registrado como **Fallido** con un mensaje seguro.

- `Respaldos:Directorio`: carpeta **del servidor de SQL Server** donde escribir; el servicio de SQL Server necesita
  permiso de escritura. Vacío = carpeta predeterminada de la instancia.
- Los archivos JSON y PDF (carpeta `App_Data/Documentos`) no están dentro de la base: cópielos aparte.

## Restaurar (manual, nunca automático)

La aplicación no restaura. Restaurar sobre la base activa **reemplaza todos los datos posteriores a la copia**.
Procedimiento recomendado: restaurar primero con **otro nombre** y revisar.

```sql
-- 1. Ver los archivos lógicos de la copia
RESTORE FILELISTONLY FROM DISK = N'C:\Respaldos\Backup_2026-10-19_014150.bak';

-- 2. Restaurar como base de revisión (no toca la base activa)
RESTORE DATABASE [FacturacionDSP_Revision]
FROM DISK = N'C:\Respaldos\Backup_2026-10-19_014150.bak'
WITH MOVE N'FacturacionDSP' TO N'C:\SQLData\FacturacionDSP_Revision.mdf',
     MOVE N'FacturacionDSP_log' TO N'C:\SQLData\FacturacionDSP_Revision_log.ldf',
     CHECKSUM, RECOVERY;
```

Solo si se decide reemplazar la base activa, con autorización y la aplicación detenida:

```sql
ALTER DATABASE [FacturacionDSP] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [FacturacionDSP] FROM DISK = N'C:\Respaldos\Backup_….bak' WITH REPLACE, CHECKSUM, RECOVERY;
ALTER DATABASE [FacturacionDSP] SET MULTI_USER;
```

Después restaure la carpeta de documentos de la misma fecha para que las huellas SHA-256 coincidan.
