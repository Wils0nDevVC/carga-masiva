# Research: Notificación por correo en ms-notificaciones

**Procedencia**: `Dec` = decisión del usuario o constitución v3.4.0; `Spec` = spec 007; `F001/F004/F005/F006` = features anteriores; `Plan` = decisión técnica de este plan (alternativa más sencilla; sin aprobación individual); `Verif` = comprobado en esta etapa; `Sin verif` = se comprueba en la implementación.

## D-01 Paquetes

| Paquete | Versión | Proyecto | Licencia | Comprobación |
|---|---|---|---|---|
| `MailKit` | 4.18.2 (arrastra MimeKit 4.18.2) | Infrastructure | MIT | Verif (NuGet: estable más reciente, net10.0, publicado 2026-10-23) |
| `RabbitMQ.Client` | 7.2.2 | Infrastructure | Apache-2.0 OR MPL-2.0 | Verif (igual que 005/006) |
| `Polly.Core` | 8.9.0 | Infrastructure | BSD-3-Clause | Verif (igual que 005/006) |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.14 | Application | MIT | Verif (igual que 006) |

`FrameworkReference Microsoft.AspNetCore.App` en Infrastructure (enlace de opciones), como en 005/006. Sin otros paquetes. [Dec §12] [Plan]

## D-02 Consumidor

- `Acity.Api/Consumers/ConsumidorNotificaciones.cs` (`BackgroundService`): bucle consumir → ante excepción, cerrar el canal sin confirmar, esperar y repetir. Espera: `RetryAfter` del circuito si la causa es `DependenciaNoDisponibleException` (o 30 s; mínimo 10 s), 10 s en otro caso. Sin reentrega inmediata en bucle. [Spec FR-001, FR-008] [F006 D-02, FR-016a] [Plan]
- `Acity.Infrastructure/Messaging/ConsumidorRabbitMq.cs`: igual que en la 006 (topología declarada por el consumidor, `prefetchCount = 1`, confirmación manual, espera del mensaje en curso antes de volver). Topología: intercambio directo durable `notificaciones`, cola durable `notificaciones`, clave `notificaciones` (F006 `mensaje-notificaciones.md`). [F006]
- **Sin cerrojo de exclusividad**: la constitución solo lo exige para el consumidor de carga masiva; una instancia en Compose; el descarte por estado evita reenvíos deliberados. [Dec §9] [Spec Edge Cases]
- Cada mensaje en un ámbito de DI nuevo con `IMediator.Send(new NotificarCargaCommand(...))`. [Dec §3, §4]

## D-03 Caso de uso (`NotificarCargaCommandHandler`)

1. Validar el mensaje: `idCarga` > 0, `usuario` no vacío, `fechaFin` fecha ISO 8601 válida. Si no → descartar con log de error (confirmar). [Spec FR-002]
2. Leer `CargaArchivo.Estado` (pipeline SQL). Inexistente o distinto de `Finalizado` → descartar con log (confirmar). Incluye la reentrega de Q2: si ya está Notificado, se confirma sin reenviar. [Spec FR-002, FR-009] [Dec Q2]
3. Enviar el correo (D-05, pipeline SMTP).
   - Éxito → paso 4.
   - Agotamiento de reintentos o error permanente → insertar `ErrorNotificacion` (sin reintento; motivo con la clase de error, sin credenciales) y **confirmar**. Si la inserción falla → la excepción sube: **sin confirmar**, espera de 10 s. [Dec Q3]
   - Circuito abierto → `DependenciaNoDisponibleException`: sin incidencia, **sin confirmar**, espera `RetryAfter`. [F006 FR-016a]
4. `usp_CambiarEstadoCarga(idCarga, 'Notificado', @EstadoEsperado = 'Finalizado')` (pipeline SQL; idempotente).
   - `Cambiado = 1` → confirmar.
   - `Cambiado = 0` (la carga ya no estaba Finalizado: otra ejecución la notificó) → log de advertencia y confirmar.
   - Falla tras los reintentos (o circuito abierto) → log "correo enviado; Notificado no registrado" con `idCarga` y la excepción sube: **sin confirmar** (Dec Q2). En la reentrega el paso 2 decide.
- Sin `ErrorNotificacion` ni Fallido en el paso 4. [Spec FR-009] [Dec Q2]

## D-04 Contenido del correo [Dec Q1]

- Remitente: `Smtp:Remitente`. Destinatario: `usuario` del mensaje (único).
- Asunto: `Carga {idCarga} finalizada`.
- Cuerpo (texto plano, UTF-8):
  ```
  La carga {idCarga} finalizó.
  Fecha de fin (UTC): {fechaFin:yyyy-MM-dd HH:mm:ss}
  ```
  Sin resúmenes, consultas, adjuntos ni plantillas; no afirma "sin errores". [Spec FR-005] [Plan formato de fecha]

## D-05 Envío con MailKit y Polly

- `SmtpClient` por envío (`using`): `ConnectAsync(Host, Puerto, Seguridad)`, `AuthenticateAsync` solo si hay usuario, `SendAsync`, `DisconnectAsync(true)`. Seguridad: `None`, `StartTls`, `SslOnConnect` o `Auto` (`SecureSocketOptions`). [Plan]
- Pipeline `smtp` (singleton): 3 reintentos exponencial 1 s con jitter → circuit breaker propio (0,5 / 5 / 30 s / 30 s) → tiempo de espera 30 s por intento; traducción de `BrokenCircuitException` a `DependenciaNoDisponibleException` (como en la 006).
- **Reintentables** (transitorios): `SocketException`, `IOException`, `ServiceNotConnectedException`, `SmtpProtocolException`, `SmtpCommandException` con estado 4xx (421, 450, 451, 452), `TimeoutRejectedException`. **Permanentes** (no se reintentan): `AuthenticationException`, `SmtpCommandException` con estado 5xx (p. ej. 550 destinatario rechazado), dirección inválida (`ParseException`). Clasificación exacta por comprobar [Sin verif].
- **Riesgo de duplicado del reintento**: si el servidor aceptó el mensaje pero se perdió la respuesta (tiempo de espera tras `DATA`), el reintento puede enviar otro correo; aceptado como limitación (Spec FR-010).
- Pipeline `sql` para la lectura del estado y el procedimiento: 3 reintentos, números transitorios de la 006 (excluyendo `BrokenCircuitException`), circuito propio, 30 s.

## D-06 Entidad `CargaArchivo` en ms-notificaciones

Ingeniería inversa de `dbo.CargaArchivo` (procedimiento de F004, manual) e integración parcial: `Id` (BIGINT PK) y `Estado` (VARCHAR(20)). Solo lectura. Sin relación con `Incidencia` (ya mapeada sin relaciones en F004: `CargaId` solo como columna). Justificación: Spec FR-003. [F004]

## D-07 Configuración

| Opción | Variable | Predeterminado | Validación al arrancar |
|---|---|---|---|
| `Smtp.Host` | `Smtp__Host` | — | no vacío |
| `Smtp.Puerto` | `Smtp__Puerto` | 587 | > 0 |
| `Smtp.Seguridad` | `Smtp__Seguridad` | `StartTls` | uno de `None`, `StartTls`, `SslOnConnect`, `Auto` |
| `Smtp.Usuario` / `Smtp.Clave` | `Smtp__Usuario`, `Smtp__Clave` | vacíos (sin autenticación) | si hay usuario, clave no vacía |
| `Smtp.Remitente` | `Smtp__Remitente` | — | dirección válida |
| `RabbitMq.*` | `RabbitMq__*` | puerto 5672 | no vacías |

Compose (`ms-notificaciones`): `depends_on` `rabbitmq: service_healthy` y `mailpit: service_started`; variables desde `.env` (`SMTP_HOST`, `SMTP_PUERTO`, `SMTP_SEGURIDAD`, `SMTP_USUARIO`, `SMTP_CLAVE`, `SMTP_REMITENTE`). Cambiar de servidor SMTP = cambiar esas variables, sin tocar código. [Spec FR-012]

## D-08 Mailpit como SMTP local de pruebas y demostración [Decisión del usuario, 2026-11-03; constitución §11 v3.4.0; no es requisito del README]

| Aspecto | Valor | Procedencia |
|---|---|---|
| Imagen | `axllent/mailpit:v1.31.6` (oficial; publicada 2026-10-15; manifiestos linux/amd64 y linux/arm64 comprobados con `docker manifest inspect`, sin descargarla). Se elige frente a v1.33.0 (publicada 2026-10-31, 3 días) por tener más de dos semanas | Verif / Plan |
| Servicio en Compose | `mailpit`, sin volumen (los correos capturados son de prueba y pueden perderse al recrear el contenedor), `restart: "no"`, en la red por defecto de Compose | Plan |
| Conexión desde ms-notificaciones | por nombre de servicio: `SMTP_HOST=mailpit`, `SMTP_PUERTO=1025` (SMTP de Mailpit, sin publicar en el host) | Dec + Plan |
| Seguridad | `SMTP_SEGURIDAD=None`, sin `SMTP_USUARIO` ni `SMTP_CLAVE`: **solo** para estas pruebas locales; un servidor real se configura con `StartTls`/`SslOnConnect` y credenciales por entorno | Dec |
| Remitente local | `SMTP_REMITENTE=notificaciones@carga-masiva.test` (dominio reservado `.test`) | Dec |
| Interfaz web | `8025:8025` en el host (puerto libre en este equipo; no choca con 8081–8084, 8888, 14333 ni 15672) | Verif / Plan |
| Lectura de correos capturados | API HTTP de Mailpit (`/api/v1/messages`, `/api/v1/message/{id}`) o la interfaz web | Sin verif (comprobar en la implementación) |
| Fallos SMTP simulados | detener el contenedor `mailpit` (transitorio o agotamiento); la función **Chaos** de Mailpit (errores SMTP configurables) para el error permanente, si se confirma su activación en esta versión | Sin verif |
| Entrega externa | Mailpit **no** entrega a buzones externos (no se configura relay); la entrega real queda fuera de esta feature | Dec |

`.env` (no versionado) recibe los valores locales de la tabla; `.env.example` los documenta como ejemplo local, sin secretos. No se añaden bibliotecas, endpoints ni más infraestructura.

## Abiertos

1. Clasificación exacta de excepciones de MailKit 4.18 (D-05).
2. API de Mailpit para leer correos y activación de Chaos en v1.31.6 (D-08).
3. Validación en el equipo de evaluación: no realizada.
