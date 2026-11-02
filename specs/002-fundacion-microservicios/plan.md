# Implementation Plan: Fundación común de los cuatro microservicios

**Branch**: `002-fundacion-microservicios` (trabajo en `main`; sin rama dedicada) | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-fundacion-microservicios/spec.md`

**Procedencia**: `Dec` = decisión del usuario o de la constitución v2.1.0; `Ref` = proyecto de referencia (`Clean_architecture_base_net7`, lectura parcial: ver spec); `Plan` = decisión técnica de este plan con la alternativa más sencilla; `Verif` = comprobado en esta sesión. Las decisiones técnicas se documentan en `research.md` y no requieren aprobación individual.

## Summary

Cuatro microservicios independientes (`ms-autenticacion`, `ms-control`, `ms-carga-masiva`, `ms-notificaciones`), cada uno con `src/Acity.Api|Application|Domain|Infrastructure|Common` y host ASP.NET Core Web API con controladores. Control es el único propietario de las migraciones EF Core de la base compartida, que aplica al iniciar y antes de escuchar. Los demás servicios no migran. Un Compose en la raíz levanta SQL Server → Control → los otros tres. [Dec]

Enfoque:
- **Capas** como la referencia (`Api → Infrastructure → Application → {Common, Domain}`) con registro por capa, MediatR 12.5.0, FluentValidation (validadores + un `ValidationBehavior`), Repository y Unit of Work mínimos. [Ref] [Dec]
- **Contrato común** `Response<T>` en `Acity.Common/Helpers`; manejo global con `IExceptionHandler` y `UseStatusCodePages`. [Dec] [Plan]
- **Salud**: `GET /health` por controlador; en Control solo responde tras migrar porque el servidor no escucha antes. [Plan]
- **Migraciones**: `InitialSchema` (generada desde el modelo, equivalente a `V001`) y `StoredProcedures` (SQL de `V002`); base nueva `CargaMasivaDb` en la instancia existente; guarda contra la base heredada. [Dec] [Plan]
- **Logs** Serilog en consola y archivo, rotación diaria y 14 archivos, un volumen por servicio. [Plan]

Entrega **solo documentación de diseño**: no se generan proyectos, entidades, migraciones, Dockerfiles ni código; eso es de `tasks` e implementación.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10 instalado en el equipo). **Primary Dependencies** (versiones y licencias verificadas en `research.md` D-02): EF Core SqlServer 10.0.14 (MIT), MediatR 12.5.0 (Apache-2.0), FluentValidation 12.2.0 (Apache-2.0), Serilog.AspNetCore 10.0.0 (Apache-2.0); `Design` 10.0.14 y `dotnet-ef` 10.0.14 solo en Control. Sin Dapper, AutoMapper, Reec, EPPlus, Cronos, CloudWatch, BulkExtensions, Swagger, Newtonsoft ni paquetes de pruebas. [Dec]

**Storage**: SQL Server 2022 (imagen existente `2022-CU27-ubuntu-22.04`), base nueva `CargaMasivaDb` en la instancia y volumen actuales; la base `CargaMasiva` anterior se conserva. [Dec]

**Testing**: Sin pruebas automatizadas; verificación manual (`quickstart.md`). [Dec]

**Target Platform**: Contenedores Docker (Linux). Limitación vigente: el motor es amd64 y corre emulado en Apple silicon, sin soporte oficial de Microsoft; la comprobación previa con el runner anterior **no valida** EF Core Migrations ni las imágenes .NET, que se validan en la implementación. No se declara Docker funcional. [Dec]

**Project Type**: Monorepo de microservicios independientes; esta feature crea la estructura de los cuatro y el Compose raíz; no toca `api-gateway/`. **Performance Goals**: no definidos. **Constraints**: sin referencias entre servicios ni biblioteca compartida; secretos solo por variables de entorno; Control único propietario de migraciones; sin RabbitMQ/SeaweedFS/MailKit/ClosedXML/Polly todavía. **Scale/Scope**: mínimo para el examen.

## Constitution Check

*GATE: antes de Phase 0 y tras Phase 1.* Constitución v2.1.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Decisiones técnicas con procedencia; límites de FR-042–044 respetados; pendientes visibles | Pasa |
| §2 Organización | Cambios en los cuatro `ms-*` y `compose.yaml`; sin Git ni Spec Kit por componente; `database/` no se toca | Pasa |
| §3 Plataforma | .NET 10; Web API con controladores; `Acity.Api` en los cuatro; consumidores futuros como `BackgroundService`, sin `Acity.Worker`; MediatR 12.5.0; ejecutables independientes | Pasa |
| §4 Capas | Cinco proyectos; DTO `record`, entidades clases, interfaces; mapeos manuales; sin capas ni carpetas vacías preventivas | Pasa |
| §5 Respuesta | `Response<T>` en Common; `Code` = estado HTTP; manejo global sin Reec | Pasa |
| §6 Persistencia | SQL Server; EF Core/LINQ; migraciones solo en Control, aplicadas al iniciar; procedimientos por SQL en migraciones; sin Dapper | Pasa |
| §9 Integraciones | Gateway, RabbitMQ, SeaweedFS, Polly y consumidores fuera de esta feature | Pasa |
| §10 Logging | Serilog; archivo por servicio; volúmenes; sin secretos; `using` solo para recursos propios | Pasa |
| §11 Docker y pruebas | Dockerfile por ejecutable; Compose raíz; sin tests | Pasa |
| §12 Dependencias | Versiones y licencias verificadas; excluidos respetados | Pasa |

Nota: la constitución §5 y §10 delegaban al plan "Data sin resultado y serialización" y "rotación y retención de logs"; este plan los resuelve (research D-08, D-10). La lista "Pendientes" de la constitución aún los menciona; retirarlos es una enmienda mínima opcional fuera de esta etapa.

Tras Phase 1: sin violaciones nuevas.

## Project Structure

### Documentation (this feature)

```text
specs/002-fundacion-microservicios/
├── plan.md
├── research.md                          # Decisiones D-01…D-13, versiones y verificaciones
├── data-model.md                        # Mapeo EF Core de Control y equivalencia con V001/V002
├── quickstart.md                        # Validación manual (ningún escenario ejecutado)
├── contracts/
│   └── http-y-operacion.md              # /health, Response<T>, variables, logs, Compose
└── tasks.md                             # Phase 2 (/speckit-tasks; NO creado aquí)
```

### Source Code (repository root)

Se crea en `/speckit-tasks`/implementación (nombres definitivos; los cuatro servicios con la misma forma; ejemplo de `ms-control`):

```text
ms-control/
├── Dockerfile
├── .dockerignore
├── .config/dotnet-tools.json            # solo Control (dotnet-ef 10.0.14)
└── src/
    ├── Acity.Control.slnx
    ├── Acity.Api/                       # host, Program.cs, HealthController, GlobalExceptionHandler, appsettings
    ├── Acity.Application/               # AddApplication, ValidationBehavior, contratos IGenericRepository/IUnitOfWork
    ├── Acity.Domain/                    # entidades (Control: 7)
    ├── Acity.Infrastructure/            # AppDbContext, repositorio y UoW, AddInfrastructure
    │   └── Persistence/Migrations/      # solo Control
    └── Acity.Common/                    # Helpers/Response<T>
```

Mismo patrón en `ms-autenticacion/` (`Acity.Autenticacion.slnx`), `ms-carga-masiva/` (`Acity.CargaMasiva.slnx`) y `ms-notificaciones/` (`Acity.Notificaciones.slnx`), sin `.config/` ni `Migrations/`. `compose.yaml` (raíz) conserva SQL Server y añade los cuatro servicios y sus volúmenes de logs. `.env.example` pasa a definir `DB_NAME=CargaMasivaDb`.

**Structure Decision**: Cuatro soluciones independientes, una por servicio, con proyectos de nombre repetido pero sin referencias cruzadas; contexto Docker por servicio. Detalle en `research.md` D-03.

## Complexity Tracking

| Elemento | Por qué es necesario | Alternativa más simple rechazada porque |
|---|---|---|
| Cuatro copias de `Response<T>`, `ValidationBehavior`, Repository/UoW | Los servicios son independientes y no pueden compartir biblioteca [Dec] | Una biblioteca compartida está prohibida |
| `ValidationBehavior` único | Hace real la integración de FluentValidation sin añadir más comportamientos | No integrar: incumple FR-007 |
| `HealthController` por servicio | La constitución exige controladores y la spec pide salud | `MapHealthChecks` y paquetes de salud: salen de §3 y añaden superficie |
| Migrar entre `Build()` y `Run()` | Garantiza que Control no responde `/health` hasta terminar | Marcar "listo" con un indicador: más estado y más código |
| Guarda de base heredada | Evita migrar sobre la base creada con el runner (FR-034) | Confiar en la base nueva: no protege ante un error de configuración |
| Dos migraciones (`InitialSchema`, `StoredProcedures`) | Mantiene el código generado sin editar y el SQL manual aparte | Una sola con SQL mezclado: más difícil de revisar |
| `AppDbContext` vacío en tres servicios | Cableado de Repository/UoW sin duplicar el esquema | Omitirlo: cada feature lo rehace |

## Pendientes tras el plan

Ninguno bloquea las tasks. Se comprueban en la implementación (research "Abiertos"): `bash` y permisos de volumen en la imagen `aspnet:10.0`; comportamiento de `dotnet ef` con el host mínimo; equivalencia exacta del esquema migrado con V001/V002; validación real de Docker con EF Core Migrations en el equipo de ejecución. Fuera de esta feature: RabbitMQ, SeaweedFS, Polly, Gateway y la lógica funcional de cada servicio.
