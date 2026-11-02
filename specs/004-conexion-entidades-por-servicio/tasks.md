# Tasks: Conexión a CargaMasivaApp y entidades por microservicio

**Input**: `specs/004-conexion-entidades-por-servicio/` (plan.md, spec.md, research.md, data-model.md, contracts/ingenieria-inversa-y-comprobacion.md, quickstart.md)

**Alcance**: `Acity.Domain/Entities` y `Acity.Infrastructure/Persistence/Configurations` de los cuatro `ms-*/`; scripts de comprobación fuera del repositorio; al final, notas en `specs/002-.../data-model.md` y `specs/003-.../data-model.md` y los resultados de esta feature. No se tocan `database/`, `referencias/`, `api-gateway/`, `compose.yaml`, la constitución, `AppDbContext`, `DependencyInjection`, `Program.cs`, `.csproj` ni `/health`. Sin endpoints, controladores, handlers, repositorios por entidad, `BaseEntity`, `DbSet`, navegaciones, paquetes ni pruebas automatizadas. Verificación manual con `quickstart.md`.

**Reglas de ejecución**
- **Ingeniería inversa manual** (T011 a T014): la ejecuta el usuario (o Claude solo con su autorización expresa en la sesión). Salida **siempre** a una carpeta temporal **fuera del repositorio**; nunca `--force` ni `--output-dir`/`--context-dir` dentro del repositorio. Si una tarea depende de esa salida y aún no existe, **detenerse** y pedirla; no sustituirla por código escrito de memoria.
- **No dar por comprobado** lo que `research.md` lista como "Abiertos" (claves foráneas omitidas por el scaffold limitado, `UseCollation` en consultas LINQ, conexión y lectura reales, correspondencia con el catálogo): cada uno tiene su tarea; si falla, registrar el error exacto y aplicar la solución mínima compatible con el plan.
- **No modificar recursos ajenos**: `CargaMasiva` y `CargaMasivaDb`, sus datos y los volúmenes se conservan; en este equipo hay contenedores y volúmenes de otros proyectos que no se tocan. Nunca `docker compose down -v` ni borrar volúmenes. Las bases temporales llevan el prefijo `CargaMasivaApp_` y se eliminan al terminar; el trabajo normal es sobre `CargaMasivaApp` **solo en lectura** (las filas de prueba van a bases temporales).
- La contraseña solo por variable de entorno (el shell exporta `MSSQL_SA_PASSWORD`; Compose usa esa y no la de `.env`): no imprimirla ni escribirla en archivos versionados.
- Marcar `[x]` solo tareas realmente ejecutadas y verificadas. **No declarar Docker validado**: los resultados valen solo para este equipo (motor amd64 emulado en Apple silicon, sin soporte oficial de Microsoft); el equipo de evaluación queda sin validar.
- Al cerrar la implementación, entregar al usuario el comando de commit (sin `Co-Authored-By`).

**Formato**: `- [ ] ID [P] [US] Descripción con ruta`. `[P]` = archivos distintos y sin dependencia pendiente. Notación: `<s>` = cada servicio.

## Phase 1: Setup (línea base)

- [x] T001 Registrar en `specs/004-conexion-entidades-por-servicio/quickstart.md` (sección "Resultados", creándola) la huella inicial de solo lectura de `CargaMasiva`, `CargaMasivaDb` y `CargaMasivaApp` (conteo de objetos, fecha máxima de modificación y filas de `SchemaMigraciones` o `__EFMigrationsHistory`) mediante `docker exec` con `sqlcmd` en el contenedor `sqlserver` y `SQLCMDPASSWORD` tomada del entorno sin imprimirla, y la lista de volúmenes del proyecto
- [x] T002 Verificar la línea base: `docker compose ps` con `sqlserver` y los cuatro servicios `healthy` sobre `CargaMasivaApp`; `git status` limpio; los cuatro `ms-<s>/src/Acity.Infrastructure/Persistence/AppDbContext.cs` idénticos entre sí y sin entidades (`ms-<s>/src/Acity.Domain` sin `Entities/`); `dotnet build` de las cuatro soluciones (`ms-<s>/src/Acity.<S>.slnx`) con 0 advertencias

## Phase 2: Foundational (bloquea las historias)

- [x] T003 Preparar la sesión de trabajo: exportar `CONN="Server=localhost,14333;Database=CargaMasivaApp;User Id=sa;Password=$MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"` (sin mostrarla), crear `OUT="$(mktemp -d)"` y comprobar que está **fuera** del repositorio; ejecutar `dotnet tool restore` y `dotnet ef --version` desde `ms-control/` (herramienta local `dotnet-ef` 10.0.14); crear fuera del repositorio una carpeta de scripts de comprobación (`$OUT/../chk` o equivalente) y anotar su ruta en `quickstart.md`

## Phase 3: US1 - Cada servicio se conecta a CargaMasivaApp (P1)

**Meta**: los cuatro servicios abren conexión a `CargaMasivaApp` mediante su `DbContext` real y la cadena solo viene del entorno.

**Prueba independiente**: ejecutar el `.cs` de comprobación de cada servicio (primero sin entidades) y revisar la configuración.

- [x] T004 [P] [US1] Crear fuera del repositorio `chk-autenticacion.cs` según el contrato (`#:sdk Microsoft.NET.Sdk.Web`, `#:project /Users/wils0ndev/examenes-tecnicos/acity/carga-masiva/ms-autenticacion/src/Acity.Infrastructure/Acity.Infrastructure.csproj`, `#:property PublishAot=false`, `#:property PublishTrimmed=false`): `WebApplication.CreateBuilder`, `AddInfrastructure(builder.Configuration)`, resolver `AppDbContext`, `Database.CanConnectAsync()`, `SELECT DB_NAME()` y cantidad de entidades del modelo; solo lecturas
- [x] T005 [P] [US1] Crear fuera del repositorio `chk-control.cs` igual que T004 con `#:project` a `ms-control/src/Acity.Infrastructure/Acity.Infrastructure.csproj`
- [x] T006 [P] [US1] Crear fuera del repositorio `chk-carga-masiva.cs` igual que T004 con `#:project` a `ms-carga-masiva/src/Acity.Infrastructure/Acity.Infrastructure.csproj`
- [x] T007 [P] [US1] Crear fuera del repositorio `chk-notificaciones.cs` igual que T004 con `#:project` a `ms-notificaciones/src/Acity.Infrastructure/Acity.Infrastructure.csproj`
- [x] T008 [US1] Ejecutar los cuatro `chk-<s>.cs` con `ConnectionStrings__Default="$CONN"` (`dotnet run chk-<s>.cs`): cada uno debe dar `CanConnect=True`, `DB_NAME=CargaMasivaApp` y 0 entidades (línea base antes de integrar); anotar el resultado por servicio en `quickstart.md` (escenario 5, previo)
- [x] T009 [US1] Revisar que la cadena de conexión llega solo por entorno: `grep` de `Password=`, `User Id=` y de la contraseña en `ms-*/src/**/appsettings*.json`, `ms-*/Dockerfile`, `compose.yaml` y `.env.example` (solo debe aparecer la plantilla de `compose.yaml` con variables, nunca valores); y ejecutar `chk-control.cs` con un puerto cerrado en `ConnectionStrings__Default` para comprobar que el fallo no imprime la contraseña

## Phase 4: US2 - Selección justificada por servicio (P1)

**Meta**: se obtienen por ingeniería inversa solo las tablas de la matriz, con justificación documentada.

**Prueba independiente**: comparar la salida de cada scaffold con la lista de tablas del servicio y revisar la justificación.

- [x] T010 [US2] Cotejar `spec.md` (tabla de distribución y justificación), `plan.md`, `research.md` D-02/D-03 y `data-model.md` (matriz): mismas 10 asignaciones (2, 3, 4, 1), cada una con función y procedencia, y los diferidos (`Usuario` en Control; `Producto`/`CargaPeriodo` en Control; `CargaArchivo` en Notificaciones) y exclusiones iguales en los cuatro documentos; anotar discrepancias, si las hubiera, sin corregirlas por suposición
- [x] T011 [US2] **Manual**: ejecutar desde `ms-control/` el comando de **ms-autenticacion** de `contracts/ingenieria-inversa-y-comprobacion.md` (`--table dbo.Rol --table dbo.Usuario`, salida a `$OUT/autenticacion/{Entities,Context}`)
- [x] T012 [US2] **Manual**: ejecutar el comando de **ms-control** (`--table dbo.CargaArchivo --table dbo.CargaEstadoHistorial --table dbo.Incidencia`, salida a `$OUT/control/...`)
- [x] T013 [US2] **Manual**: ejecutar el comando de **ms-carga-masiva** (`--table dbo.CargaArchivo --table dbo.CargaPeriodo --table dbo.Producto --table dbo.Incidencia`, salida a `$OUT/carga-masiva/...`)
- [x] T014 [US2] **Manual**: ejecutar el comando de **ms-notificaciones** (`--table dbo.Incidencia`, salida a `$OUT/notificaciones/...`)
- [x] T015 [US2] Revisar las cuatro salidas: una clase por tabla solicitada más `ScaffoldContext.cs` y nada más; `git status` sin archivos nuevos en el repositorio; registrar en `quickstart.md` (escenario 1) si el generador omitió las claves foráneas hacia tablas no incluidas (p. ej. `CargaArchivo.UsuarioId`, `Incidencia.CargaPeriodoId` en Control y Notificaciones) y si incluyó propiedades de navegación hacia tablas no solicitadas (resuelve el abierto 1 de `research.md`)

## Phase 5: US3 - El mapeo corresponde al esquema existente (P1)

**Meta**: cada servicio integra sus entidades y mapeos sin redefinir el esquema, y el modelo coincide con el catálogo.

**Prueba independiente**: contrastar el volcado del modelo de cada servicio con el catálogo de `CargaMasivaApp`.

Reglas comunes de integración (research D-05; no sobrescribir; comparar con lo existente antes de copiar): entidad como clase en `ms-<s>/src/Acity.Domain/Entities/<Entidad>.cs` (namespace `Acity.Domain.Entities`, sin `partial`, sin navegaciones, sin `using` sin uso, **todas las columnas de la tabla**); un `ms-<s>/src/Acity.Infrastructure/Persistence/Configurations/<Entidad>Configuration.cs` por entidad (`IEntityTypeConfiguration<T>`, namespace `Acity.Infrastructure.Persistence.Configurations`) con `ToTable`, `HasKey` sin nombre, longitud, `IsUnicode(false)` en `varchar`, `IsFixedLength` en `char`, precisión del `decimal`, obligatoriedad y generación de valores; **sin** `HasName`, `HasConstraintName`, `HasIndex`, filtros, valores por defecto SQL, datos sembrados ni `DbSet`; relaciones solo entre entidades mapeadas del mismo servicio con `HasOne<T>().WithMany().HasForeignKey(...)`; claves foráneas hacia tablas no mapeadas solo como propiedad de clave.

- [x] T016 [P] [US3] Integrar **ms-autenticacion** desde `$OUT/autenticacion` (requiere T011): `Rol` — `Id` SMALLINT PK **sin identidad** (`short`, `ValueGeneratedNever`), `Nombre` NVARCHAR(50) NOT NULL; `Usuario` — `Id` INT IDENTITY PK, `Email` NVARCHAR(256) NOT NULL, `PasswordHash` VARCHAR(255) NOT NULL (`IsUnicode(false)`), `RolId` SMALLINT NOT NULL FK → `Rol`; relación `Usuario.RolId → Rol`; archivos en `ms-autenticacion/src/Acity.Domain/Entities/{Rol,Usuario}.cs` y `ms-autenticacion/src/Acity.Infrastructure/Persistence/Configurations/{Rol,Usuario}Configuration.cs`; compilar `ms-autenticacion/src/Acity.Autenticacion.slnx` con 0 advertencias
- [x] T017 [P] [US3] Integrar **ms-control** desde `$OUT/control` (requiere T012): `CargaArchivo` — `Id` BIGINT IDENTITY PK, `NombreArchivo` NVARCHAR(260) NOT NULL, `UsuarioId` INT NOT NULL (solo columna; `Usuario` no se mapea), `FechaRecepcion` DATETIME2 NOT NULL, `RutaArchivo` NVARCHAR(500) NOT NULL, `Estado` VARCHAR(20) NOT NULL (`IsUnicode(false)`), `FechaFin` DATETIME2 NULL (`DateTime?`), `IntentosProceso` INT NOT NULL (sin replicar el DEFAULT 0); `CargaEstadoHistorial` — `Id` BIGINT IDENTITY PK, `CargaId` BIGINT NOT NULL FK → `CargaArchivo`, `Estado` VARCHAR(20) NOT NULL (`IsUnicode(false)`), `FechaCambio` DATETIME2 NOT NULL; `Incidencia` — `Id` BIGINT IDENTITY PK, `CargaId` BIGINT NOT NULL FK → `CargaArchivo`, `CargaPeriodoId` BIGINT NULL (solo columna; `CargaPeriodo` no se mapea), `Tipo` VARCHAR(30) NOT NULL (`IsUnicode(false)`), `NumeroFila` INT NULL, `CodigoProducto` NVARCHAR(100) NULL, `PeriodoTexto` NVARCHAR(50) NULL, `Motivo` NVARCHAR(500) NOT NULL, `FechaRegistro` DATETIME2 NOT NULL; relaciones `CargaEstadoHistorial.CargaId → CargaArchivo` e `Incidencia.CargaId → CargaArchivo`; archivos en `ms-control/src/Acity.Domain/Entities/` y `ms-control/src/Acity.Infrastructure/Persistence/Configurations/`; compilar `ms-control/src/Acity.Control.slnx` con 0 advertencias
- [x] T018 [P] [US3] Integrar **ms-carga-masiva** desde `$OUT/carga-masiva` (requiere T013): `CargaArchivo` igual que en T017; `CargaPeriodo` — `Id` BIGINT IDENTITY PK, `CargaId` BIGINT NOT NULL FK → `CargaArchivo`, `Periodo` CHAR(7) NOT NULL (`IsFixedLength`, `IsUnicode(false)`, longitud 7), `Resultado` VARCHAR(20) NOT NULL (`IsUnicode(false)`), `PeriodoReservado` CHAR(7) NULL (`string?`, longitud 7), `FechaResultado` DATETIME2 NOT NULL; `Producto` — `Id` BIGINT IDENTITY PK, `CodigoProducto` NVARCHAR(100) NOT NULL **COLLATE Latin1_General_100_CI_AS_WS** (conservar la collation que genera el scaffold; no sobrescribirla), `NombreProducto` NVARCHAR(200) NOT NULL, `Precio` DECIMAL(18,2) NOT NULL (`decimal`, precisión 18 y escala 2), `CargaPeriodoId` BIGINT NOT NULL FK → `CargaPeriodo`; `Incidencia` igual que en T017; relaciones `CargaPeriodo.CargaId → CargaArchivo`, `Producto.CargaPeriodoId → CargaPeriodo`, `Incidencia.CargaId → CargaArchivo` e `Incidencia.CargaPeriodoId → CargaPeriodo`; `CargaArchivo.UsuarioId` solo columna; archivos en `ms-carga-masiva/src/Acity.Domain/Entities/` y `ms-carga-masiva/src/Acity.Infrastructure/Persistence/Configurations/`; compilar `ms-carga-masiva/src/Acity.CargaMasiva.slnx` con 0 advertencias
- [x] T019 [P] [US3] Integrar **ms-notificaciones** desde `$OUT/notificaciones` (requiere T014): solo `Incidencia` con las mismas columnas y tipos que en T017, con `CargaId` BIGINT NOT NULL y `CargaPeriodoId` BIGINT NULL **solo como columnas de clave** (sin relaciones ni entidades relacionadas); archivos `ms-notificaciones/src/Acity.Domain/Entities/Incidencia.cs` y `ms-notificaciones/src/Acity.Infrastructure/Persistence/Configurations/IncidenciaConfiguration.cs`; compilar `ms-notificaciones/src/Acity.Notificaciones.slnx` con 0 advertencias
- [x] T020 [US3] Extender los cuatro `chk-<s>.cs` (fuera del repositorio) con los pasos 4 a 6 del contrato: cantidad de entidades, lectura LINQ `Set<T>().AsNoTracking().Take(1)` y `Count()` de cada entidad del servicio, y volcado del modelo (tabla, columna, tipo de almacén, nulabilidad, longitud, clave, generación de valores y claves foráneas, en el mismo formato normalizado que el volcado de catálogo del contrato); solo lecturas
- [x] T021 [US1] [US3] Ejecutar los cuatro `chk-<s>.cs` extendidos con `ConnectionStrings__Default="$CONN"`: `CanConnect=True`, `DB_NAME=CargaMasivaApp`, entidades 2, 3, 4 y 1, lectura LINQ sin error (`Rol` con 2 filas; el resto vacío); registrar en `quickstart.md` (escenarios 5)
- [x] T022 [US3] Comparar el volcado del modelo de cada servicio con el catálogo de `CargaMasivaApp` (consulta de `sys.columns`, `sys.foreign_keys` y `sys.indexes` por tabla): 0 diferencias en tabla, columna, tipo, longitud, nulabilidad, clave, generación de valores y claves foráneas; registrar y corregir en el mapeo (no en el esquema) cualquier diferencia; anotar en `quickstart.md` (escenario 6)
- [x] T023 [US3] Comprobar la collation de `CodigoProducto` con LINQ (research D-07): crear una base temporal `CargaMasivaApp_Coll` con el runner (`docker compose run --rm --no-deps -e DB_NAME=CargaMasivaApp_Coll database-runner`), insertar por SQL (no por EF) un carga, un periodo y productos "P0117" y "a", consultar desde un `.cs` fuera del repositorio con el `DbContext` de ms-carga-masiva: `CodigoProducto == "p0117"` debe encontrar "P0117" y `"á"` no debe igualar `"a"`; si falla o EF emite un `COLLATE` conflictivo, registrar el error exacto y quitar `UseCollation` del mapeo de `Producto` (la columna conserva su collation); eliminar la base temporal (escenario 7)
- [x] T024 [US2] Verificar que las entidades de cada servicio coinciden exactamente con la matriz (ms-autenticacion: `Rol`, `Usuario`; ms-control: `CargaArchivo`, `CargaEstadoHistorial`, `Incidencia`; ms-carga-masiva: `CargaArchivo`, `CargaPeriodo`, `Producto`, `Incidencia`; ms-notificaciones: `Incidencia`) listando `ms-<s>/src/Acity.Domain/Entities/`, que 0 entidades están fuera de la matriz y que ningún servicio tiene las siete; registrar en `quickstart.md` (escenarios 3 y 4)

## Phase 6: US4 - Organización conforme a la referencia, sin sobreingeniería (P2)

**Meta**: el cambio se limita a entidades y mapeos en las capas aprobadas, y el arranque no toca el esquema.

**Prueba independiente**: revisar el diff y comparar el esquema antes y después de arrancar los servicios.

- [x] T025 [US4] Tomar la huella del esquema y del registro de `CargaMasivaApp`; reconstruir y levantar con `docker compose up -d --build`; comprobar los cuatro servicios `healthy` y `GET /health` = 200 en 8081–8084
- [x] T026 [US4] Comparar la huella de esquema y `SchemaMigraciones` de `CargaMasivaApp` antes (T025) y después de arrancar y de ejecutar los `chk`: idéntica, sin `__EFMigrationsHistory` y sin operaciones de esquema (escenario 8)
- [x] T027 [US4] Revisar `git diff` y `git status` de la feature: solo archivos nuevos en `ms-<s>/src/Acity.Domain/Entities/` y `ms-<s>/src/Acity.Infrastructure/Persistence/Configurations/` (más las notas de documentos); 0 controladores, comandos, consultas, handlers, endpoints, repositorios por entidad, `BaseEntity`, `DbSet`, navegaciones, `HasIndex`, `HasName`, datos sembrados ni abstracciones; `*.csproj`, `AppDbContext.cs`, `DependencyInjection.cs`, `Program.cs`, `compose.yaml`, `database/` y `/health` sin cambios; 0 `ProjectReference` fuera de cada servicio; sin paquetes ni proyectos de pruebas (escenario 9)
- [x] T028 [US4] Confirmar que no se sobrescribió ningún archivo existente (`git diff --stat` solo muestra archivos añadidos y no modificados dentro de `ms-*`) y que la carpeta temporal de scaffold no está dentro del repositorio

## Phase 7: Polish y documentación

- [x] T029 [P] Añadir una nota breve "Precisado por la Feature 004" en `specs/003-runner-sql-refactor-control/data-model.md` (tabla "Modelo EF Core por servicio": las entidades por servicio pasan a las de la Feature 004) y en `specs/003-runner-sql-refactor-control/research.md` D-04, sin reescribir lo histórico
- [x] T030 [P] Añadir la misma nota en `specs/002-fundacion-microservicios/data-model.md` (tabla "Propietario y alcance": `AppDbContext` sin entidades por ahora)
- [x] T031 Comprobar que no hay contraseñas ni cadenas de conexión en los logs de los cuatro servicios (`docker logs`) ni en las salidas guardadas de los `chk` (`grep -F` sin imprimir el valor) (escenario 10)
- [x] T032 Tomar de nuevo la huella de `CargaMasiva`, `CargaMasivaDb` y los volúmenes y compararla con la de T001: idéntica; `git diff` sin cambios en `.specify/memory/constitution.md` ni `referencias/README.md` (escenarios 11 y 12)
- [x] T033 Eliminar la base temporal `CargaMasivaApp_Coll` (y cualquier otra `CargaMasivaApp_*` creada) y la carpeta temporal de scaffold `$OUT`; conservar los `chk-<s>.cs` en su carpeta temporal para poder repetir la comprobación y registrar su ruta; comprobar que `CargaMasivaApp`, `CargaMasiva` y `CargaMasivaDb` siguen presentes
- [x] T034 Completar la sección "Resultados" de `specs/004-conexion-entidades-por-servicio/quickstart.md` con el resultado real de cada escenario (ejecutado / no ejecutado), actualizar los "Abiertos" de `research.md` y los "Pendientes" de `plan.md`, y dejar escrita la limitación del entorno (motor amd64 emulado, sin soporte de Microsoft; validado solo en este equipo; equipo de evaluación sin validar); indicar como no ejecutado el cuerpo de error de conexión por HTTP
- [x] T035 Revisión final: `git status` (solo cambios de esta feature; sin `.env`, `bin/`, `obj/`, `logs/` ni secretos), `docker compose ps` (todo `healthy` sobre `CargaMasivaApp`), tareas marcadas solo si se verificaron; informar al usuario y entregar el comando de commit sin `Co-Authored-By`

## Dependencias y orden

- **Setup (T001–T002)** → **Foundational (T003)** → **US1 base (T004–T009)** → **US2 selección (T010–T015)** → **US3 mapeo (T016–T024)** → **US4 (T025–T028)** → **Polish (T029–T035)**.
- T011–T014 son manuales y secuenciales (usan la misma herramienta de Control); cada integración de T016–T019 depende de su scaffold (T011–T014, respectivamente) y puede ir en paralelo entre servicios.
- T021 (lectura con entidades) cierra US1 y depende de T016–T020; T024 (matriz) cierra US2 y depende de T016–T019; T023 y T022 dependen de la integración.
- T025 requiere las integraciones compiladas; T026 requiere T025.

## Oportunidades de paralelismo

- T004–T007 (los cuatro `chk-<s>.cs`, archivos distintos).
- T016–T019 (cuatro servicios distintos, una vez hechos sus scaffolds).
- T029 y T030 (documentos de features distintas).

## Estrategia de implementación

- **MVP**: Foundational + US1 base + US2 + US3 (las tres historias P1): conexión comprobada, entidades de la matriz integradas y mapeo equivalente al esquema.
- **Incremento**: US4 (organización y arranque sin tocar el esquema) y Polish (documentos, limpieza y resultados).
- **Cierre**: comando de commit.

## Notas

- Los diferidos (`Usuario` en Control; `Producto`/`CargaPeriodo` en Control; `CargaArchivo` en Notificaciones) se añaden, con el mismo procedimiento, solo cuando sus contratos o casos de uso justifiquen su acceso; no se agregan por necesidades hipotéticas.
- Los resultados de validación se registran en `quickstart.md`; lo no ejecutado se indica como tal.
