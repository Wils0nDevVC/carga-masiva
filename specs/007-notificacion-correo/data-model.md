# Data Model: Notificación por correo en ms-notificaciones

Sin cambios de esquema (V001–V004).

## Entidades de ms-notificaciones tras esta feature

| Entidad | Origen | Columnas mapeadas | Escribe | Lee |
|---|---|---|---|---|
| `CargaArchivo` | **nueva** (ingeniería inversa, research D-06) | `Id` BIGINT PK, `Estado` VARCHAR(20) NOT NULL | nunca (el estado lo cambia el procedimiento) | `Estado` |
| `Incidencia` | F004 | todas | `ErrorNotificacion` | — |

## Incidencia `ErrorNotificacion`

| Columna | Valor |
|---|---|
| `CargaId` | `idCarga` |
| `CargaPeriodoId`, `NumeroFila`, `CodigoProducto`, `PeriodoTexto` | NULL |
| `Tipo` | `ErrorNotificacion` (CHECK existente) |
| `Motivo` (NVARCHAR(500)) | "No se pudo enviar el correo de notificación tras los reintentos: {clase de error}." o "…: error permanente del servidor ({código})." — sin credenciales ni direcciones del servidor |
| `FechaRegistro` | UTC |

## Recorridos

| Situación | Estado | Historial | Incidencia | Mensaje |
|---|---|---|---|---|
| Envío correcto | Finalizado → Notificado | + Notificado | — | confirmado |
| Carga no Finalizado (incluye ya Notificado) o mensaje inválido | sin cambios | — | — | confirmado (descartado) |
| Envío agotado o permanente | Finalizado | — | `ErrorNotificacion` | confirmado |
| Envío agotado y la incidencia no se guarda | Finalizado | — | — | sin confirmar (espera 10 s) |
| Circuito SMTP o SQL abierto | sin cambios | — | — | sin confirmar (espera `RetryAfter`) |
| Correo enviado, Notificado no se guarda | Finalizado | — | — | sin confirmar; en la reentrega se consulta el estado |
| Correo enviado, procedimiento devuelve 0 | el que tenga | — | — | confirmado |
