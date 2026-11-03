# Research: Plantilla del correo de notificación

**Procedencia**: `Dec` = decisión del usuario o constitución v3.9.0; `Spec` = spec 016; `F007` = notificación; `Plan` = decisión técnica de este plan (alternativa más sencilla).

## D-01 Datos del resumen [Spec FR-002, FR-003, FR-006]

- `ICargasConsulta.ObtenerResumenAsync(idCarga)` devuelve `ResumenCarga`, leído dentro del pipeline `sql` existente, con `AsNoTracking` y en 3 consultas:
  - `NombreArchivo` de `CargaArchivo`;
  - `CargaPeriodo` (`Periodo` y `Resultado`), ordenado por `Periodo`;
  - `Incidencia`: el recuento por `Tipo` (`GroupBy`, ordenado por recuento descendente), el total y las primeras 20 por `Id` (tipo, fila, código, periodo y motivo).
- **Mapeos adicionales**, solo de lectura y sin cambios de esquema:
  - `CargaArchivo.NombreArchivo` (nvarchar(260));
  - la entidad `CargaPeriodo` (`Id`, `CargaId`, `Periodo` char(7), `Resultado` varchar(20)), igual que en Control (F010).
  - Se contrastan con el catálogo y se obtienen por ingeniería inversa si se autoriza (§6).
- **Momento de la lectura**: después de comprobar que la carga está Finalizado y antes de enviar.
  - Si falla tras los reintentos, la excepción sube y el mensaje no se confirma; el consumidor espera y la reentrega vuelve a empezar. Es el mismo comportamiento que ante un fallo al leer el estado (F007 D-02).
  - Un circuito abierto se traduce en `DependenciaNoDisponibleException`, que no es un fallo terminal.

## D-02 Construcción del correo [Spec FR-002 a FR-005] [Dec Q1–Q3]

- `PlantillaCorreo.Construir(idCarga, fechaFin, ResumenCarga, urlDetalle?)` en Application. Es una función pura, sin dependencias, y devuelve `CorreoNotificacion(Asunto, Html, Texto)`.
- El asunto no cambia: «Carga {idCarga} finalizada».
- **HTML**: tablas con estilos `style="…"` en línea, sin hojas externas ni imágenes. Contiene:
  - una cabecera con el título;
  - los datos: archivo, carga y fecha de fin (UTC);
  - la tabla de periodos con un color por resultado: Completado verde; Rechazado y Fallido rojo; Bloqueado y NoProcesado ámbar; el resto gris;
  - el resumen de incidencias por tipo;
  - la tabla con las primeras 20 y «y N más; consulte el detalle de la carga» si hay más;
  - «Sin periodos procesados» o «Sin incidencias» cuando corresponda;
  - el botón «Ver detalle» si hay URL.
- **Texto plano**: las mismas secciones en líneas.
- **Escapado**: `System.Net.WebUtility.HtmlEncode` sobre cada valor del usuario (archivo, motivos, códigos y periodos). Es de la BCL, sin paquetes.
- **No afirma «sin errores»**: el texto dice «La carga {id} finalizó» y nunca «correctamente».

## D-03 Envío multipart [Spec FR-004] [Dec Q2]

- `IEnvioCorreo.EnviarAsync(destinatario, asunto, texto, html, ct)`.
- `EnvioCorreoMailKit` usa `BodyBuilder { TextBody = texto, HtmlBody = html }.ToMessageBody()`, que produce `multipart/alternative` (MimeKit ya incluido en MailKit 4.18.2).
- El pipeline `smtp`, la clasificación de errores y `EnvioCorreoFallidoException` no cambian.

## D-04 URL del frontend [Spec FR-005] [Dec Q3]

- Variable `Notificaciones__UrlFrontend`, opcional, que llega desde `FRONTEND_URL` en `.env`. Compose usa por defecto `http://localhost:3000`, el frontend de Compose.
- Se registra como `ConfiguracionCorreo(string? UrlFrontend)` (record) desde la configuración en `Program.cs`. Si está vacía, no hay enlace.
- Si no es una URI absoluta http o https, se ignora y se registra una advertencia al arrancar, sin impedir el arranque.
- El enlace es `{UrlFrontend sin la barra final}/historial/{idCarga}`.

## Abiertos

1. La presentación en Mailpit: vista HTML y vista de texto. Se verifica en la implementación.
