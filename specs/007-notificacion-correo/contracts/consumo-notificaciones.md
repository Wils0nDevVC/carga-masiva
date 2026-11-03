# Contrato: consumo de `notificaciones` y correo (ms-notificaciones)

Procedencia: README §2, §3 paso 4, §4 (Correo); constitución §8 v3.3.0, §9; F006 `contracts/mensaje-notificaciones.md`; spec 007; `research.md` D-02–D-05.

## Topología (mismos argumentos que el publicador)

Intercambio `notificaciones` (`direct`, durable); cola `notificaciones` (durable, no exclusiva, sin autoborrado, sin argumentos); clave `notificaciones`; `prefetchCount = 1`; confirmación manual.

## Mensaje de entrada (F006)

`{ "idCarga": <long>, "usuario": "<correo>", "fechaFin": "<UTC ISO 8601>" }`

## Correo [Decisión Q1]

| Campo | Valor |
|---|---|
| De | `Smtp__Remitente` |
| Para | `usuario` del mensaje (único destinatario) |
| Asunto | `Carga {idCarga} finalizada` |
| Cuerpo (texto plano) | `La carga {idCarga} finalizó.` / `Fecha de fin (UTC): {fechaFin:yyyy-MM-dd HH:mm:ss}` |

Sin adjuntos, plantillas, resúmenes ni copia a terceros. No afirma que la carga terminó sin errores.

## Decisión por mensaje

| Situación | Acción | Confirmación |
|---|---|---|
| Mensaje inválido, carga inexistente o no Finalizado (incluye Notificado) | log, sin correo ni cambios | ack (descartar) |
| Correo enviado y Notificado registrado (`Cambiado = 1`) | — | ack |
| Correo enviado y el procedimiento devuelve 0 | log de advertencia | ack |
| Correo enviado y Notificado no registrado tras los reintentos | log "correo enviado; Notificado no registrado" | **sin ack**; en la reentrega se consulta el estado (posible duplicado documentado) |
| Envío agotado o error permanente | `ErrorNotificacion`; carga Finalizado | ack (sin recuperación posterior) |
| Envío agotado y la incidencia no se guarda | log de error | **sin ack**; espera 10 s |
| Circuito SMTP o SQL abierto | sin incidencia ni cambios | **sin ack**; espera `RetryAfter` (o 30 s; mínimo 10 s) |

## Garantías y límites

No hay entrega exactamente una vez: un reintento tras perder la respuesta del servidor, o la reentrega cuando Notificado no se guardó, pueden enviar un segundo correo. Nunca se reenvía deliberadamente a una carga Notificado.
