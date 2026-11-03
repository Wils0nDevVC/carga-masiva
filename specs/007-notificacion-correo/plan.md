# Implementation Plan: Notificación por correo en ms-notificaciones

**Branch**: `007-notificacion-correo` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: `specs/007-notificacion-correo/spec.md`

**Procedencia**: `Dec` = decisión del usuario o constitución v3.3.0; `Spec`; `F004/F006`; `Plan` = decisión técnica (detalle en `research.md`); `Verif` = comprobado en esta etapa.

## Summary

ms-notificaciones consume `notificaciones` con un `BackgroundService` de a un mensaje y con confirmación manual. Por cada mensaje válido lee el estado de la carga; si está Finalizado, envía con MailKit el correo aprobado ("Carga {idCarga} finalizada", texto plano con `idCarga` y `fechaFin`) y, tras el envío, la pasa a Notificado con `usp_CambiarEstadoCarga` y origen esperado Finalizado. Envío agotado o permanente → `ErrorNotificacion`, Finalizado, mensaje confirmado. Notificado no guardado tras enviar → sin confirmar (posible duplicado documentado). Circuito abierto → sin confirmar y espera. [Spec] [Dec Q1–Q3] [Plan]

## Technical Context

**Language/Version**: C# / .NET 10. **Primary Dependencies**: existentes (MediatR 12.5.0, FluentValidation 12.2.0, EF Core SqlServer 10.0.14, Serilog); nuevas: MailKit 4.18.2 (MIT), RabbitMQ.Client 7.2.2, Polly.Core 8.9.0 (Infrastructure), Logging.Abstractions 10.0.14 (Application). [Verif NuGet]
**Storage**: `CargaMasivaApp` sin cambios de esquema. **Testing**: manual (`quickstart.md`), SMTP de pruebas fuera de Compose. **Target Platform**: Docker Compose; SQL Server amd64 emulado en este equipo. **Project Type**: microservicio; toca `ms-notificaciones/`, `compose.yaml`, `.env.example`. **Constraints**: sin cerrojo (no exigido), sin Outbox, plantillas ni servicios nuevos; sin entrega exactamente una vez.

## Constitution Check

Constitución v3.3.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo lo de la spec; sin endpoints, Autenticación ni Gateway | Pasa |
| §3/§4 Plataforma y capas | `BackgroundService` en Api; caso de uso con MediatR en Application; MailKit, RabbitMQ, Polly y EF en Infrastructure; `CargaArchivo` (`Id`, `Estado`) en Domain | Pasa |
| §6 Persistencia | EF + procedimiento por EF; sin cambios de esquema; entidad por ingeniería inversa con columnas mínimas | Pasa |
| §8 Estados y correo | Notificado solo tras envío; contenido y reintentos según las cláusulas de la 007; Finalizado ante fallos; sin Fallido | Pasa |
| §9 Integraciones | MailKit por entorno; Polly con reintentos limitados y circuito; circuito abierto no terminal (F006 FR-016a) | Pasa |
| §10 Logging | Sin credenciales SMTP, contraseñas ni cadenas de conexión | Pasa |
| §11/§12 | Variables en Compose, sin servicio SMTP nuevo; sin tests; dependencias verificadas | Pasa |

Tras Phase 1: sin violaciones.

## Project Structure

```text
specs/007-notificacion-correo/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/consumo-notificaciones.md
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks (no creado aquí)

compose.yaml, .env.example                                   # ms-notificaciones: RabbitMq__*, Smtp__*, depends_on rabbitmq

ms-notificaciones/src/
├── Acity.Api/{Program.cs, appsettings.json, Consumers/ConsumidorNotificaciones.cs}
├── Acity.Application/
│   ├── Acity.Application.csproj                             # + Logging.Abstractions
│   ├── Notificaciones/Commands/NotificarCarga/{NotificarCargaCommand, NotificarCargaCommandHandler}.cs
│   └── Interfaces/Services/{IEnvioCorreo, IProcedimientosCarga, ICargasConsulta, DependenciaNoDisponibleException, EnvioCorreoFallidoException}.cs
├── Acity.Domain/Entities/CargaArchivo.cs                    # Id, Estado
└── Acity.Infrastructure/
    ├── Acity.Infrastructure.csproj                          # + MailKit, RabbitMQ.Client, Polly.Core, FrameworkReference
    ├── DependencyInjection.cs
    ├── Persistence/{Configurations/CargaArchivoConfiguration, ProcedimientosCarga, CargasConsulta}.cs
    ├── Mail/{SmtpOptions, EnvioCorreoMailKit}.cs
    ├── Messaging/{RabbitMqOptions, RabbitMqConexion, ConsumidorRabbitMq}.cs
    └── Resilience/PipelinesResiliencia.cs                   # smtp y sql
```

Los demás servicios, el esquema y la constitución no cambian.

## Complexity Tracking

| Elemento | Por qué | Alternativa descartada |
|---|---|---|
| `CargaArchivo` en ms-notificaciones | Comprobar Finalizado antes de enviar (contrato de la 006; Q2) | Enviar sin comprobar (reenvíos deliberados) |
| `EnvioCorreoFallidoException` | Distinguir en Application el fallo definitivo del envío (→ `ErrorNotificacion`) del fallo al guardar Notificado | Capturar excepciones de MailKit en Application (rompe capas) |
| Copia de `ConsumidorRabbitMq`, `RabbitMqConexion` y pipelines de la 006 | Servicios independientes sin biblioteca compartida (F002) | Biblioteca común (prohibida) |

## Pendientes tras el plan

Ninguno que requiera decisión. Se comprueban en la implementación: clasificación de excepciones de MailKit y servidor SMTP de pruebas (research "Abiertos"); validación en el equipo de evaluación sin realizar.
