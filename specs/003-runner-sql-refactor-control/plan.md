# Implementation Plan: Reactivación del runner de database/ y refactorización de ms-control

**Branch**: `003-runner-sql-refactor-control` (trabajo en `main`; sin rama dedicada) | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-runner-sql-refactor-control/spec.md`

**Procedencia**: `Dec` = decisión del usuario o de la constitución v3.0.0 (incluye el prompt de este plan); `Spec` = requisito de la spec 003; `Ref` = `referencias/`; `Plan` = decisión técnica de este plan con la alternativa más sencilla; `Obs` = comprobado leyendo el repositorio en esta etapa (solo lecturas). Las decisiones técnicas se documentan en `research.md` y no requieren aprobación individual.

## Summary

Pasa a haber un único responsable del esquema: un runner en `database/` que Docker Compose ejecuta una vez, después de SQL Server y antes de los cuatro microservicios. Aplica solo los scripts versionados pendientes, los registra y, si falla, Compose no arranca los microservicios. En `ms-control` se retiran las migraciones EF Core, su ejecución al iniciar y las siete entidades con sus mapeos (ninguna tiene referencias fuera del modelo y las migraciones). Se documenta la ingeniería inversa con `dotnet ef dbcontext scaffold` para cuando una feature funcional necesite entidades. [Dec]

Enfoque:
- **Runner**: se reutiliza el existente (`database/run-scripts.sh`, `bootstrap.sql`, `Dockerfile`, scripts V001/V002, cerrojo de sesión, checksum, orden estricto). Cambios mínimos: base destino obligatoria sin valor por defecto, comprobación previa de la base (no mezclar historiales) y espera acotada ya existente. [Dec] [Obs]
- **Compose**: nuevo servicio `database-runner` (ejecución única, `restart: "no"`), `depends_on: sqlserver healthy`; los cuatro servicios dependen de `database-runner: service_completed_successfully`. [Dec]
- **Base destino**: una base nueva y vacía `CargaMasivaApp` para el runner y los cuatro servicios; `CargaMasiva` y `CargaMasivaDb` y sus volúmenes se conservan sin cambios. [Dec] [Plan: nombre]
- **Control**: sin Migrations, sin `DatabaseInitializer`, sin entidades ni configuraciones; conserva fundación, `AppDbContext`, Repository/UoW, `/health`, `dotnet-ef` local y el paquete `Design` (necesarios para la ingeniería inversa). [Dec]
- **Otros tres servicios**: sin cambios de código; la ingeniería inversa queda documentada. [Dec]

Entrega **solo documentación de diseño**: no se modifica código, Compose, scripts, bases ni volúmenes; eso es de `tasks` e implementación.

## Technical Context

**Language/Version**: C# / .NET 10 (servicios, sin cambios de plataforma); Bash + `sqlcmd` (mssql-tools18) para el runner existente. **Primary Dependencies**: ninguna nueva. Se conservan EF Core SqlServer 10.0.14, MediatR 12.5.0, FluentValidation 12.2.0, Serilog.AspNetCore 10.0.0, `System.Security.Cryptography.Pkcs` 10.0.14 y, solo en Control, `Microsoft.EntityFrameworkCore.Design` 10.0.14 y `dotnet-ef` 10.0.14. [Dec] [Obs]

**Storage**: SQL Server 2022 (imagen existente `2022-CU27-ubuntu-22.04`, volumen `sqlserver-data`, puerto 14333); base nueva `CargaMasivaApp`; las bases `CargaMasiva` y `CargaMasivaDb` se conservan. [Dec]

**Testing**: sin pruebas automatizadas; validación manual real de Compose en la implementación (`quickstart.md`). [Dec]

**Target Platform**: Contenedores Docker (Linux). Limitación vigente: el motor, y el runner que reutiliza su imagen, son amd64 y corren emulados en Apple silicon, sin soporte oficial de Microsoft; la validación vale solo para este equipo y Docker **no se declara validado** hasta ejecutarla. [Dec]

**Project Type**: Monorepo de microservicios más un componente de base de datos. **Performance Goals**: no definidos. **Constraints**: sin referencias entre servicios; secretos por variables de entorno; ningún servicio modifica el esquema al arrancar; sin proyecto .NET ni microservicio para migraciones; sin tecnologías nuevas. **Scale/Scope**: mínimo para el examen.

## Constitution Check

*GATE: antes de Phase 0 y tras Phase 1.* Constitución v3.0.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Procedencia por decisión; asuntos pendientes de la spec resueltos con lo indicado por el usuario; sin funcionalidad nueva | Pasa |
| §2 Organización | `database/` activo con scripts y runner; cambios en `database/`, `compose.yaml`, `.env.example`, `ms-control`; sin Git ni Spec Kit por componente | Pasa |
| §3 Plataforma | Sin cambios de .NET, hosts ni paquetes; sin `Acity.Worker` | Pasa |
| §4 Capas | Control conserva sus cinco proyectos; `Acity.Domain` queda sin entidades, como en los otros tres servicios (sin carpetas ni capas vacías preventivas: se retira `Entities/`) | Pasa |
| §5 Respuesta | Sin cambios | Pasa |
| §6 Persistencia | Una base compartida; scripts versionados y runner desde Compose; ningún servicio crea o modifica el esquema al arrancar; EF Core + LINQ para acceso a datos; ingeniería inversa como operación de desarrollo con solo las entidades necesarias; procedimientos por los scripts; roles, contraseñas y usuarios iniciales sin cambios | Pasa |
| §9 Integraciones | Fuera de esta feature | Pasa |
| §10 Logging | El runner no imprime secretos; logs de servicios sin cambios | Pasa |
| §11 Docker y pruebas | Orden SQL Server → runner → servicios; si el runner falla, no arrancan; sin otro microservicio ni ejecutable .NET; sin tests | Pasa |
| §12 Dependencias | Ninguna añadida; `Design` y `dotnet-ef` ya presentes (solo desarrollo) | Pasa |

Hallazgo de coherencia: la constitución §6 pide ingeniería inversa para "las entidades y mapeos que cada servicio necesita funcionalmente". La revisión de Control (D-04) sigue dos fases: conservación provisional de lo dudoso hasta reunir evidencia y decisión final por entidad (uso o ausencia documentados; se conserva solo con una referencia actual o una función de Control implementada o aprobada). La evidencia reunida (0 referencias; sin función implementada ni aprobada) lleva a retirar las siete y a que los cuatro servicios queden con `AppDbContext` sin entidades. No es un conflicto; es el resultado de aplicar el criterio.

Tras Phase 1: sin violaciones nuevas.

## Project Structure

### Documentation (this feature)

```text
specs/003-runner-sql-refactor-control/
├── plan.md                              # Este plan
├── research.md                          # Decisiones D-01…D-10, procedencia y alternativas
├── data-model.md                        # Registro de scripts, resultado de revisión de Control y procedimiento de ingeniería inversa
├── quickstart.md                        # Validación manual real (ningún escenario ejecutado)
├── contracts/
│   └── runner-y-compose.md              # Contrato del runner (entorno, salidas, códigos) y del orden en Compose
├── checklists/requirements.md           # De la etapa de spec
└── tasks.md                             # Phase 2 (/speckit-tasks; NO creado aquí)
```

### Source Code (repository root)

Cambios previstos en la implementación (no se hacen en esta etapa):

```text
database/
├── Dockerfile                           # sin cambios previstos (reconstruir tras editar scripts)
├── bootstrap.sql                        # sin cambios previstos
├── run-scripts.sh                       # DB_NAME obligatorio; comprobación previa de la base (D-03)
└── scripts/V001…, V002…                 # sin cambios (idénticos a referencias/)
compose.yaml                             # servicio database-runner; depends_on de los 4 servicios; DB_NAME por defecto CargaMasivaApp
.env.example                             # DB_NAME=CargaMasivaApp
ms-control/                              # retiro de Migrations, DatabaseInitializer, 7 entidades y 7 configuraciones; Program.cs y AppDbContext simplificados
ms-autenticacion/, ms-carga-masiva/, ms-notificaciones/   # sin cambios de código
```

**Structure Decision**: `database/` conserva su estructura actual; `compose.yaml` añade un único servicio de ejecución única. Detalle en `research.md` D-01 a D-10.

## Complexity Tracking

| Elemento | Por qué es necesario | Alternativa más simple rechazada porque |
|---|---|---|
| Comprobación previa de la base en el runner (D-03) | Cumple FR-013: no modificar ni mezclar historiales con bases creadas por EF o con objetos sin registro | Confiar en que el nombre de base sea nuevo: no protege ante un error de configuración (mismo criterio que la guarda de la Feature 002) |
| `DB_NAME` sin valor por defecto en el runner | El valor anterior (`CargaMasiva`) apunta a una base que se conserva | Mantener el valor por defecto: riesgo de operar sobre la base antigua por descuido |
| Servicio `database-runner` con imagen del propio SQL Server | Ya incluye `sqlcmd` y se descarga una sola vez (Feature 001 R-11) | Un ejecutable .NET o una imagen de herramientas aparte: prohibido (§11) o tecnología nueva |

## Pendientes tras el plan

Ninguno bloquea. Los puntos abiertos del plan se comprobaron en la implementación (`research.md` "Abiertos" y `quickstart.md` "Resultados"). **Sigue pendiente**: la validación en el equipo de evaluación (motor y runner amd64 emulados en Apple silicon, sin soporte de Microsoft; lo comprobado vale solo para este equipo) y, como hallazgo, la espera del runner con SQL Server detenido (~9,5 min o más) no se investigó. Documentos de las features 001 y 002: con nota de "sustituido por la Feature 003". Fuera de esta feature: RabbitMQ, SeaweedFS, Polly, Gateway y la lógica funcional de cada servicio.
