# Implementation Plan: Conexión a CargaMasivaApp y entidades por microservicio

**Branch**: `004-conexion-entidades-por-servicio` (trabajo en `main`; sin rama dedicada) | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-conexion-entidades-por-servicio/spec.md`

**Procedencia**: `Dec` = decisión del usuario o de la constitución v3.0.0 (incluye el prompt de este plan); `Spec` = requisito de la spec 004; `Ref` = repositorio de referencia consultado; `F001/F002/F003` = features anteriores; `Plan` = decisión técnica de este plan con la alternativa más sencilla; `Verif` = comprobado en esta etapa con lecturas (sin scaffolding, sin cambios de código ni de bases). Las decisiones técnicas se documentan en `research.md` y no requieren aprobación individual.

## Summary

Cada microservicio mapea con EF Core + LINQ solo las tablas existentes de `CargaMasivaApp` que sus funciones usan (10 mapeos de 7 tablas: autenticación 2, control 3, carga masiva 4, notificaciones 1). Las entidades se obtienen por ingeniería inversa manual (`dotnet ef dbcontext scaffold`, una invocación por servicio con su lista de tablas) y se integran a mano según la organización de la referencia: entidad en `Acity.Domain`, un mapeo por entidad en `Acity.Infrastructure/Persistence/Configurations`, aplicado por el `AppDbContext` que ya existe. La conexión ya está cableada por la fundación (`ConnectionStrings__Default` → `AppDbContext`); esta feature solo la **comprueba**, con una verificación manual que usa el `DbContext` real de cada servicio y no añade endpoints ni cambia `/health`. [Dec]

Enfoque:
- **Selección**: la de la spec, confirmada y justificada con las fuentes (D-02). Quedan **diferidos** `Usuario` en Control y `Producto`/`CargaPeriodo` en Control; `CargaArchivo` **no** se mapea en Notificaciones, porque marcar Notificado se hace con el procedimiento `usp_CambiarEstadoCarga` (D-03). [Dec]
- **Ingeniería inversa**: una sola herramienta (la de Control) para los cuatro servicios, salida a carpeta temporal fuera del repositorio, sin `--force` sobre el repositorio y con revisión e integración manual (D-04, D-05). [Dec] [Plan]
- **Comprobación manual**: un archivo `.cs` ejecutable con `dotnet run`, fuera del repositorio, que registra la infraestructura del servicio, abre la conexión y lee cada entidad; también contrasta el modelo con el catálogo (D-06). [Dec] [Plan] [Verif]
- **Sin cambios** en el esquema, `database/`, Compose, `/health`, paquetes, referencias entre servicios ni constitución. [Dec]

Entrega **solo documentación de diseño**: no se ejecuta scaffolding ni se modifica código, bases o volúmenes; eso es de `tasks` e implementación.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.401 instalado). **Primary Dependencies**: ninguna nueva. EF Core SqlServer 10.0.14 (los cuatro `Acity.Infrastructure`), y solo en Control `Microsoft.EntityFrameworkCore.Design` 10.0.14 y `dotnet-ef` 10.0.14 (herramienta local). [Dec] [Verif]

**Storage**: SQL Server 2022 (imagen existente, volumen `sqlserver-data`, puerto 14333); base `CargaMasivaApp` creada por el runner de `database/`, idéntica a `referencias/V001`/`V002`; las bases `CargaMasiva` y `CargaMasivaDb` se conservan. [Dec] [F003]

**Testing**: sin pruebas automatizadas; verificación manual (`quickstart.md`), con scripts de comprobación fuera del repositorio. [Dec]

**Target Platform**: contenedores Docker (Linux). Limitación vigente: el motor es amd64 y corre emulado en Apple silicon, sin soporte oficial de Microsoft; lo comprobado vale para este equipo y no se declara validado en el equipo de evaluación. [Dec]

**Project Type**: monorepo de microservicios; esta feature toca solo `Acity.Domain` y `Acity.Infrastructure/Persistence` de los servicios que reciban entidades. **Performance Goals**: no definidos. **Constraints**: sin endpoints, casos de negocio, repositorios por entidad, `BaseEntity`, paquetes ni referencias entre servicios; el esquema solo cambia con scripts de `database/`. **Scale/Scope**: 10 mapeos de 7 tablas.

## Constitution Check

*GATE: antes de Phase 0 y tras Phase 1.* Constitución v3.0.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Procedencia por decisión; deferidos explícitos; sin funcionalidad nueva | Pasa |
| §2 Organización | Cambios solo en `ms-*` (Domain e Infrastructure); `database/` sin cambios | Pasa |
| §3 Plataforma | Sin cambios de host, MediatR ni paquetes | Pasa |
| §4 Capas | Entidades (clases) en Domain, mapeos en Infrastructure, `Api → Infrastructure → Application → {Common, Domain}`; sin carpetas ni capas vacías (Domain recibe `Entities/` solo en los servicios con entidades); mapeos manuales mínimos | Pasa |
| §5 Respuesta | Sin cambios; un fallo de conexión usa el manejo global existente | Pasa |
| §6 Persistencia | Una base compartida; EF Core + LINQ; ingeniería inversa de desarrollo con solo las entidades necesarias por servicio, sin CRUD ni lógica, sin sobrescribir personalizaciones; ningún servicio crea o modifica el esquema | Pasa |
| §7 Reglas de carga | `CodigoProducto` se compara con la collation de la columna (comprobación D-07) | Pasa |
| §9 Integraciones | Fuera de esta feature | Pasa |
| §10 Logging | Sin cambios; secretos solo por entorno y fuera de logs y respuestas | Pasa |
| §11 Docker y pruebas | Sin cambios de Dockerfile ni Compose; sin tests | Pasa |
| §12 Dependencias | Ninguna añadida; la herramienta y el paquete de diseño ya existen en Control | Pasa |

Tras Phase 1: sin violaciones nuevas.

## Project Structure

### Documentation (this feature)

```text
specs/004-conexion-entidades-por-servicio/
├── plan.md                                 # Este plan
├── research.md                             # Decisiones D-01…D-09, procedencia y verificaciones
├── data-model.md                           # Columnas por tabla (V001), matriz por servicio y reglas de mapeo
├── quickstart.md                           # Validación manual (ningún escenario ejecutado)
├── contracts/
│   └── ingenieria-inversa-y-comprobacion.md  # Comandos por servicio, reglas de integración y comprobación manual
├── checklists/requirements.md              # De la etapa de spec
└── tasks.md                                # Phase 2 (/speckit-tasks; NO creado aquí)
```

### Source Code (repository root)

Cambios previstos en la implementación (no se hacen en esta etapa):

```text
ms-autenticacion/src/Acity.Domain/Entities/{Rol,Usuario}.cs
ms-autenticacion/src/Acity.Infrastructure/Persistence/Configurations/{Rol,Usuario}Configuration.cs
ms-control/src/Acity.Domain/Entities/{CargaArchivo,CargaEstadoHistorial,Incidencia}.cs
ms-control/src/Acity.Infrastructure/Persistence/Configurations/{CargaArchivo,CargaEstadoHistorial,Incidencia}Configuration.cs
ms-carga-masiva/src/Acity.Domain/Entities/{CargaArchivo,CargaPeriodo,Producto,Incidencia}.cs
ms-carga-masiva/src/Acity.Infrastructure/Persistence/Configurations/{CargaArchivo,CargaPeriodo,Producto,Incidencia}Configuration.cs
ms-notificaciones/src/Acity.Domain/Entities/Incidencia.cs
ms-notificaciones/src/Acity.Infrastructure/Persistence/Configurations/IncidenciaConfiguration.cs
```

`AppDbContext`, `DependencyInjection`, `Program.cs`, `.csproj`, `compose.yaml`, `database/` y `/health` no cambian. Los scripts de comprobación manual viven fuera del repositorio.

**Structure Decision**: la organización de la referencia (entidad en Domain, `IEntityTypeConfiguration<T>` por entidad en `Infrastructure/Persistence/Configurations`, aplicados con `ApplyConfigurationsFromAssembly`), ya presente en el `AppDbContext` de la fundación. Detalle en `research.md` D-01 a D-09.

## Complexity Tracking

| Elemento | Por qué es necesario | Alternativa más simple rechazada porque |
|---|---|---|
| Un mapeo (clase de configuración) por entidad | Es la organización de la referencia y mantiene la configuración fuera del dominio | Atributos de datos o configuración dentro del contexto generado: contaminan Domain o concentran todo en un archivo |
| Comprobación manual con un `.cs` fuera del repositorio | Verifica la conexión y la lectura por el `DbContext` real sin añadir endpoints ni cambiar `/health` | Un endpoint o una comprobación de salud con base: prohibido por la spec; sqlcmd: no pasa por el `DbContext` |
| Scaffold desde la herramienta de Control para los cuatro servicios | Cumple "sin paquetes nuevos" (FR-020); el resultado no depende del proyecto destino | Añadir `Design` y `dotnet-ef` a cada servicio: paquetes y herramientas nuevos no aprobados en esta feature |

## Pendientes tras el plan

Ninguno bloquea. Los puntos abiertos del plan se comprobaron en la implementación (`research.md` "Abiertos" y `quickstart.md` "Resultados"). **Sigue pendiente**: la validación en el equipo de evaluación (motor amd64 emulado en Apple silicon, sin soporte de Microsoft; lo comprobado vale solo para este equipo) y el cuerpo de error de conexión por HTTP, que no puede ejercitarse sin un punto de ejecución de datos. Diferidos hasta que sus contratos o casos de uso lo justifiquen: `Usuario` en Control, `Producto`/`CargaPeriodo` en Control; el contenido del correo y la recuperación de notificaciones siguen pendientes en la constitución y no se asumen. Fuera de esta feature: endpoints, repositorios por entidad, RabbitMQ, SeaweedFS, Polly, Gateway y la lógica funcional.
