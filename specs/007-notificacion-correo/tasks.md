# Tasks: Notificación por correo en ms-notificaciones

**Input**: `specs/007-notificacion-correo/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Alcance**: `ms-notificaciones/`, `compose.yaml`, `.env.example`; verificación fuera del repositorio. No se tocan los otros servicios, el esquema, `referencias/` ni la constitución. Sin pruebas automatizadas (constitución §11).

**Reglas de ejecución**
- **Ingeniería inversa manual** (T006): la ejecuta el usuario (o Claude con autorización expresa); salida fuera del repositorio.
- Datos de prueba solo en `CargaMasivaApp_F007`; sin correos reales (servidor SMTP de pruebas fuera de Compose); secretos solo por entorno.
- Marcar `[x]` solo lo ejecutado y verificado; una tarea `[x]` no es evidencia de escenarios no probados.
- Al cerrar, entregar el comando de commit (sin `Co-Authored-By`).

**Formato**: `<api>` = `ms-notificaciones/src/Acity.Api`, `<app>` = `ms-notificaciones/src/Acity.Application`, `<dom>` = `ms-notificaciones/src/Acity.Domain`, `<inf>` = `ms-notificaciones/src/Acity.Infrastructure`.

## Phase 1: Setup

- [ ] T001 Registrar la línea base en `specs/007-notificacion-correo/quickstart.md` ("Resultados"): `git status`, `dotnet build ms-notificaciones/src/Acity.Notificaciones.slnx` con 0 advertencias, `docker compose ps`
- [ ] T002 [P] Añadir a `<inf>/Acity.Infrastructure.csproj` `MailKit` 4.18.2, `RabbitMQ.Client` 7.2.2, `Polly.Core` 8.9.0 y `<FrameworkReference Include="Microsoft.AspNetCore.App" />`; a `<app>/Acity.Application.csproj` `Microsoft.Extensions.Logging.Abstractions` 10.0.14; `dotnet build` con 0 advertencias
- [ ] T003 [P] Añadir a `.env.example` `SMTP_HOST=`, `SMTP_PUERTO=587`, `SMTP_SEGURIDAD=StartTls`, `SMTP_USUARIO=`, `SMTP_CLAVE=`, `SMTP_REMITENTE=` (sin valores secretos)
- [ ] T004 Modificar `ms-notificaciones` en `compose.yaml`: `depends_on` añade `rabbitmq: service_healthy`; `environment` añade `RabbitMq__Host: rabbitmq`, `RabbitMq__Puerto: "5672"`, `RabbitMq__Usuario/Clave` desde `RABBITMQ_*`, `Smtp__Host: ${SMTP_HOST:?…}`, `Smtp__Puerto: ${SMTP_PUERTO:-587}`, `Smtp__Seguridad: ${SMTP_SEGURIDAD:-StartTls}`, `Smtp__Usuario: ${SMTP_USUARIO:-}`, `Smtp__Clave: ${SMTP_CLAVE:-}`, `Smtp__Remitente: ${SMTP_REMITENTE:?…}`; sin servicio SMTP nuevo; `docker compose config -q`
- [ ] T005 [P] Añadir a `<api>/appsettings.json` `"RabbitMq": { "Puerto": 5672 }` y `"Smtp": { "Puerto": 587, "Seguridad": "StartTls" }` (sin secretos)

## Phase 2: Foundational

- [ ] T006 **Manual**: ingeniería inversa de `dbo.CargaArchivo` (procedimiento de F004, desde `ms-control/`, salida fuera del repositorio)
- [ ] T007 [P] Crear `<dom>/Entities/CargaArchivo.cs` con solo `Id` (`long`, BIGINT PK) y `Estado` (`string`, VARCHAR(20) NOT NULL) y `<inf>/Persistence/Configurations/CargaArchivoConfiguration.cs` (`ToTable("CargaArchivo")`, `HasKey`, `Estado` `HasMaxLength(20).IsUnicode(false)`); sin relaciones nuevas (research D-06)
- [ ] T008 [P] Crear en `<app>/Interfaces/Services/`: `IEnvioCorreo` (`Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken)`), `EnvioCorreoFallidoException` (fallo definitivo: reintentos agotados o error permanente, con motivo sin credenciales), `DependenciaNoDisponibleException` (operación, `RetryAfter`), `ICargasConsulta` (`Task<string?> ObtenerEstadoAsync(long idCarga, CancellationToken)`, `Task RegistrarErrorNotificacionAsync(long idCarga, string motivo, CancellationToken)`), `IProcedimientosCarga` (`Task<bool> CambiarEstadoAsync(long cargaId, string nuevoEstado, string? estadoEsperado, CancellationToken)`)
- [ ] T009 Crear `<inf>/Resilience/PipelinesResiliencia.cs` con los pipelines `smtp` y `sql` de research D-05 (reintento → circuit breaker propio 0,5/5/30 s/30 s → tiempo de espera; `smtp`: 3 reintentos, 30 s, reintentables `SocketException`, `IOException`, `ServiceNotConnectedException`, `SmtpProtocolException`, `SmtpCommandException` 4xx, `TimeoutRejectedException`; `sql`: 3 reintentos, 30 s, números transitorios de la 006, `TimeoutException`, `TimeoutRejectedException`, **excluyendo `BrokenCircuitException`**) y el método `EjecutarAsync` que traduce `BrokenCircuitException` a `DependenciaNoDisponibleException`
- [ ] T010 Crear `<inf>/Persistence/ProcedimientosCarga.cs` (`usp_CambiarEstadoCarga` con `@EstadoEsperado`, parámetros con nombre, pipeline `sql`) y `<inf>/Persistence/CargasConsulta.cs` (lectura de `Estado` con pipeline `sql`; inserción de `Incidencia` `Tipo = "ErrorNotificacion"`, `CargaPeriodoId`/`NumeroFila`/`CodigoProducto`/`PeriodoTexto` NULL, `Motivo` NVARCHAR(500), `FechaRegistro` UTC, **sin reintentos**)
- [ ] T011 Crear `<inf>/Mail/SmtpOptions.cs` (`Host`, `Puerto`, `Seguridad` ∈ {None, StartTls, SslOnConnect, Auto}, `Usuario`, `Clave`, `Remitente`; `ValidateOnStart` según research D-07) y `<inf>/Mail/EnvioCorreoMailKit.cs` (`IEnvioCorreo`): `MimeMessage` de `Remitente` a un solo destinatario, `TextPart("plain")` UTF-8; `SmtpClient` por envío en `using`: `ConnectAsync`, `AuthenticateAsync` solo con usuario, `SendAsync`, `DisconnectAsync(true)`, dentro del pipeline `smtp`; reintentos agotados o error permanente (`AuthenticationException`, `SmtpCommandException` 5xx, `ParseException`) → `EnvioCorreoFallidoException`; circuito abierto → `DependenciaNoDisponibleException`; sin credenciales en logs
- [ ] T012 Crear `<inf>/Messaging/RabbitMqOptions.cs`, `RabbitMqConexion.cs` y `ConsumidorRabbitMq.cs` siguiendo los de ms-carga-masiva (006) con la topología `notificaciones` (intercambio `direct` durable, cola durable, clave `notificaciones`), `prefetchCount = 1`, confirmación manual y espera del mensaje en curso
- [ ] T013 Registrar todo en `<inf>/DependencyInjection.cs` (opciones con validación, pipelines singleton con clave, `IEnvioCorreo`, `ICargasConsulta`, `IProcedimientosCarga` scoped, conexión y consumidor singleton); `dotnet build` con 0 advertencias

## Phase 3: US1 - Notificar una carga finalizada (P1) 🎯 MVP

**Meta**: carga Finalizado → correo aprobado → Notificado con historial. **Prueba**: quickstart escenarios 1 y 2.

- [ ] T014 [US1] Crear `<app>/Notificaciones/Commands/NotificarCarga/NotificarCargaCommand.cs` (`IdCarga`, `Usuario`, `FechaFin`) y `NotificarCargaCommandHandler.cs` (research D-03): leer estado; si no es `Finalizado` → descartar con log; armar asunto `Carga {IdCarga} finalizada` y cuerpo `La carga {IdCarga} finalizó.` + `Fecha de fin (UTC): {FechaFin:yyyy-MM-dd HH:mm:ss}` (sin "sin errores", sin resúmenes); enviar; `CambiarEstadoAsync(IdCarga, "Notificado", "Finalizado")` (`false` → log de advertencia)
- [ ] T015 [US1] Crear `<api>/Consumers/ConsumidorNotificaciones.cs` (`BackgroundService`): deserializar `{ idCarga, usuario, fechaFin }` (web), validar y enviar el comando en un ámbito nuevo; ante excepción, esperar `RetryAfter` (o 30 s; mínimo 10 s) si es `DependenciaNoDisponibleException`, 10 s en otro caso; registrar con `AddHostedService` en `<api>/Program.cs`
- [ ] T016 [US1] Ejecutar los escenarios 1 y 2 de `quickstart.md` con el servidor SMTP de pruebas; registrar resultados

## Phase 4: US2 - Reentregas y mensajes que no corresponden (P1)

**Meta**: sin correos para cargas no Finalizado ni mensajes inválidos. **Prueba**: escenarios 3 a 5.

- [ ] T017 [US2] Completar la validación del mensaje en `<api>/Consumers/ConsumidorNotificaciones.cs` (JSON inválido, `idCarga` ≤ 0, `usuario` vacío, `fechaFin` inválida → log de error y confirmar sin comando) y los logs de descarte por estado en el handler
- [ ] T018 [US2] Ejecutar los escenarios 3 a 5; registrar resultados

## Phase 5: US3 - Fallo de envío (P1)

**Meta**: agotado o permanente → `ErrorNotificacion`, Finalizado, confirmado; incidencia no guardada → sin confirmar; circuito abierto → sin confirmar. **Prueba**: escenarios 6 a 8.

- [ ] T019 [US3] En `NotificarCargaCommandHandler.cs`: capturar `EnvioCorreoFallidoException` → `RegistrarErrorNotificacionAsync` y terminar sin excepción (confirmar); si el registro falla, dejar subir (sin confirmar); `DependenciaNoDisponibleException` sube sin incidencia
- [ ] T020 [US3] Ejecutar los escenarios 6 a 8 (SMTP detenido; transitorio; destinatario rechazado si el servidor lo permite) y comprobar la clasificación real de excepciones de MailKit (abierto 1); registrar resultados

## Phase 6: US4 - Notificado no guardado tras enviar (P2)

**Meta**: sin `ErrorNotificacion` ni Fallido; sin confirmar; la reentrega consulta el estado. **Prueba**: escenario 9.

- [ ] T021 [US4] En `NotificarCargaCommandHandler.cs`: si `CambiarEstadoAsync` falla tras sus reintentos (o por circuito abierto), log de error "correo enviado; Notificado no registrado" con `IdCarga` y dejar subir la excepción (sin confirmar); sin incidencia
- [ ] T022 [US4] Ejecutar el escenario 9 (bloqueo de la fila de la carga desde otra sesión en la base temporal hasta agotar los reintentos del procedimiento) y registrar el posible segundo correo como limitación observada

## Phase 7: Polish

- [ ] T023 Escenario 10: `grep -F` de `SMTP_CLAVE`, `RABBITMQ_PASSWORD`, `MSSQL_SA_PASSWORD` y `Password=` en los logs de ms-notificaciones = 0
- [ ] T024 Revisar `git diff`: solo `ms-notificaciones/`, `compose.yaml`, `.env.example` y `specs/007-…`; sin endpoints de negocio, Worker ni pruebas
- [ ] T025 Cierre: eliminar `CargaMasivaApp_F007` y el servidor SMTP de pruebas, purgar la cola, arranque normal `healthy`; completar "Resultados" de `quickstart.md` (ejecutado / parcial / no ejecutado) y "Abiertos" de `research.md`
- [ ] T026 Revisión final y comando de commit sin `Co-Authored-By`

## Dependencias y orden

Setup (T001–T005) → Foundational (T006–T013) → US1 (T014–T016) → US2 (T017–T018) → US3 (T019–T020) → US4 (T021–T022) → Polish (T023–T026). T007 depende de T006; T010–T012 de T009; T013 de T007–T012; T015 de T014.

## Paralelismo

T002, T003, T005 (Setup); T007, T008 (Foundational).

## Estrategia

MVP: Setup + Foundational + US1. Luego US2 y US3 (P1), US4 (P2) y Polish.
