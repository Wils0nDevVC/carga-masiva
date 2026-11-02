# Quickstart: validación manual de conexión, entidades y mapeos

Guía de verificación manual (sin pruebas automatizadas) para cuando exista la implementación. **Ningún escenario se ha ejecutado**; no se declara Docker validado. Contratos: [contracts/ingenieria-inversa-y-comprobacion.md](contracts/ingenieria-inversa-y-comprobacion.md); modelo: [data-model.md](data-model.md); decisiones: [research.md](research.md).

**Limitación vigente**: el motor SQL Server amd64 corre emulado en Apple silicon, sin soporte oficial de Microsoft. La validación vale solo para este equipo; el equipo de evaluación queda sin validar.

## Prerrequisitos
- Compose levantado y `CargaMasivaApp` creada por el runner (`docker compose up -d`); `.env` con `DB_NAME=CargaMasivaApp`. Si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`; SQL Server conserva la contraseña con la que se inicializó el volumen.
- Antes de empezar, huella de solo lectura de `CargaMasiva`, `CargaMasivaDb` y de `CargaMasivaApp` (objetos, fecha máxima de modificación y filas de `SchemaMigraciones`) y de los volúmenes, para compararlas al final. Nunca `docker compose down -v` ni borrar volúmenes.
- Las bases de prueba llevan el prefijo `CargaMasivaApp_` y se eliminan al terminar.

## Escenarios

| # | Acción | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Ejecutar los cuatro comandos de ingeniería inversa del contrato (salida a carpeta temporal fuera del repositorio) | Por servicio, una clase por tabla solicitada más `ScaffoldContext.cs`; 2, 3, 4 y 1 tablas; `git status` sin archivos nuevos en el repositorio; las claves foráneas hacia tablas no incluidas aparecen solo como columnas | US3, FR-012, FR-018 |
| 2 | Integrar manualmente cada servicio según las reglas del contrato y compilar `dotnet build ms-<s>/src/Acity.<S>.slnx` | Los 4 compilan sin advertencias; entidades en `Acity.Domain/Entities` y un mapeo por entidad en `Persistence/Configurations`; sin `DbSet`, navegaciones, `HasIndex`, nombres de restricciones ni valores por defecto SQL | US4, FR-014, FR-017 |
| 3 | Comprobar que cada servicio tiene exactamente las entidades de la matriz | ms-autenticacion: `Rol`, `Usuario`; ms-control: `CargaArchivo`, `CargaEstadoHistorial`, `Incidencia`; ms-carga-masiva: `CargaArchivo`, `CargaPeriodo`, `Producto`, `Incidencia`; ms-notificaciones: `Incidencia`; 0 fuera de la matriz; 0 servicios con las siete | US2, SC-003 |
| 4 | Revisar la justificación y la procedencia de cada mapeo en `research.md` D-02/D-03 | El 100 % con función y fuente; 0 por necesidades futuras supuestas; `Usuario` y `Producto`/`CargaPeriodo` en Control y `CargaArchivo` en Notificaciones ausentes | US2, SC-004 |
| 5 | Con el `.cs` de comprobación de cada servicio (fuera del repositorio): conexión, `DB_NAME`, entidades y lectura LINQ | `CanConnect=True`; `DB_NAME=CargaMasivaApp`; 2, 3, 4 y 1 entidades; lectura sin error (`Rol` con 2 filas, el resto vacío) | US1, SC-001 |
| 6 | Volcado del modelo de cada servicio frente al catálogo de `CargaMasivaApp` | 0 diferencias en tabla, columna, tipo, longitud, nulabilidad, clave, generación de valores y claves foráneas | US3, SC-005 |
| 7 | En una base temporal creada por el runner: insertar por SQL filas de prueba y consultar con LINQ `CodigoProducto` ("p0117" frente a "P0117"; "á" frente a "a") | "p0117" encuentra "P0117"; "á" no iguala "a"; si falla, registrar el error y quitar `UseCollation` del mapeo | SC-007, FR-015 |
| 8 | Huella del esquema y de `SchemaMigraciones` de `CargaMasivaApp` antes y después de arrancar los cuatro servicios y de ejecutar la comprobación | Idéntica; sin `__EFMigrationsHistory`; sin operaciones de esquema | US1-4, SC-006, FR-004 |
| 9 | `GET /health` de cada servicio y revisión de `git diff` | 200 en 8081–8084; sin endpoints, controladores, handlers, repositorios por entidad, `BaseEntity` ni cambios en `.csproj` o paquetes; 0 referencias entre servicios | SC-008, FR-019, FR-020 |
| 10 | Revisar logs de los servicios y la salida de los scripts de comprobación | 0 coincidencias de la contraseña y de la cadena de conexión | SC-002, FR-002 |
| 11 | Huella de `CargaMasiva`, `CargaMasivaDb` y los volúmenes frente a la inicial | Idéntica | SC-010, FR-023 |
| 12 | Revisar constitución y README en `git diff` | Sin cambios | SC-010 |

Fallo de conexión con cuerpo `Response<T>`: no ejercitable sin un punto de ejecución de datos; se indica como no ejecutado y solo se comprueba que la cadena no aparece en logs (escenario 10).

Los escenarios 1, 5, 7 y 8 requieren Docker con SQL Server en el equipo de ejecución. Al implementar, los resultados se anotan en una sección "Resultados" de este archivo, indicando también los no ejecutados.

## Resultados (implementación, 2026-11-02; este equipo: Apple M4 Pro, Docker Desktop; SQL Server 2022 amd64 emulado)

### Línea base (T001, T002)

Huella inicial de solo lectura (instancia del proyecto, antes de cualquier prueba):

| Base | Objetos de usuario | Última modificación (UTC) | Filas de registro |
|---|---|---|---|
| `CargaMasiva` | 35 | 2026-11-02 13:22:39.517 | `SchemaMigraciones` = 2; sin `__EFMigrationsHistory` |
| `CargaMasivaDb` | 32 | 2026-11-02 14:48:54.303 | `__EFMigrationsHistory` = 2; sin `SchemaMigraciones` |
| `CargaMasivaApp` | 35 | 2026-11-02 18:06:59.020 | `SchemaMigraciones` = 2; sin `__EFMigrationsHistory` |

Volúmenes: `carga-masiva_sqlserver-data` (creado 2026-11-02T13:19:29Z, el que usa Compose), `carga-masiva-sqlserver-data` (2026-11-02T13:15:34Z, ajeno a este Compose), `carga-masiva_logs-{control,autenticacion,carga-masiva,notificaciones}`.

Base de AppDbContext: los cuatro `AppDbContext.cs` tienen el mismo SHA-256 y ningún `Acity.Domain` tiene `Entities/`; `dotnet build` de las cuatro soluciones: 0 advertencias y 0 errores; `git status` limpio antes de empezar.

### Sesión de trabajo y comprobación de línea base (T003 a T010)

- **Rutas temporales (fuera del repositorio)**: salida del scaffold `/private/tmp/claude-501/-Users-wils0ndev-examenes-tecnicos-acity-carga-masiva/41d8a904-ebb7-45d2-b29c-cb1f2a506cb3/scratchpad/scaffold004`; scripts de comprobación `/private/tmp/claude-501/-Users-wils0ndev-examenes-tecnicos-acity-carga-masiva/41d8a904-ebb7-45d2-b29c-cb1f2a506cb3/scratchpad/chk004` (`chk-autenticacion.cs`, `chk-control.cs`, `chk-carga-masiva.cs`, `chk-notificaciones.cs`). `dotnet tool restore` y `dotnet ef --version` = 10.0.14 desde `ms-control/`.
- **Escenario 5 (línea base, sin entidades)**: **Ejecutado**: los cuatro `chk` dieron `CanConnect=True`, `DB_NAME=CargaMasivaApp` y `Entidades en el modelo=0` (con el aviso esperado de EF "No instantiatable types implementing IEntityTypeConfiguration were found", que desaparecerá al integrar los mapeos). Solo lecturas.
- **Escenario 10 (parcial)**: **Ejecutado**: 0 archivos versionados contienen la contraseña; `appsettings`, Dockerfiles y `.env.example` no tienen `Password=` ni `User Id=`; `compose.yaml` solo contiene la plantilla con variables. Con un puerto cerrado, el fallo del `chk-control` terminó con `SocketException: Connection refused` y la salida **no** contiene la contraseña ni `Password=` (esto cubre la comprobación a nivel de `DbContext`; el cuerpo `Response<T>` por HTTP sigue sin poder ejercitarse sin un punto de ejecución).
- **Coherencia documental (T010)**: las matrices de `spec.md` y `data-model.md` son idénticas (2, 3, 4 y 1; 10 mapeos de 7 tablas); `plan.md` y `research.md` coinciden en los diferidos (`Usuario` y `Producto`/`CargaPeriodo` en Control) y en que `CargaArchivo` no se mapea en Notificaciones.

### Ingeniería inversa e integración (parcial: ms-autenticacion, ms-control y ms-carga-masiva)

- **Escenario 1 (parcial)**: **Ejecutado manualmente por el usuario** desde `ms-control/` (T011, T012, T013): se generó una clase por tabla solicitada más `ScaffoldContext.cs` (ms-autenticacion: `Rol`, `Usuario`; ms-control: `CargaArchivo`, `CargaEstadoHistorial`, `Incidencia`; ms-carga-masiva: `CargaArchivo`, `CargaPeriodo`, `Producto`, `Incidencia`); `git status` sin archivos nuevos del scaffold en el repositorio. **Resuelve el abierto 1**: el generador omite las claves foráneas hacia tablas no incluidas con el aviso `Skipping foreign key 'FK_CargaArchivo_Usuario' on table 'dbo.CargaArchivo' since principal table 'dbo.Usuario' was not found in the model` (y `FK_Incidencia_CargaPeriodo` en Control) y deja solo la columna (`UsuarioId`, `CargaPeriodoId`). Sí genera propiedades de navegación entre las tablas incluidas (se quitan al integrar). Pendiente: salida de ms-notificaciones (T014).
- **Observación de integración**: el scaffold no emite `ToTable` ni `HasKey` (salen por convención); se añadieron explícitos como en la referencia. Se conservó `OnDelete(DeleteBehavior.ClientSetNull)` en las claves foráneas obligatorias (equivale al NO ACTION de la base). `Precio` se declara con `HasPrecision(18, 2)`.
- **Integración (T016, T017, T018)**: entidades en `ms-<s>/src/Acity.Domain/Entities/` y un `…Configuration.cs` por entidad en `ms-<s>/src/Acity.Infrastructure/Persistence/Configurations/`, sin `DbSet`, navegaciones, `HasIndex`, nombres de restricciones ni valores por defecto; `dotnet build` de las tres soluciones: 0 advertencias y 0 errores.
- **Escenarios 5 y 6 (intermedio, solo lecturas; pendiente cerrar con ms-notificaciones)**: con los `chk` extendidos (volcado del modelo de diseño y lectura LINQ por entidad): ms-autenticacion — `CanConnect=True`, `DB_NAME=CargaMasivaApp`, 2 entidades, `Rol` Count=2 y `Usuario` Count=0, **0 diferencias** modelo/catálogo (7 líneas); ms-control — 3 entidades, lecturas sin error (vacías), **0 diferencias** (23 líneas); ms-carga-masiva — 4 entidades, lecturas sin error (vacías), **0 diferencias** (32 líneas), con `Producto.CodigoProducto` con `Latin1_General_100_CI_AS_WS` y las 4 claves foráneas internas (`CargaPeriodo→CargaArchivo`, `Incidencia→CargaArchivo`, `Incidencia→CargaPeriodo`, `Producto→CargaPeriodo`). Dos errores propios del script de comprobación, corregidos: usar el esquema `"dbo"` explícito (el modelo tiene esquema nulo) y leer metadatos relacionales del modelo en ejecución (solo lectura optimizada; se usa `IDesignTimeModel`). Aviso conocido de EF en el script (`Take` sin `OrderBy`), sin efecto.
- **Escenario 7 (collation)**: **Ejecutado** sobre la base temporal `CargaMasivaApp_Coll` (creada por el runner; filas "P0117" y "a" insertadas por SQL; eliminada al terminar): `CodigoProducto == "p0117"` → 1 fila ("P0117"); `== "á"` → 0 filas; `== "a"` y `== "A"` → 1 fila cada una. El SQL que genera EF es `WHERE [p].[CodigoProducto] = @codigo` **sin** `COLLATE`; `UseCollation` en el mapeo no altera la consulta (resuelve el abierto 2).

### Cierre de los escenarios (T014 a T035)

- **Escenario 1 (completo)**: **Ejecutado** (T014 por el usuario): ms-notificaciones generó solo `Incidencia.cs` y `ScaffoldContext.cs`, con los avisos `Skipping foreign key 'FK_Incidencia_Carga' … 'dbo.CargaArchivo' was not found in the model` y `'FK_Incidencia_CargaPeriodo' … 'dbo.CargaPeriodo'`: `CargaId` y `CargaPeriodoId` quedan como columnas. Las cuatro salidas se descartaron al terminar.
- **Escenario 2**: **Ejecutado**: integración de los cuatro servicios (T016 a T019); `dotnet build` de las cuatro soluciones, 0 advertencias y 0 errores; 20 archivos nuevos (10 entidades y 10 configuraciones); sin `DbSet`, navegaciones, `HasIndex`, `HasName`, `HasConstraintName`, `HasData`, valores por defecto ni `partial`.
- **Escenario 3**: **Ejecutado**: entidades por servicio = matriz (ms-autenticacion: `Rol`, `Usuario`; ms-control: `CargaArchivo`, `CargaEstadoHistorial`, `Incidencia`; ms-carga-masiva: `CargaArchivo`, `CargaPeriodo`, `Incidencia`, `Producto`; ms-notificaciones: `Incidencia`); 0 fuera de la matriz; 0 servicios con las siete.
- **Escenario 4**: **Ejecutado**: la justificación y la procedencia de cada mapeo constan en `research.md` D-02/D-03; las matrices de `spec.md` y `data-model.md` son idénticas (T010); `Usuario` y `Producto`/`CargaPeriodo` en Control y `CargaArchivo` en Notificaciones están ausentes.
- **Escenario 5 (completo)**: **Ejecutado** con los cuatro `chk` extendidos: `CanConnect=True`, `DB_NAME=CargaMasivaApp`, entidades 2, 3, 4 y 1; lecturas LINQ sin error (`Rol` Count=2; las demás vacías).
- **Escenario 6 (completo)**: **Ejecutado**: volcado del modelo de diseño frente al catálogo de `CargaMasivaApp`: **0 diferencias** en los cuatro servicios (7, 23, 32 y 9 líneas de tabla, columna, tipo, nulabilidad, identidad, clave primaria, collation y claves foráneas entre tablas mapeadas).
- **Escenario 8**: **Ejecutado**: tras reconstruir y levantar con `docker compose up -d --build`, la huella de `CargaMasivaApp` (35 objetos, fecha 2026-11-02 18:06:59.020, `SchemaMigraciones` = 2) y su catálogo son idénticos a los de antes; sin `__EFMigrationsHistory`; el runner volvió a ejecutarse y respondió "Sin scripts pendientes".
- **Escenario 9**: **Ejecutado**: los cuatro servicios `healthy` y `/health` = 200 en 8081 a 8084; 0 archivos modificados fuera de `specs/` (solo archivos nuevos de entidades y mapeos); ningún `*.csproj`, `AppDbContext`, `DependencyInjection`, `Program.cs`, `compose.yaml`, `database/` ni Dockerfile tocado; 0 controladores, handlers, repositorios por entidad, `BaseEntity`, `DbSet` ni navegaciones nuevos; 0 `ProjectReference` fuera de cada servicio.
- **Escenario 10**: **Ejecutado**: 0 coincidencias de la contraseña, `Password=` y `User Id=` en los logs de los cuatro servicios y en las salidas de los `chk`.
- **Escenario 11**: **Ejecutado**: la huella de `CargaMasiva` (35 objetos, `SchemaMigraciones` = 2), `CargaMasivaDb` (32 objetos, `__EFMigrationsHistory` = 2), `CargaMasivaApp` y los volúmenes es idéntica a la de T001.
- **Escenario 12**: **Ejecutado**: sin cambios en `.specify/memory/constitution.md` ni en `referencias/README.md`.
- **Limpieza (T033)**: no queda ninguna base `CargaMasivaApp_*` (solo se creó `CargaMasivaApp_Coll`, eliminada); la carpeta de scaffold se eliminó; los `chk-<servicio>.cs` y `coll-carga-masiva.cs` se conservan **fuera del repositorio** en `/private/tmp/claude-501/-Users-wils0ndev-examenes-tecnicos-acity-carga-masiva/41d8a904-ebb7-45d2-b29c-cb1f2a506cb3/scratchpad/chk004/` para repetir la comprobación (`ConnectionStrings__Default` en el entorno y `dotnet run chk-<servicio>.cs`).

**No ejecutado**: el cuerpo de error de conexión por HTTP (`Response<T>`): no hay un punto de ejecución de datos; solo se comprobó a nivel de `DbContext` que un fallo de conexión no imprime la contraseña. **Validación en el equipo de evaluación**: no realizada.

**Limitación vigente**: el motor SQL Server amd64 corre emulado en Apple silicon, sin soporte oficial de Microsoft. Estos resultados se obtuvieron **solo en este equipo**; el equipo de evaluación no está validado y no se generaliza que Docker funcione en otro entorno. Si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`, y SQL Server conserva la contraseña con la que se inicializó el volumen.
