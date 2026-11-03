# Quickstart: validación manual de la notificación por correo

Verificación manual (constitución §11). Contrato en `contracts/consumo-notificaciones.md`; recorridos en `data-model.md`. **Ningún escenario se ha ejecutado todavía.**

## Reglas

- Datos de prueba solo en una base temporal `CargaMasivaApp_F007` creada por el runner; `CargaMasiva`, `CargaMasivaDb` y los volúmenes ajenos no se tocan.
- Correos a **Mailpit** (`axllent/mailpit:v1.31.6`, servicio de Compose; decisión del usuario, 2026-11-03): los captura y los muestra en `http://localhost:8025`, sin entregarlos a buzones externos.
- Niveles: **(a) sustitutos** (mensajes publicados a mano, cargas preparadas por SQL, Mailpit detenido o Chaos para fallos); **(b) SMTP local real** (MailKit → Mailpit); **(c) entrega externa**: fuera de esta feature.
- Secretos solo por entorno; de `.env` se cargan solo las variables necesarias (no `MSSQL_SA_PASSWORD`).

## Preparación

1. Base temporal con V001–V004 y un usuario de prueba.
2. Mailpit en marcha con Compose; en `.env`: `SMTP_HOST=mailpit`, `SMTP_PUERTO=1025`, `SMTP_SEGURIDAD=None`, sin usuario, `SMTP_REMITENTE=notificaciones@carga-masiva.test`.
3. `DB_NAME=CargaMasivaApp_F007 docker compose up -d --build ms-notificaciones` (y `ms-control`, `ms-carga-masiva` para el escenario de punta a punta).
4. Cargas de prueba en Finalizado, Notificado, Fallido y Pendiente (por SQL o subiendo un archivo).

## Escenarios

| # | Escenario | Pasos | Resultado esperado | Spec |
|---|---|---|---|---|
| 1 | Envío correcto (nivel b) | Publicar el mensaje de una carga Finalizado | Mailpit captura un correo de `notificaciones@carga-masiva.test` a `usuario`, asunto "Carga {id} finalizada" y cuerpo del contrato; Notificado con una entrada de historial | US1-1, US1-3 |
| 2 | Punta a punta | Subir el Excel de ejemplo con Control | la carga llega a Notificado; un correo | US1-1, US1-2 |
| 3 | Ya Notificado | Volver a publicar el mensaje del escenario 1 | sin correo ni cambios; descartado | US2-1 |
| 4 | Duplicado | Publicar dos veces el mensaje de una carga Finalizado | un correo; el segundo descartado | US2-2 |
| 5 | Estados no válidos e inválidos | Mensajes de cargas Fallido, Pendiente, inexistente; `{}`; `usuario` vacío | sin correos; descartados con log | US2-3, US2-4 |
| 6 | SMTP caído | Detener `mailpit` y publicar | reintentos en el log; `ErrorNotificacion`; Finalizado; mensaje confirmado; sin nuevo intento posterior | US3-1 |
| 7 | Transitorio | Detener `mailpit` y arrancarlo dentro de la ventana de reintentos | un correo; Notificado; sin `ErrorNotificacion` | US3-3 |
| 8 | Error permanente | Error SMTP 5xx con la función Chaos de Mailpit (si se confirma su activación) | sin reintentos; `ErrorNotificacion`; Finalizado | US3-4 |
| 9 | Notificado no guardado | Tras enviar, impedir el procedimiento (p. ej. bloquear la carga desde otra sesión hasta agotar los reintentos) | log "correo enviado; Notificado no registrado"; sin confirmar; en la reentrega: si sigue Finalizado, segundo correo (duplicado documentado) y Notificado | US4-1 a US4-3 |
| 10 | Logs | `grep -F` de `SMTP_CLAVE`, `RABBITMQ_PASSWORD`, `Password=` | 0 coincidencias | SC-005 |

## Cierre

Eliminar la base temporal; borrar los correos de prueba de Mailpit; purgar la cola; arranque normal y todo `healthy`. Registrar en "Resultados" lo ejecutado y lo no ejecutado.

## Resultados (implementación, 2026-11-03)

**Estado: implementada, NO completamente verificada.** Mailpit aprobado (2026-11-03) como SMTP local; su integración (T027–T029) y la verificación (T016–T025, T030) están pendientes.

| Tipo de comprobación | Estado | Detalle |
|---|---|---|
| Compilación | **Hecha** | `dotnet build ms-notificaciones/src/Acity.Notificaciones.slnx`: 0 advertencias, 0 errores |
| Configuración y arranque | **Hecha** (con los valores SMTP de prueba ya retirados) | `docker compose config -q` correcto; ms-notificaciones arrancó `healthy` y registró "Consumiendo la cola notificaciones". Sin `SMTP_HOST` ni `SMTP_REMITENTE` en `.env`, Compose no arranca (comportamiento previsto) |
| Mapeo de `CargaArchivo` (T006) | **Hecha** | Reutilizado de ms-control y verificado contra el catálogo actual de `CargaMasivaApp`: `Id` bigint NOT NULL PK, `Estado` varchar(20) NOT NULL; 0 diferencias |
| (a) Pruebas con sustitutos (mensajes a mano, cargas por SQL, fallos simulados) | **No ejecutadas** | Escenarios 3 a 10 sin ejecutar |
| (b) SMTP local real (MailKit → Mailpit, Notificado e historial) | **No ejecutado** | Escenarios 1 y 2 y T030, tras integrar Mailpit (T027–T029) |
| (c) Entrega a buzones externos | **Fuera de alcance** | Mailpit no entrega a buzones externos; no se verifica en esta feature |

- **Implementado**: paquetes (MailKit 4.18.2, RabbitMQ.Client 7.2.2, Polly.Core 8.9.0, Logging.Abstractions 10.0.14, `FrameworkReference`), `CargaArchivo` (`Id`, `Estado`), pipelines `smtp` y `sql` (reintento → circuito propio → tiempo de espera; sin reintentar circuito abierto), procedimiento con `@EstadoEsperado = 'Finalizado'`, consulta de estado, `ErrorNotificacion` sin reintentos, envío con MailKit (texto plano, un destinatario), consumidor con confirmación manual y espera tras fallo, Compose y `.env.example` (variables documentadas con ejemplos identificados como tales).
- **Entorno**: los valores SMTP de prueba que se habían añadido a `.env` sin autorización se retiraron; no se añadió ningún servidor SMTP.
- Ningún resultado de los escenarios 1 a 10 se declara.
