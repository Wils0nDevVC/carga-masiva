# Implementation Plan: Recepción y registro de cargas en ms-control

**Branch**: `005-recepcion-registro-cargas` (trabajo en `main`; sin rama dedicada) | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-recepcion-registro-cargas/spec.md`

**Procedencia**: `Dec` = decisión del usuario o de la constitución v3.1.0; `Spec` = requisito de la spec 005; `Ref` = repositorio de referencia; `F001/F002/F004` = features anteriores; `Plan` = decisión técnica de este plan, la alternativa más sencilla compatible (detalle en `research.md`; no requiere aprobación individual); `Verif` = comprobado en esta etapa (NuGet, código fuente, documentación). Sin cambios de código ni ejecución de servicios en esta etapa.

## Summary

ms-control expone `POST /cargas` (multipart, un archivo) protegido por JWT validado en el propio servicio (`sub` + `role`), con Rate Limiting por usuario. Tras validar el archivo (uno, no vacío, `.xlsx`, nombre ≤ 260, ≤ 10 MiB configurable) y comprobar el usuario persistido, el caso de uso: (1) sube el archivo a SeaweedFS por la API HTTP del filer en una ruta única; (2) registra `CargaArchivo` Pendiente y su historial en una transacción; (3) publica `{idCarga, rutaArchivo, usuario}` en la cola `carga_masiva` con confirmación del intermediario. Ante un fallo definitivo de publicación llama a `usp_CambiarEstadoCarga` con origen esperado Pendiente (nuevo parámetro opcional de V003) y registra la incidencia técnica solo si el cambio se aplicó. Responde `Response<T>` 202 con `idCarga`. Polly protege SeaweedFS, RabbitMQ y el procedimiento con parámetros por operación según su riesgo de duplicación. Compose incorpora RabbitMQ y SeaweedFS. [Spec] [Dec] [Plan]

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.401). [F004]

**Primary Dependencies**: existentes (MediatR 12.5.0, FluentValidation 12.2.0, EF Core SqlServer 10.0.14, Serilog). Nuevas: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14 (Api), `RabbitMQ.Client` 7.2.2 y `Polly.Core` 8.9.0 (Infrastructure); licencias MIT, Apache-2.0/MPL-2.0 y BSD-3-Clause. Rate Limiting y `HttpClient` del framework, sin paquetes. [Verif NuGet] (research D-01)

**Storage**: SQL Server `CargaMasivaApp` (tablas existentes; procedimiento modificado por `database/scripts/V003__estado_origen_esperado.sql`); SeaweedFS (filer HTTP) para el Excel; RabbitMQ (intercambio directo `carga_masiva`). [Dec]

**Testing**: sin pruebas automatizadas; verificación manual en `quickstart.md` con base temporal `CargaMasivaApp_F005` y tokens generados fuera del repositorio. [Dec §11]

**Target Platform**: contenedores Linux con Docker Compose; motor SQL Server amd64 emulado en este equipo (equipo de evaluación sin validar). [Dec]

**Project Type**: monorepo de microservicios; esta feature toca `ms-control/`, `database/scripts/`, `compose.yaml` y `.env.example`. [Spec]

**Performance Goals**: no definidos. **Constraints**: un archivo ≤ 10 MiB por solicitud (configurable); sin Outbox, servicios de recuperación ni frameworks nuevos; sin atomicidad entre SeaweedFS, SQL Server y RabbitMQ (limitaciones en spec). **Scale/Scope**: un endpoint, un caso de uso, una entidad nueva en Control, un script SQL.

## Constitution Check

*GATE: antes de Phase 0 y tras Phase 1.* Constitución v3.1.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo lo de la spec; procedencia en cada decisión; sin endpoints extra, consumidores ni Gateway | Pasa |
| §2 Organización | Cambios en `ms-control/`, `database/scripts/` (V003), `compose.yaml`, `.env.example`; sin Git ni Spec Kit nuevos | Pasa |
| §3 Plataforma | .NET 10; CQRS con MediatR 12.5.0 (comando + handler); controlador en `Acity.Api`; Repository/UoW con la única operación añadida que hace falta (transacción) | Pasa |
| §4 Capas | Controlador en Api; comando, handler, validador, DTO `record`, opciones e interfaces de almacenamiento/publicación/estado en Application; entidad `Usuario` en Domain; SeaweedFS, RabbitMQ, Polly, procedimiento y mapeo en Infrastructure; mapeos manuales; sin carpetas vacías | Pasa |
| §5 Respuesta | `Response<T>` con `Code` = HTTP; camelCase y `data` null (comportamiento predeterminado, comprobado); un solo formato; 401/403/413/429 por las páginas de estado y el manejador global existentes | Pasa |
| §6 Persistencia | EF Core + LINQ; procedimiento por EF (`ExecuteSqlRawAsync`), sin Dapper; esquema cambiado solo por script versionado del runner; `Usuario` por ingeniería inversa con solo las columnas necesarias (spec FR-009) | Pasa |
| §7 Reglas de carga | Control no lee el Excel; no deduce límites del ejemplo | Pasa |
| §8 Estados | Pendiente al crear; Pendiente → Fallido solo ante fallo definitivo de publicación y solo desde Pendiente (V003); error transitorio no cambia el estado | Pasa |
| §9 Integraciones | Control valida JWT y permiso; contrato `sub`/`role`; 10 MiB; Rate Limiting en Control; Polly con reintentos limitados por operación y Circuit Breaker; colas y SeaweedFS del README; sin Outbox | Pasa |
| §10 Logging | Propiedades estructuradas; sin tokens, claves, cadenas de conexión ni contenido | Pasa |
| §11 Docker y pruebas | RabbitMQ y SeaweedFS en Compose sin directorios nuevos; sin tests automatizados | Pasa |
| §12 Dependencias | Tres paquetes con versión y licencia comprobadas; sin envoltorios adicionales de Polly | Pasa |

Tras Phase 1: sin violaciones. Excepciones justificadas en Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/005-recepcion-registro-cargas/
├── plan.md                                   # Este plan
├── research.md                               # D-01…D-14 y abiertos
├── data-model.md                             # Entidades de Control, valores escritos, estados, opciones
├── quickstart.md                             # Verificación manual (ningún escenario ejecutado)
├── contracts/
│   ├── http-recepcion-carga.md               # POST /cargas, contrato del token, respuestas
│   ├── mensaje-carga-masiva.md               # Topología y mensaje de la cola carga_masiva
│   └── usp-cambiar-estado-carga-v003.md      # Parámetro @EstadoEsperado
├── checklists/requirements.md                # De la etapa de spec
└── tasks.md                                  # Phase 2 (/speckit-tasks; NO creado aquí)
```

### Source Code (repository root)

Cambios previstos en la implementación (no se hacen en esta etapa):

```text
database/scripts/V003__estado_origen_esperado.sql                     # nuevo (ALTER PROCEDURE)
compose.yaml                                                          # rabbitmq, seaweedfs, env y depends_on de ms-control
.env.example                                                          # RABBITMQ_*, JWT_*

ms-control/src/Acity.Api/
├── Acity.Api.csproj                                                  # + JwtBearer 10.0.14
├── Program.cs                                                        # auth JWT, rate limiter, límites Kestrel/Form, orden de middleware, mensajes 401/403/413/429
├── appsettings.json                                                  # CargaArchivo, Jwt (sin clave), RateLimiting, RabbitMq/SeaweedFs (sin secretos)
├── Controllers/CargasController.cs                                   # nuevo: POST /cargas
└── Handlers/GlobalExceptionHandler.cs                                # + BadHttpRequestException → su estado

ms-control/src/Acity.Application/
├── Cargas/Commands/RegistrarCarga/
│   ├── RegistrarCargaCommand.cs                                      # record
│   ├── RegistrarCargaCommandValidator.cs                             # FluentValidation
│   ├── RegistrarCargaCommandHandler.cs                               # flujo D-09
│   └── RegistrarCargaResponse.cs                                     # record (IdCarga, Estado)
├── Cargas/CargaArchivoOptions.cs                                     # TamanoMaximoBytes
├── Interfaces/Repositories/IUnitOfWork.cs                            # + ExecuteInTransactionAsync
└── Interfaces/Services/
    ├── IAlmacenamientoArchivos.cs                                    # SubirAsync(ruta, abrirContenido)
    ├── IPublicadorCargas.cs                                          # PublicarAsync(mensaje)
    └── IEstadoCargaService.cs                                        # PasarAFallidoDesdePendienteAsync → bool

ms-control/src/Acity.Domain/Entities/Usuario.cs                       # nuevo (Id, Email)

ms-control/src/Acity.Infrastructure/
├── Acity.Infrastructure.csproj                                       # + RabbitMQ.Client 7.2.2, Polly.Core 8.9.0
├── DependencyInjection.cs                                            # opciones, HttpClient, pipelines Polly, servicios
├── Persistence/Configurations/UsuarioConfiguration.cs                # nuevo
├── Persistence/Configurations/CargaArchivoConfiguration.cs           # + relación UsuarioId → Usuario
├── Repositories/UnitOfWork.cs                                        # + ExecuteInTransactionAsync
├── Storage/SeaweedFsAlmacenamiento.cs                                # PUT al filer con Polly
├── Messaging/RabbitMqPublicadorCargas.cs                             # topología, confirmación, Polly
└── Persistence/EstadoCargaService.cs                                 # usp_CambiarEstadoCarga con @EstadoEsperado, Polly
```

Las opciones de infraestructura (`JwtOptions` en Api; `RabbitMqOptions`, `SeaweedFsOptions` en Infrastructure) se definen junto a quien las usa. Los demás microservicios, `api-gateway/`, V001/V002, `referencias/` y la constitución no cambian.

**Structure Decision**: se mantiene la estructura de capas de F002/F004; se añaden solo las carpetas con contenido (`Cargas/`, `Interfaces/Services/`, `Storage/`, `Messaging/`, `Controllers/CargasController.cs`).

## Complexity Tracking

| Elemento | Por qué hace falta | Alternativa más simple descartada |
|---|---|---|
| `IUnitOfWork.ExecuteInTransactionAsync` | El historial Pendiente necesita el `Id` generado de la carga en la misma transacción (FR-018) y F004 prohíbe navegaciones | Navegaciones (contrarias a F004); procedimiento nuevo de alta (amplía `database/` sin decisión) |
| Mapeo parcial de `Usuario` (`Id`, `Email`) | Mínimo privilegio: Control no debe leer `PasswordHash` ni necesita `RolId` (spec FR-009) | Mapear todas las columnas como en F004 (expone el hash sin necesidad) |
| Tres interfaces de servicio en Application | Inversión de dependencias del handler respecto de SeaweedFS, RabbitMQ y el procedimiento (README §4 "CQRS o inversión de dependencias") | Llamar a la infraestructura desde el handler (rompe las capas) |
| Script V003 en `database/` | Comprobación y cambio atómicos (decisión del usuario, opción A) | Comprobación en Control (deja ventana de carrera) |

## Pendientes tras el plan

Ninguno que requiera decisión del usuario. Se comprueban en la implementación (research "Abiertos"): etiquetas exactas de las imágenes de RabbitMQ y SeaweedFS (Docker Hub inaccesible en esta sesión), espera de confirmación de `BasicPublishAsync`, código de estado del `PUT` del filer, valores predeterminados de Kestrel/`FormOptions`, clasificación de errores transitorios y validación en el equipo de evaluación. Dependencias de otras features: Autenticación debe emitir tokens con el contrato de `contracts/http-recepcion-carga.md`; el consumidor de Carga Masiva debe declarar la misma topología y descartar mensajes de cargas fuera de Pendiente.
