# Implementation Plan: Procesamiento de cargas en ms-carga-masiva

**Branch**: `006-procesamiento-cargas` (trabajo en `main`; sin rama dedicada) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-procesamiento-cargas/spec.md`

**Procedencia**: `Dec` = decisión del usuario o constitución v3.2.0; `Spec` = spec 006; `F001/F004/F005` = features anteriores; `Ref` = referencia; `Plan` = decisión técnica de este plan (detalle en `research.md`; sin aprobación individual); `Verif` = comprobado en esta etapa. Sin cambios de código, scripts ni servicios en esta etapa.

## Summary

ms-carga-masiva incorpora un `BackgroundService` que, con un cerrojo de aplicación de SQL Server de ámbito de sesión, recupera las cargas interrumpidas y consume `carga_masiva` de a un mensaje con confirmación manual. Cada carga Pendiente se toma con `usp_CambiarEstadoCarga` y origen esperado Pendiente (V003), cuenta un intento, se descarga de SeaweedFS, se lee con ClosedXML, se validan y normalizan las filas, se reservan los periodos con `usp_ReservarPeriodo`, se deciden los ganadores sobre todo el archivo y se persiste cada periodo Admitido en una transacción (con modo fila a fila ante una colisión de código). Termina en Cargado → Finalizado y publica `{idCarga, usuario, fechaFin}` en `notificaciones` con confirmación. Un archivo inválido genera `ArchivoInvalido` (V004) y termina Finalizado; un archivo inexistente o un fallo técnico terminal termina Fallido con `ErrorTecnico`; una notificación sin confirmación deja Finalizado con `ErrorTecnico`. [Spec] [Dec] [Plan]

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.401). [F004]

**Primary Dependencies**: existentes (MediatR 12.5.0, FluentValidation 12.2.0, EF Core SqlServer 10.0.14, Serilog). Nuevas: `ClosedXML` 0.105.1 (MIT), `RabbitMQ.Client` 7.2.2, `Polly.Core` 8.9.0 (Infrastructure); `Microsoft.Extensions.Logging.Abstractions` 10.0.14 (Application); `FrameworkReference Microsoft.AspNetCore.App` en Infrastructure. [Verif NuGet] (research D-01; vigilar `SixLabors.Fonts` transitivo)

**Storage**: SQL Server `CargaMasivaApp` (V004 amplía `CK_Incidencia_Tipo`); SeaweedFS (lectura); RabbitMQ (`carga_masiva` consumo, `notificaciones` publicación). [Dec]

**Testing**: sin pruebas automatizadas; verificación manual (`quickstart.md`) con base temporal `CargaMasivaApp_F006`, Control real y la gestión de RabbitMQ. [Dec §11]

**Target Platform**: contenedores Linux con Docker Compose; SQL Server amd64 emulado en este equipo. [Dec]

**Project Type**: monorepo de microservicios; toca `ms-carga-masiva/`, `database/scripts/V004__…`, `compose.yaml`. [Spec]

**Performance Goals**: no definidos (archivos ≤ 10 MiB por Control). **Constraints**: un consumidor activo, procesamiento secuencial, sin Outbox, barrido, Cronos, Redis ni frameworks de mensajería; sin atomicidad entre base, cola y almacenamiento. **Scale/Scope**: un consumidor, dos casos de uso (procesar, recuperar), una entidad nueva, un script.

## Constitution Check

*GATE: antes de Phase 0 y tras Phase 1.* Constitución v3.2.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Reglas de F001 por referencia; sin endpoints, consultas, correo ni Gateway | Pasa |
| §2 Organización | Cambios en `ms-carga-masiva/`, `database/scripts/V004`, `compose.yaml` | Pasa |
| §3 Plataforma | `BackgroundService` en `Acity.Api` (sin Worker); casos de uso con MediatR; UoW con transacción (como la 005) | Pasa |
| §4 Capas | Consumidor en Api; comandos, handlers, lector de filas (reglas puras), interfaces de almacenamiento, lector Excel, publicador, cerrojo y procedimientos en Application; implementaciones en Infrastructure; `Usuario` en Domain | Pasa |
| §5 Respuesta | Sin endpoints nuevos; `/health` sin cambios | Pasa |
| §6 Persistencia | EF Core + LINQ; procedimientos por EF; esquema solo por V004; `Usuario` por ingeniería inversa con columnas mínimas | Pasa |
| §7 Reglas | Normalización, ausentes/inválidos, orden original, Existente con producto confirmado, transacción por periodo, fallo terminal, archivo inválido (v3.2.0) | Pasa |
| §8 Estados | Toma con origen esperado; Cargado → Finalizado; Fallido solo por fallo terminal; reinicio no produce Fallido; notificación no confirmada conserva Finalizado | Pasa |
| §9 Integraciones | Cerrojo de sesión; recuperación antes de consumir; reentrega descartada; Polly por operación; colas `carga_masiva`/`notificaciones`; ClosedXML | Pasa |
| §10 Logging | Sin contenido del Excel ni secretos | Pasa |
| §11 Docker y pruebas | Compose: variables y dependencias de ms-carga-masiva; sin tests | Pasa |
| §12 Dependencias | ClosedXML (única de Excel); sin EPPlus ni BulkExtensions | Pasa |

Tras Phase 1: sin violaciones; excepciones justificadas en Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/006-procesamiento-cargas/
├── plan.md
├── research.md                              # D-01…D-17 y abiertos
├── data-model.md                            # Entidades, incidencias, recorridos, opciones
├── quickstart.md                            # 24 escenarios (ninguno ejecutado)
├── contracts/
│   ├── consumo-carga-masiva.md              # Topología, decisión por mensaje, recuperación
│   ├── mensaje-notificaciones.md            # Topología y mensaje de notificaciones
│   ├── cerrojo-consumidor.md                # sp_getapplock de sesión y vigencia
│   └── v004-incidencia-archivo-invalido.md  # Ampliación del CHECK
├── checklists/requirements.md
└── tasks.md                                 # Phase 2 (/speckit-tasks; NO creado aquí)
```

### Source Code (repository root)

Cambios previstos en la implementación (no se hacen en esta etapa):

```text
database/scripts/V004__incidencia_archivo_invalido.sql
compose.yaml                                                     # ms-carga-masiva: env y depends_on

ms-carga-masiva/src/Acity.Api/
├── Program.cs                                                   # opciones, AddHostedService
├── appsettings.json                                             # Procesamiento:MaxIntentos, RabbitMq:Puerto
└── Consumers/ConsumidorCargasMasivas.cs                         # BackgroundService (D-02)

ms-carga-masiva/src/Acity.Application/
├── Acity.Application.csproj                                     # + Logging.Abstractions
├── Cargas/ProcesamientoOptions.cs                               # MaxIntentos
├── Cargas/Commands/ProcesarCarga/{ProcesarCargaCommand,ProcesarCargaCommandHandler}.cs
├── Cargas/Commands/RecuperarCargas/{RecuperarCargasCommand,RecuperarCargasCommandHandler}.cs
├── Cargas/Procesamiento/
│   ├── FilaExcel.cs                                             # record (fila leída, sin reglas)
│   ├── ValidadorFilas.cs                                        # D-09 (reglas puras)
│   ├── DecisorGanadores.cs                                      # D-10 (reglas puras)
│   └── ProcesadorCarga.cs                                       # fases D-05, D-11, D-14
├── Interfaces/Repositories/IRepositorioProcesamiento.cs         # implementado en lugar de ampliar IUnitOfWork (research D1)
└── Interfaces/Services/
    ├── IAlmacenamientoArchivos.cs + ArchivoNoEncontradoException.cs   # descarga; 404 = ausencia confirmada
    ├── ILectorExcel.cs             # devuelve filas o "archivo inválido"
    ├── IPublicadorNotificaciones.cs + MensajeNotificacion.cs
    ├── IProcedimientosCarga.cs     # usp_ReservarPeriodo, usp_CambiarEstadoCarga
    └── ICerrojoConsumidor.cs

ms-carga-masiva/src/Acity.Domain/Entities/Usuario.cs

ms-carga-masiva/src/Acity.Infrastructure/
├── Acity.Infrastructure.csproj                                  # + ClosedXML, RabbitMQ.Client, Polly.Core, FrameworkReference
├── DependencyInjection.cs
├── Persistence/Configurations/{UsuarioConfiguration,CargaArchivoConfiguration}.cs
├── Persistence/ProcedimientosCarga.cs
├── Persistence/RepositorioProcesamiento.cs                       # transacción por periodo y colisión (research D1); UnitOfWork sin cambios
├── Storage/SeaweedFsAlmacenamiento.cs + SeaweedFsOptions.cs
├── Excel/LectorExcelClosedXml.cs
├── Messaging/{RabbitMqOptions,RabbitMqConexion,ConsumidorRabbitMq,PublicadorNotificaciones}.cs
├── Locking/CerrojoConsumidorSql.cs
└── Resilience/PipelinesResiliencia.cs
```

Los otros servicios, V001–V003, `api-gateway/`, `referencias/` y la constitución no cambian.

**Structure Decision**: capas de F002/F004/F005; carpetas nuevas solo con contenido. Las reglas de negocio (validación de filas y decisión de ganadores) son clases puras de Application sin dependencias de infraestructura.

## Complexity Tracking

| Elemento | Por qué hace falta | Alternativa más simple descartada |
|---|---|---|
| Modo fila a fila ante colisión | R-06 exige resolver la colisión como Existente sin rechazar el grupo y la collation puede discrepar de `OrdinalIgnoreCase` dentro del mismo archivo | Reintentar el grupo completo: repetiría la misma colisión hasta agotar los reintentos |
| Conexión dedicada sin pool para el cerrojo | El cerrojo de sesión debe vivir toda la vida del consumidor (F001) | Conexión del pool: se devolvería y liberaría el cerrojo |
| Clases puras `ValidadorFilas` y `DecisorGanadores` | Separan las reglas de F001 de la infraestructura y hacen revisable el algoritmo de R-05 | Todo en el handler: mezcla reglas, persistencia y mensajería |
| `IProcedimientosCarga` | Inversión de dependencias para dos procedimientos usados en varias fases | Llamadas SQL desde Application (rompe capas) |
| Script V004 | Tipo de incidencia nuevo autorizado (Q1) | Reutilizar otro tipo (descartado por el usuario) |

## Pendientes tras el plan

**Requieren decisión del usuario** (revisión de cierre, `research.md` "Hallazgos"):
- **H1**: criterio de lectura de precios numéricos (representación decimal más corta) frente a la regla de precisión exacta sin redondeo silencioso; caso P0047 `75.51000000000001` → 75.51. No se da por aprobado.
- **H2**: precio en texto con coma ("1,5") tratado como no numérico; cambio frente a D-07.
- **H3**: la transacción de un periodo no tiene circuit breaker, contra spec FR-016.

**Sin decisión pendiente, pero no verificado**: validación en el equipo de evaluación (motor amd64 emulado en este equipo); "reintento seguido de éxito" de la descarga (escenario 19, parcial); errores SQL transitorios no provocados; duplicado de notificación por reintento tras confirmación perdida no provocado. Dependencia de la feature de Notificaciones: declarar la misma topología de `notificaciones` y descartar mensajes de cargas que no estén Finalizado.
