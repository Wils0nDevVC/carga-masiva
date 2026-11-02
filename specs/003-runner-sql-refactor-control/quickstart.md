# Quickstart: validación manual del runner y de Control refactorizado

Guía de verificación manual (sin pruebas automatizadas) para cuando exista la implementación. **Ningún escenario se ha ejecutado**; no se declara Docker validado. Contratos: [contracts/runner-y-compose.md](contracts/runner-y-compose.md); modelo: [data-model.md](data-model.md); decisiones: [research.md](research.md).

**Limitación vigente**: el motor SQL Server y la imagen del runner son amd64 y corren emulados en Apple silicon, sin soporte oficial de Microsoft. La validación vale solo para este equipo; el equipo de evaluación queda sin validar.

## Prerrequisitos
- Docker; `.env` con `MSSQL_SA_PASSWORD` (nunca versionado) y `DB_NAME=CargaMasivaApp`. Si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`; SQL Server conserva la contraseña con la que se inicializó el volumen.
- Se reutiliza la instancia SQL Server y su volumen. `CargaMasiva` y `CargaMasivaDb` no se tocan: tomar antes y después una huella de catálogo (conteo de objetos y fecha de modificación) para comprobarlo.
- Las bases de pruebas llevan el prefijo `CargaMasivaApp_` y se eliminan al terminar; nunca se eliminan las bases existentes ni volúmenes ni se usa `docker compose down -v`.

## Escenarios

| # | Acción | Resultado esperado | Spec |
|---|---|---|---|
| 1 | `docker compose up -d --build` con `DB_NAME=CargaMasivaApp` (base inexistente) | SQL Server healthy → `database-runner` termina con código 0 → los 4 servicios arrancan y quedan healthy | US2, FR-010/011 |
| 2 | Catálogo de `CargaMasivaApp` contra `referencias/V001`/`V002` | 7 tablas, índices, CHECK, FK, collation, 2 roles, 2 procedimientos sin diferencias; `SchemaMigraciones` con 2 filas y checksums | US1, SC-001 |
| 3 | `docker compose up -d` de nuevo, sin scripts nuevos | El runner informa "Sin scripts pendientes", sale 0 y no cambia el esquema ni el registro; los servicios siguen sanos | US1-3, SC-002 |
| 4 | Añadir un script de prueba de número mayor (copia temporal fuera del repositorio o en una base temporal) y ejecutar el runner | Solo ese script se aplica y se registra; los anteriores no se reejecutan | US1-4, SC-003 |
| 5 | Modificar un script aplicado y, aparte, añadir uno de número menor (en una base temporal) | El runner falla sin aplicar nada | US1-5, SC-004 |
| 6 | Forzar un fallo de script en una base temporal (p. ej., objeto en conflicto) | El script se revierte y no se registra; el runner sale ≠ 0; con Compose, ningún microservicio arranca | US1-6, US2-2, SC-005 |
| 7 | Dos ejecuciones simultáneas del runner sobre una base temporal | Solo una aplica; sin duplicados ni corrupción | US1-7 |
| 8 | Apuntar el runner a una base temporal con `__EFMigrationsHistory` y a otra con tablas sin `SchemaMigraciones` | Error claro y base intacta en ambos casos; los servicios no arrancan | US6-2, FR-013 |
| 9 | Detener SQL Server y ejecutar el runner | Espera acotada y error sin crear nada | US1-1 |
| 10 | Revisar salida y registros del runner | Sin contraseña ni cadenas de conexión | US1-8, FR-008 |
| 11 | Revisar `ms-control`: repetir la búsqueda de referencias por entidad antes de retirar y registrar el resultado final por entidad (uso o ausencia; conservada solo con referencia actual o función implementada/aprobada) | 0 migraciones, 0 inicializador, 0 entidades ni configuraciones salvo las que la evidencia justifique y quede documentada; compila sin advertencias; sin referencias entre servicios ni paquetes no autorizados | US3, US4, SC-008/009 |
| 12 | Arrancar `ms-control` contra la base del runner | Escucha sin operaciones de esquema; `GET /health` 200; no crea `__EFMigrationsHistory` | US3-2 |
| 13 | Comparar esquema y registro antes y después de arrancar los cuatro servicios | Idénticos | US2-4, SC-007 |
| 14 | Repetir las reglas de la Feature 001 sobre `CargaMasivaApp` | Mismos resultados que con el runner y con EF anteriores | US6-4 |
| 15 | Ejecutar la ingeniería inversa de una tabla contra `CargaMasivaApp` con salida a una carpeta temporal | Genera solo esa tabla; no se integra ni se añade al repositorio; no se ejecuta en arranque ni en imágenes | US5, SC-010 |
| 16 | Revisar `CargaMasiva`, `CargaMasivaDb` y los volúmenes | Sin cambios respecto de la huella inicial | US6-1, SC-011 |
| 17 | Revisar dependencias y proyectos | 0 paquetes nuevos y 0 proyectos o dependencias de pruebas | SC-012 |

Comprobar y registrar en la implementación (no se asumen): comportamiento de `docker compose up -d` con un runner fallido y la reejecución del runner en cada `up` (research "Abiertos").

Escenarios 1, 3, 6, 7, 8, 9 y 13 requieren Docker con SQL Server en el equipo de ejecución. El resultado de cada uno se anota en una sección "Resultados" de este archivo al implementar, indicando también los no ejecutados.

## Resultados (implementación, 2026-11-02; este equipo: Apple M4 Pro, Docker Desktop; SQL Server 2022 amd64 emulado)

### Línea base (T001, T002)

Huella inicial de solo lectura de las bases protegidas (instancia del proyecto, antes de cualquier prueba):

| Base | Objetos de usuario | Última modificación (UTC) | Filas de registro |
|---|---|---|---|
| `CargaMasiva` | 35 | 2026-11-02 13:22:39.517 | `SchemaMigraciones` = 2; sin `__EFMigrationsHistory` |
| `CargaMasivaDb` | 32 | 2026-11-02 14:48:54.303 | `__EFMigrationsHistory` = 2; sin `SchemaMigraciones` |

Volúmenes del proyecto: `carga-masiva_sqlserver-data` (creado 2026-11-02T13:19:29Z, el que usa Compose), `carga-masiva-sqlserver-data` (2026-11-02T13:15:34Z, ajeno a este Compose), `carga-masiva_logs-{control,autenticacion,carga-masiva,notificaciones}`. `database/scripts/V001`/`V002` idénticos a `referencias/` (`diff -q`); `sqlserver` y los cuatro servicios estaban `healthy` al empezar.

### US1 - Runner (T007 a T016; bases temporales `CargaMasivaApp_T1`/`T2`, scripts de prueba en copias fuera del repositorio)

Cambios en `database/run-scripts.sh`: `DB_NAME` obligatorio (sin valor por defecto; con `DB_NAME` vacío el runner falla con `Defina DB_NAME`) y comprobación previa de la base antes del bootstrap (estados `NUEVA`/`OK` continúan; `EF` y `SINREGISTRO` fallan sin modificar la base). `V001`/`V002` sin cambios (mismos SHA-256 que al inicio).

| # | Resultado |
|---|---|
| 2 | **Ejecutado**: sobre `CargaMasivaApp_T1` (inexistente) el runner creó la base, aplicó `V001` y `V002` y salió con 0. Volcado de catálogo (74 líneas: columnas, tipos, collation, nulabilidad, defaults, CHECK, FK, índices con filtro, 2 procedimientos por hash desde `CREATE PROCEDURE`, 2 roles) **idéntico** al de `CargaMasiva` (solo lectura). `SchemaMigraciones`: 2 filas con los mismos checksums que los archivos |
| 3 (runner) | **Ejecutado**: segunda ejecución → "Sin scripts pendientes", código 0, huella (objetos, fecha, registro) idéntica |
| 4 | **Ejecutado**: con una copia que añade `V003__prueba.sql` aplicó y registró solo `V003` (`V001`/`V002` no se reejecutaron) |
| 5 | **Ejecutado**: (a) `V002` modificado → `ERROR: el script aplicado V002__procedimientos.sql cambio (checksum distinto)`, código 1; (b) `V000__menor.sql` → `ERROR: V000__menor.sql tiene numero menor que el ultimo aplicado (V003__prueba.sql)`, código 1; en ambos, huella idéntica y la tabla de prueba no se creó |
| 6 (runner) | **Ejecutado**: `V004__falla.sql` (crea una tabla y luego `SELECT 1/0`) → `Msg 8134 Divide by zero error encountered`, código 1; la tabla no existe, `V004` no está en `SchemaMigraciones` y la huella es idéntica |
| 7 | **Ejecutado**: sobre `CargaMasivaApp_T2` (ya con V001/V002), dos ejecuciones con un `V003__lento.sql` (20 s): una aplicó (código 0) y la otra salió con 1 y `Msg 50001 Otro runner mantiene el cerrojo de scripts.`; `V003` registrado una sola vez y la tabla creada una vez. Nota: la carrera de la primera creación de una base inexistente no se probó |
| 9 | **Ejecutado**: con `sqlserver` detenido, el runner mostró "Esperando a SQL Server…", agotó los 60 intentos y salió con 1 (`SQL Server no responde tras 60 intentos.`) sin crear la base. **Hallazgo**: tardó unos 9,5 minutos o más (no se midió con precisión ni se investigó la causa; no es el ~2 minutos esperado). El límite es acotado, pero más largo que el estimado; no se cambió. `sqlserver` se reinició y quedó `healthy` |
| 10 | **Ejecutado**: 10 salidas guardadas fuera del repositorio; 0 coincidencias con la contraseña, `Password=` o `User Id=`; 0 coincidencias de la contraseña en los archivos versionados de esta feature |

### Retiro del modelo de Control (T024 a T031, T028)

Evidencia actual (búsqueda por nombre, `*.cs`/`*.csproj`/`*.json` de los cuatro servicios y el Gateway, sin `bin`/`obj`/`.idea`), ya con las migraciones retiradas:

| Entidad | Referencias fuera de su modelo | Función de Control implementada o aprobada | Decisión final |
|---|---|---|---|
| Rol | 0 | Ninguna | Retirada |
| Usuario | 0 | Ninguna | Retirada |
| CargaArchivo | 0 | Ninguna (el README y la constitución describen el rol de Control, pero no hay feature, spec, plan ni código de recepción aprobados) | Retirada |
| CargaPeriodo | 0 | Ninguna | Retirada |
| Producto | 0 | Ninguna | Retirada |
| Incidencia | 0 | Ninguna | Retirada |
| CargaEstadoHistorial | 0 | Ninguna | Retirada |

No quedó ninguna entidad en duda, así que no hubo conservaciones provisionales pendientes. Código de Control restante: `HealthController`, `GlobalExceptionHandler`, `Program.cs`, `ValidationBehavior`, `DependencyInjection` (Application e Infrastructure), `IGenericRepository`, `IUnitOfWork`, `GenericRepository`, `UnitOfWork`, `AppDbContext`, `Response`. `Program.cs` y `AppDbContext.cs` de Control quedaron idénticos a los de `ms-autenticacion` (salvo el nombre). `dotnet build ms-control/src/Acity.Control.slnx`: 0 advertencias, 0 errores; sin restos de `Migrations`, `DatabaseInitializer`, `MigrateAsync`, `__EFMigrationsHistory` ni `ForeignKeyIndexConvention` en `ms-control`.

### US2 - Compose (T017 a T023)

`compose.yaml`: los cuatro servicios dependen solo de `database-runner: service_completed_successfully` (`docker compose config` resuelve `database-runner -> sqlserver: service_healthy`); `DB_NAME` por defecto `CargaMasivaApp` (runner y cadenas de conexión).

| # | Resultado |
|---|---|
| 6 (Compose) | **Ejecutado** antes del arranque correcto y con los 4 servicios detenidos: base temporal `CargaMasivaApp_Fallo` vacía con `dbo.usp_ReservarPeriodo` preexistente; `DB_NAME=CargaMasivaApp_Fallo docker compose up -d ms-autenticacion` → `V001` aplicado, `V002` falló con `Msg 2714 … There is already an object named 'usp_ReservarPeriodo'`, se revirtió y no se registró (procedimiento original intacto); `docker compose up` terminó con código 1 (`service "database-runner" didn't complete successfully: exit 1`); `ms-autenticacion` quedó solo en estado `Created` (Compose lo recreó pero no lo inició) y los otros tres siguieron `Exited` (ninguno arrancó) |
| 1 | **Ejecutado**: `docker compose up -d --build` con `DB_NAME=CargaMasivaApp` (base inexistente), código 0 en ~14 s: `sqlserver` healthy → `database-runner` (inicio 18:06:58.075, fin 18:06:59.049, código 0) → los cuatro servicios iniciados a partir de 18:06:59.19 y los cuatro `healthy`; `/health` = 200 en 8081–8084 |
| 2 (base real) | **Ejecutado**: catálogo de `CargaMasivaApp` (74 líneas) **idéntico** al de `CargaMasiva`; `SchemaMigraciones` con las 2 filas y los checksums de `V001`/`V002`; sin `__EFMigrationsHistory` |
| 3 (Compose) | **Ejecutado**: un segundo `docker compose up -d` **vuelve a ejecutar** el runner (inicio 18:07:21), que informa "Sin scripts pendientes" y sale con 0; la huella (objetos, fecha, registro) quedó idéntica y los servicios siguieron `healthy` sin recrearse |
| 13 | **Ejecutado**: tras reiniciar los cuatro servicios, huella y catálogo idénticos; sin `__EFMigrationsHistory`; 0 coincidencias de la contraseña en los logs de los cuatro servicios |

### US3 y US4 - Control (T026, T027, T032, T033)

| # | Resultado |
|---|---|
| 11 | **Ejecutado**: `ms-control` sin `Migrations`, `DatabaseInitializer`, entidades ni configuraciones; `dotnet build` 0 advertencias y 0 errores; en la imagen reconstruida, 0 apariciones de `DatabaseInitializer`, `InitialSchema`, `AppDbContextModelSnapshot` ni `MigrateAsync` en `Acity.Infrastructure.dll`/`Acity.Api.dll` |
| 12 | **Ejecutado**: `ms-control` arrancó tras el runner (reconstruido en el `up` de T020), log sin operaciones de esquema, `GET /health` = 200 con `Response<string>` y sin `__EFMigrationsHistory` en `CargaMasivaApp` |
| — | **Ejecutado**: `ms-autenticacion`, `ms-carga-masiva` y `ms-notificaciones` compilan sin cambios (0 advertencias, 0 errores); 0 `ProjectReference` fuera de su servicio |

### US5 - Ingeniería inversa (T034, T035)

| # | Resultado |
|---|---|
| 15 | **Ejecutado**: `dotnet ef dbcontext scaffold` de `dbo.Rol` contra `CargaMasivaApp`, salida a carpeta temporal fuera del repositorio: código 0, solo `Rol.cs` y `ScaffoldContext.cs`; 0 coincidencias de la contraseña en la salida; `git status` sin archivos nuevos del repositorio; no se ejecuta en el arranque ni en las imágenes. Comando y observaciones en `data-model.md` |

### US6 - Sin mezclar historiales y reglas de la Feature 001 (T036 a T038)

| # | Resultado |
|---|---|
| 8 | **Ejecutado**: (a) `CargaMasivaApp_EF` con un `dbo.__EFMigrationsHistory` simulado → `ERROR: … tiene historial de EF Core (__EFMigrationsHistory). No se mezclan historiales… La base no se modifico.`, código 1; (b) `CargaMasivaApp_Tablas` con una tabla y sin `SchemaMigraciones` → `ERROR: … tiene tablas sin registro del runner (SchemaMigraciones)… La base no se modifico.`, código 1. En ambas, huella y lista de tablas idénticas (no se creó `SchemaMigraciones`). Con Compose (`DB_NAME=CargaMasivaApp_EF docker compose up -d ms-autenticacion`, servicios detenidos): código 1, `service "database-runner" didn't complete successfully: exit 1` y ningún servicio arrancó; después se restauró el stack con `docker compose up -d` (los 4 `healthy` sobre `CargaMasivaApp`). No se probó el runner contra `CargaMasiva` ni `CargaMasivaDb` |
| 14 | **Ejecutado** sobre `CargaMasivaApp_T3` (creada por el runner): los mismos scripts de verificación de la Feature 001 usados con los mecanismos anteriores: **28 comprobaciones aprobadas / 0 fallos** (reserva, CHECK, índice filtrado, unicidad, comparación de códigos) y **27 aprobadas / 0 fallos** (matriz de estados y fallo terminal); concurrencia de 40 periodos desde dos sesiones: 40 Admitido (uno por periodo), 0 periodos con más de un Admitido, 0 errores. La salida conserva la nota informativa conocida (la base redondea 10.005 a 10.01: la omisión de la fila es de la aplicación). Los escenarios de consumidor, recuperación y correo de la Feature 001 no corresponden a esta feature y no se ejecutaron |
| 16 | **Ejecutado**: tras todas las pruebas, la huella de `CargaMasiva` (35 objetos, `SchemaMigraciones` = 2) y `CargaMasivaDb` (32 objetos, `__EFMigrationsHistory` = 2) y la fecha de creación de los volúmenes son idénticas a las de T001 |

### Cierre (T041 a T044)

| # | Resultado |
|---|---|
| 17 | **Ejecutado**: 0 `*.csproj` modificados; paquetes de Control sin cambios (MediatR 12.5.0, FluentValidation 12.2.0, EF Core SqlServer y Design 10.0.14, Serilog.AspNetCore 10.0.0, `System.Security.Cryptography.Pkcs` 10.0.14); sin proyectos ni dependencias de pruebas; 0 `ProjectReference` fuera de cada servicio |
| — | **Ejecutado**: las seis bases temporales (`CargaMasivaApp_T1`, `_T2`, `_T3`, `_EF`, `_Tablas`, `_Fallo`) se eliminaron al terminar; `CargaMasivaApp` (35 objetos, `SchemaMigraciones` = 2), `CargaMasiva` y `CargaMasivaDb` siguen presentes; sin contenedores temporales del runner; los cuatro servicios y `sqlserver` `healthy` |

**Resumen de los 17 escenarios**: ejecutados 1 a 17 en este equipo. **No ejecutado / no probado**: la carrera por crear a la vez una base inexistente (escenario 7 se probó con la base ya creada); el runner apuntado a `CargaMasiva` o `CargaMasivaDb` (deliberadamente no se probó); los escenarios de consumidor, recuperación y correo de la Feature 001 (no son de esta feature); la validación en el equipo de evaluación.

**Hallazgo**: con `sqlserver` detenido, la espera acotada del runner (60 intentos) tardó unos 9,5 minutos o más, no los ~2 minutos esperados; no se investigó la causa ni se cambió el límite.

**Limitación vigente**: el motor SQL Server y la imagen del runner son amd64 y corren emulados en Apple silicon, sin soporte oficial de Microsoft. Estos resultados se obtuvieron **solo en este equipo**; el equipo de evaluación **no está validado**, y no se generaliza que Docker funcione en otro entorno. Además, si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`, y SQL Server conserva la contraseña con la que se inicializó el volumen.
