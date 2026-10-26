# Cómo funciona el sistema de facturación electrónica, paso a paso

Esta guía explica, en palabras sencillas, qué hace el sistema por dentro desde que alguien abre la página hasta que el
cliente recibe su factura por correo. Todo lo que se describe aquí está en el código del repositorio (se indica el
archivo para que puedas mostrarlo en la presentación).

---

## 1. La idea general en una frase

Es una página web hecha en **ASP.NET Core MVC (C#)** que guarda todo en **SQL Server**. Un usuario registra clientes y
productos, arma un documento (factura, CCF o nota de crédito), el **servidor** calcula los importes, guarda el
documento, crea su **JSON** y su **PDF**, y lo envía por **correo** al cliente. Todo queda registrado en un
**historial** que nadie puede modificar.

Como es un proyecto académico, **no se conecta con Hacienda**: los documentos no tienen firma ni sello y así se indica
en la pantalla, el PDF, el JSON y el correo.

---

## 2. Las cuatro partes (capas) del proyecto

Piensa en el sistema como un restaurante:

| Capa (carpeta) | Comparación | Qué contiene |
|---|---|---|
| **Facturacion.Web** | El mesero | Las pantallas (vistas Razor), los controladores que reciben lo que el usuario escribe, el inicio de sesión y la seguridad |
| **Facturacion.Application** | El jefe de cocina | Los "casos de uso": emitir, enviar correo, anular, hacer respaldos. Decide el orden de los pasos |
| **Facturacion.Domain** | Las recetas | Las reglas puras: cómo se calcula el IVA, qué datos necesita un cliente, cuándo se puede anular |
| **Facturacion.Infrastructure** | Los aparatos de la cocina | La base de datos (EF Core + SQL Server), el generador de PDF (QuestPDF), el envío de correo (MailKit), los archivos en disco |

Regla importante: **las reglas (Domain) no dependen de nada**. Por eso se pueden probar sin base de datos ni internet.
La capa Application usa "interfaces" (contratos) como `IEnviadorCorreo` o `IGeneradorPdf`; Infrastructure las cumple.
En las pruebas se cambia el correo real por uno falso sin tocar el resto.

Cuando el programa arranca (`src/Facturacion.Web/Program.cs`), conecta todas las piezas: configura la base de datos,
Identity (usuarios y roles), la seguridad, el registro de errores (Serilog) y el trabajador de correo en segundo plano.

---

## 3. Entrar al sistema (seguridad)

1. Todas las páginas exigen haber iniciado sesión; si no, te manda a **Iniciar sesión**.
2. Las contraseñas no se guardan tal cual: Identity guarda un **hash** (una huella imposible de revertir).
3. Si alguien falla la contraseña **5 veces**, la cuenta se bloquea 15 minutos. Además, desde una misma conexión solo
   se permiten **10 intentos por minuto**.
4. Hay dos roles:
   - **Administrador**: todo, incluidas anulaciones, historial, respaldos, configuración y usuarios.
   - **Facturador**: clientes, productos, emisión y reenvío de correos.
5. Cada formulario lleva un **token anti-falsificación (CSRF)**: un formulario enviado desde otra página es rechazado.
6. El primer administrador se crea desde la consola con `dotnet run -- crear-admin`, y él crea los demás usuarios.

---

## 4. Datos que se preparan antes de emitir

- **Configuración (emisor):** NIT, NRC, nombre, actividad, dirección, teléfono, correo y los códigos de establecimiento
  (`M001`) y punto de venta (`P001`). Sin esto el sistema no deja emitir.
- **Clientes:** pueden ser *consumidor final* (basta el nombre) o *contribuyente* (NIT, NRC, actividad, dirección y
  correo obligatorios). Las reglas están en `Domain/Reglas/ReglasCliente.cs`.
- **Productos:** código, nombre, **precio sin IVA**, unidad de medida y, si se quiere, control de existencias.
- Nada se borra: clientes y productos se **desactivan**. Si dos personas editan el mismo cliente a la vez, el sistema
  lo detecta (campo `Version`) y pide recargar.

---

## 5. Qué pasa exactamente al emitir un documento

Archivo principal: `src/Facturacion.Application/Servicios/EmisionService.cs`.

### Paso 1: Capturar
El usuario elige tipo (factura, CCF o nota de crédito), cliente, forma de pago y productos con su cantidad y descuento.

### Paso 2: Revisar (el servidor calcula)
El navegador **no** decide los importes. El servidor usa `CalculadoraImportes` (`Domain/Reglas/CalculadoraImportes.cs`):

- **Factura:** el precio que ve el cliente **incluye IVA**.
  Ejemplo: producto de $10.00 sin IVA → precio $11.30. Tres unidades = $33.90, de las cuales $3.90 son IVA.
- **CCF:** el precio va **sin IVA** y el IVA (13 %) se suma al final.
  Ejemplo: 3 × $10.00 − $2.00 de descuento = $28.00; IVA $3.64; total $31.64.
- Todo se redondea a 2 decimales (0.065 sube a 0.07).
- El total también se escribe en letras: "TREINTA Y TRES 90/100 DÓLARES".

También valida: el CCF solo a contribuyentes, que haya existencias, que la nota de crédito no acredite más de lo que
tenía el CCF, etc. La pantalla de revisión muestra los totales y genera una **clave única** para esta emisión.

### Paso 3: Confirmar (todo o nada)
Al pulsar **Confirmar y emitir**:

1. **¿Ya existe esa clave?** Si el usuario hizo doble clic o recargó, se devuelve el documento ya emitido y **no se
   duplica**.
2. Se abre una **transacción** en SQL Server (todo lo siguiente se guarda junto o no se guarda nada):
   1. Se toma el siguiente **número correlativo** del tipo y del año (por ejemplo, el 1 de facturas de 2026).
   2. Se **descuentan existencias** solo si alcanzan.
   3. Se guarda el **documento** con:
      - **Código de generación:** un identificador único, por ejemplo `A53F33DA-10FE-…`.
      - **Número de control:** `DTE-01-M001P001-000000000000001`.
      - **Una copia de los datos del cliente y del emisor:** si mañana cambias el cliente, la factura vieja no cambia.
   4. Se genera el **JSON** con la estructura del esquema de Hacienda (`Application/Dte/`), se valida y se guarda como
      `CODIGO.json`.
   5. Se genera el **PDF** (`Infrastructure/Pdf/GeneradorPdfQuest.cs`) y se guarda como `CODIGO.pdf`.
   6. De cada archivo se guarda una **huella SHA-256** para detectar si alguien lo modifica después.
   7. Se registra un **envío de correo "Pendiente"** y una línea en el **historial**.
3. Si **cualquier paso falla**, se deshace todo: no queda documento, ni número usado, ni existencias descontadas, y se
   borran los archivos a medio escribir.
4. En modo académico se registra que **no se transmitió** a Hacienda.

Los archivos se guardan en `App_Data/Documentos/año/mes/`, fuera de la carpeta pública: solo se descargan con sesión
iniciada.

### Paso 4: El correo (en segundo plano)
Un trabajador (`Infrastructure/Correo/TrabajadorCorreo.cs`) toma el envío pendiente para no hacer esperar al usuario:

1. Comprueba que el cliente tenga correo.
2. Lee el JSON y el PDF y verifica que **existen y no fueron modificados** (huella SHA-256).
3. Arma el correo con los dos adjuntos y lo envía con MailKit al servidor SMTP.
4. Guarda el resultado:
   - **"Enviado al servidor SMTP":** el servidor aceptó el mensaje. No significa que el cliente lo leyó.
   - **"Fallido":** se guarda con un motivo claro y sin contraseñas, por ejemplo "No se pudo conectar con el servidor
     SMTP".
5. El botón **Reenviar** crea un intento nuevo con los **mismos archivos**. Nunca se crea otro documento.

---

## 6. Nota de crédito

Solo se puede hacer sobre un **CCF vigente**. Se elige el CCF, se indica cuántas unidades de cada línea se acreditan y
el sistema impide pasarse de lo que queda disponible. El JSON incluye `documentoRelacionado` con el código del CCF.

---

## 7. Anulaciones

Archivo: `Application/Servicios/AnulacionService.cs`. Solo el Administrador.

1. **Validación:** el documento no puede estar ya anulado y debe estar dentro del **plazo** configurado (Factura
   3 meses; CCF y nota de crédito 1 día, cambiables en `appsettings.json`). Además:
   - Un CCF con notas de crédito vigentes no se anula.
   - Los tipos "error en la información" y "otro" exigen indicar el documento que lo reemplaza.
2. Se registra la **solicitud** con el motivo, el responsable y quien la solicita.
3. Se genera el **JSON del evento de anulación** (sin sello, porque es académico).
4. En modo académico se procesa como **simulación**: el documento pasa a "Anulado (simulado)" y se **devuelven las
   existencias**.
5. **Solo después** de procesada se envía la **notificación** al cliente con el JSON del evento.

---

## 8. Historial de operaciones (auditoría)

Cada acción importante queda registrada: inicio de sesión, clientes creados, documentos emitidos, correos enviados o
fallidos, anulaciones y respaldos. Se guarda quién, cuándo, qué y el resultado.

Nadie puede cambiarlo ni borrarlo, por **dos candados**:
- **La aplicación** rechaza modificar o eliminar registros (`AppDbContext`).
- **SQL Server** tiene un **disparador** (`TR_Auditoria_SoloInsercion`) que bloquea cualquier `UPDATE` o `DELETE`,
  aunque se intente directamente en la base de datos.

La pantalla permite filtrar por fechas, tipo de operación, resultado, usuario o identificador.

---

## 9. Copias de seguridad ("Backup (fecha)")

Archivo: `Infrastructure/Respaldos/ProveedoresRespaldo.cs`.

1. El administrador pulsa **Backup (dd/MM/aaaa)**.
2. SQL Server ejecuta `BACKUP DATABASE … WITH CHECKSUM` y crea `Backup_aaaa-MM-dd_HHmmss.bak`.
3. Luego ejecuta `RESTORE VERIFYONLY` para comprobar que la copia sirve.
4. Se muestra el resultado real (exitoso o fallido, tamaño, verificado).
5. **No hay botón de restaurar.** Restaurar borra lo que vino después, así que se hace a mano siguiendo
   `docs/04-respaldos.md`.

---

## 10. Las tablas de la base de datos

| Tabla | Para qué sirve |
|---|---|
| ConfiguracionEmisor | Datos de la empresa que emite (una sola fila) |
| Clientes | Consumidores finales y contribuyentes |
| Productos | Catálogo con precio sin IVA y existencias |
| Documentos | Cada factura, CCF o nota de crédito con sus totales y copia de los datos del cliente |
| DetallesDocumento | Las líneas (productos) de cada documento |
| Correlativos | El último número usado por tipo y año |
| ArchivosGenerados | Ruta, tamaño y huella de cada JSON y PDF |
| EnviosCorreo | Cada intento de envío con su estado |
| Anulaciones | Solicitudes de anulación y su resultado |
| Auditoria | El historial que no se puede modificar |
| Respaldos | Resultado de cada copia de seguridad |
| AspNetUsers, AspNetRoles… | Usuarios, contraseñas (hash) y roles de Identity |

Los importes son `decimal(18,2)` (nunca números con decimales imprecisos). Hay **índices únicos** que impiden
duplicar el código de generación, el número de control, la clave de emisión o dos anulaciones vigentes del mismo
documento.

---

## 11. Cómo se comprobó que funciona

- **119 pruebas automáticas (xUnit)**, todas pasaron:
  - Cálculos, reglas, emisión, JSON, PDF, correo, anulación, respaldos, permisos y seguridad.
  - Usan bases de datos temporales y un correo falso: no tocan datos reales.
  - Una de ellas lanza **8 emisiones al mismo tiempo** con solo 5 productos en existencia: se emiten exactamente 5 y
    los números quedan del 1 al 5, sin repetir.
- **Recorrido en el navegador de los escenarios A–F** con SQL Server y un servidor de correo de prueba: llegaron los
  5 correos con sus adjuntos.

---

## 12. Lo que el sistema NO hace (para decirlo en la presentación)

- No transmite a Hacienda: no tiene firma electrónica ni sello de recepción. Está preparado para agregarlo
  (`ITransmisorHacienda`), pero eso requiere credenciales y certificado oficiales.
- Solo maneja ventas gravadas al contado (sin retenciones ni ventas exentas).
- La copia de seguridad no incluye la carpeta de JSON y PDF.
- Los plazos de anulación de CCF y nota de crédito deben confirmarse con la cátedra.

---

## Resumen para explicar en 30 segundos

> "El usuario captura el documento; el servidor calcula el IVA y los totales; al confirmar, en una sola transacción se
> asigna el número de control, se descuenta el inventario, se guarda el documento y se generan su JSON y su PDF con el
> código de generación como nombre. Luego, en segundo plano, se envían ambos por correo y se registra el resultado.
> Todo queda en un historial que ni la base de datos permite modificar, se pueden hacer anulaciones dentro del plazo y
> copias de seguridad verificadas. Es modo académico: no hay conexión con Hacienda."
