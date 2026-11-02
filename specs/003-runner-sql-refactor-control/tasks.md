# Tasks: Reactivación del runner de database/ y refactorización de ms-control

**Input**: `specs/003-runner-sql-refactor-control/` (plan.md, spec.md, research.md, data-model.md, contracts/runner-y-compose.md, quickstart.md)

**Alcance**: `database/run-scripts.sh`, `compose.yaml`, `.env.example` y `ms-control/`; al final, notas de "sustituido por la Feature 003" en los documentos de las features 001 y 002 y los resultados de esta feature. No se tocan `referencias/`, `api-gateway/`, la constitución, los scripts `V001`/`V002` ni el código de `ms-autenticacion`, `ms-carga-masiva` y `ms-notificaciones`. Sin pruebas automatizadas, proyectos de tests, paquetes ni tecnologías nuevas, ni funcionalidades. Verificación manual con `quickstart.md`.

**Reglas de ejecución**
- **No dar por comprobado** lo que `research.md` lista como "Abiertos": cada punto tiene una tarea de comprobación real y, si falla, se registra el error concreto y se aplica la solución mínima compatible con el plan.
- **No modificar recursos ajenos**: las bases `CargaMasiva` y `CargaMasivaDb`, sus datos y los volúmenes (`sqlserver-data`, logs) se conservan; en este equipo hay contenedores y volúmenes de otros proyectos que no se tocan. Nunca usar `docker compose down -v` ni borrar volúmenes. Las bases temporales llevan el prefijo `CargaMasivaApp_` y se eliminan al final; el runner nunca se apunta a `CargaMasiva` ni a `CargaMasivaDb`.
- La contraseña solo por variable de entorno: no imprimirla ni escribirla en archivos versionados. El shell exporta `MSSQL_SA_PASSWORD` y Compose usa esa en vez de la de `.env`; es la que acepta SQL Server en este equipo.
- Pruebas con scripts de prueba (nuevo, modificado, fallido): usar una copia de `database/scripts/` en una carpeta temporal fuera del repositorio, montada en el contenedor del runner; no editar `V001`/`V002`.
- Marcar `[x]` solo tareas realmente ejecutadas y verificadas. **No declarar Docker validado**: los resultados valen solo para este equipo (motor y runner amd64 emulados en Apple silicon, sin soporte oficial de Microsoft); el equipo de evaluación queda sin validar.
- Al cerrar la implementación, entregar al usuario el comando de commit (sin `Co-Authored-By`).

**Formato**: `- [ ] ID [P] [US] Descripción con ruta`. `[P]` = archivos distintos y sin dependencia pendiente. Notación: `<s>` = cada servicio.

## Phase 1: Setup (línea base)

- [x] T001 Registrar en `specs/003-runner-sql-refactor-control/quickstart.md` (sección "Resultados", creándola) la huella inicial de solo lectura de `CargaMasiva` y `CargaMasivaDb` (conteo de objetos, fecha máxima de modificación y filas de `SchemaMigraciones` o `__EFMigrationsHistory`) mediante `docker exec` con `sqlcmd` en el contenedor `sqlserver` y `SQLCMDPASSWORD` tomada del entorno sin imprimirla, y la lista de volúmenes del proyecto (`docker volume ls`); sin modificar nada
- [x] T002 Verificar que `database/scripts/V001__esquema_inicial.sql` y `V002__procedimientos.sql` son idénticos a `referencias/V001__esquema_inicial.sql` y `V002__procedimientos.sql` (`diff -q`), que `git status` solo muestra `ms-control/src/.idea/` y que el contenedor `sqlserver` está `healthy`

## Phase 2: Foundational (bloquea las historias)

- [x] T003 Detener (`docker compose stop`, nunca `down` ni `-v`) los cuatro servicios actuales (`ms-control`, `ms-autenticacion`, `ms-carga-masiva`, `ms-notificaciones`) para que las pruebas de Compose no los afecten; `sqlserver` sigue en marcha
- [x] T004 Añadir a `compose.yaml` el servicio `database-runner` según `contracts/runner-y-compose.md`: `build: ./database`, `image: carga-masiva-database-runner:local`, `platform: linux/amd64`, `restart: "no"`, `depends_on: sqlserver: condition: service_healthy`, entorno `DB_HOST: sqlserver`, `DB_PORT: "1433"`, `DB_NAME: ${DB_NAME:-CargaMasivaApp}` y `MSSQL_SA_PASSWORD: ${MSSQL_SA_PASSWORD:?Defina MSSQL_SA_PASSWORD}`; sin puertos ni volúmenes; todavía sin cambiar los `depends_on` de los servicios
- [x] T005 [P] Cambiar `DB_NAME=CargaMasivaDb` por `DB_NAME=CargaMasivaApp` en `.env.example` y en el `.env` local (no versionado; no mostrar ni tocar la contraseña)
- [x] T006 Validar con `docker compose config` (sin imprimir secretos) que el Compose resuelve y construir la imagen con `docker compose build database-runner`

## Phase 3: US1 - El runner crea y versiona el esquema (P1)

**Meta**: el runner aplica solo versiones pendientes sobre una base nueva y vacía, la registra, falla de forma clara y no toca bases ajenas.

**Prueba independiente**: ejecutar el runner contra bases temporales `CargaMasivaApp_T*` y comparar el catálogo con `CargaMasiva` (solo lectura, idéntica a `referencias/`).

- [x] T007 [US1] En `database/run-scripts.sh` eliminar el valor por defecto `CargaMasiva` de `DB_NAME` y hacerlo obligatorio (`: "${DB_NAME:?Defina DB_NAME (variable de entorno)}"`), conservando la validación de caracteres; actualizar el comentario de cabecera (ya no "se ejecuta una vez por arranque" de un servicio retirado) sin cambiar el resto del comportamiento
- [x] T008 [US1] En `database/run-scripts.sh`, después de la espera a `master` y antes del bootstrap, añadir la comprobación previa de la base (research D-03; FR-013): consulta de catálogo desde `master` con cuatro resultados: base inexistente → continuar; existe `dbo.__EFMigrationsHistory` → error con mensaje claro en español y código ≠ 0; sin `dbo.SchemaMigraciones` y con tablas de usuario → error claro y código ≠ 0; en otro caso → continuar. En los errores no se ejecuta bootstrap ni scripts. Sin imprimir la contraseña
- [x] T009 [US1] Reconstruir la imagen (`docker compose build database-runner`) y ejecutar el runner con `docker compose run --rm --no-deps -e DB_NAME=CargaMasivaApp_T1 database-runner`: debe crear la base, aplicar `V001` y `V002`, registrar ambos y salir con 0 (quickstart 2); comparar con consultas de catálogo contra `CargaMasiva` (solo lectura): tablas, columnas y tipos, collation de `CodigoProducto`, nulabilidad, valores por defecto, CHECK, claves foráneas, índices incluido el filtro `UX_CargaPeriodo_PeriodoReservado`, 2 roles base, 2 procedimientos y `SchemaMigraciones` con 2 filas y checksums; anotar diferencias (esperadas: ninguna)
- [x] T010 [US1] Repetir la ejecución del runner sobre `CargaMasivaApp_T1` sin scripts nuevos: debe informar "Sin scripts pendientes", salir con 0 y dejar esquema y registro idénticos (quickstart 3, runner)
- [x] T011 [US1] Con una copia temporal de `database/scripts/` (fuera del repositorio) que añada `V003__prueba.sql` inocuo y montada con `-v <carpeta>:/database/scripts:ro`, ejecutar el runner sobre `CargaMasivaApp_T1`: debe aplicar y registrar solo `V003` sin reejecutar `V001`/`V002` (quickstart 4)
- [x] T012 [US1] Con copias temporales: (a) `V002` modificado y (b) un `V000__menor.sql` de número menor que el último aplicado; en ambos casos el runner debe salir con error sin aplicar nada ni reescribir el registro (quickstart 5)
- [x] T013 [US1] Con una copia temporal que añada un `V004__falla.sql` que falle a mitad (p. ej., sentencia válida seguida de una división por cero) sobre `CargaMasivaApp_T1`: debe revertirse, no registrarse, salir ≠ 0 y conservar el registro anterior (quickstart 6, runner)
- [x] T014 [US1] Lanzar dos ejecuciones simultáneas del runner contra una base temporal nueva `CargaMasivaApp_T2` con una copia temporal que añada un script lento; solo una debe aplicar y la otra no duplicar ni corromper nada (quickstart 7); registrar el mensaje de cada una
- [x] T015 [US1] Detener solo el contenedor `sqlserver` del proyecto (`docker compose stop sqlserver`), ejecutar el runner con `--no-deps` y comprobar la espera acotada y el error sin crear nada (quickstart 9, ~2 min); volver a iniciar `sqlserver` y esperar a `healthy`
- [x] T016 [US1] Revisar la salida de todas las ejecuciones anteriores (guardarlas en un archivo temporal fuera del repositorio) y comprobar con `grep` sin imprimir el valor que no aparece la contraseña ni `Password=` (quickstart 10)

## Phase 4: US2 - Compose arranca los servicios solo si el runner termina bien (P1)

**Meta**: orden SQL Server → runner → servicios; con el runner fallido no arranca ninguno.

**Prueba independiente**: levantar Compose sobre una base nueva, luego forzar el fallo del runner con otra base temporal.

- [x] T017 [US2] En `compose.yaml` cambiar los cuatro servicios a `depends_on: database-runner: condition: service_completed_successfully`; retirar la dependencia de `ms-control` de `sqlserver` y las de los otros tres de `ms-control`; cambiar `${DB_NAME:-CargaMasivaDb}` por `${DB_NAME:-CargaMasivaApp}` en las cuatro cadenas de conexión; reemplazar el comentario de cabecera por la descripción actual (el esquema lo aplica el runner de `database/`; los servicios no migran). Conservar `sqlserver`, healthchecks, puertos, volúmenes de logs y `restart: "no"`
- [x] T018 [US2] Validar con `docker compose config` que las dependencias resuelven como se espera (sin imprimir secretos)
- [x] T019 [US2] Prueba de fallo en Compose: preparar en la instancia una base temporal vacía `CargaMasivaApp_Fallo` con un procedimiento `dbo.usp_ReservarPeriodo` preexistente (conflicto para `V002`), y con los servicios detenidos ejecutar `DB_NAME=CargaMasivaApp_Fallo docker compose up -d ms-autenticacion`; debe quedar `V001` aplicado y `V002` revertido y sin registrar, el runner salir ≠ 0 y ningún microservicio arrancar; anotar el comportamiento exacto de `docker compose up -d` (código de salida y mensaje) (quickstart 6, Compose)
- [x] T020 [US2] Levantar con `docker compose up -d --build` (sin variables de prueba; `DB_NAME=CargaMasivaApp`, base inexistente): comprobar el orden sqlserver → `database-runner` (sale 0) → los cuatro servicios `healthy` (puertos 8081–8084), con las marcas de tiempo de `docker compose ps -a`/eventos (quickstart 1)
- [x] T021 [US2] Comparar el catálogo de `CargaMasivaApp` con la referencia como en T009 (7 tablas, índices, CHECK, FK, collation, 2 roles, 2 procedimientos, `SchemaMigraciones` con 2 filas) (quickstart 2, base real)
- [x] T022 [US2] Ejecutar de nuevo `docker compose up -d` sin scripts nuevos: comprobar si el runner se vuelve a ejecutar y termina con "Sin scripts pendientes" y 0, sin cambiar esquema ni registro, y que los servicios siguen sanos; anotar el comportamiento real (quickstart 3, Compose)
- [x] T023 [US2] Tomar una huella del esquema y del registro de `CargaMasivaApp`, reiniciar los cuatro servicios y comparar: idénticas, sin objetos nuevos ni `__EFMigrationsHistory` (quickstart 13)

## Phase 5: US3 - ms-control sin EF Core Migrations ni migración al iniciar (P1)

**Meta**: Control arranca sin tocar el esquema y conserva la fundación.

**Prueba independiente**: compilar Control, buscar referencias a migraciones e inicializador y arrancarlo contra la base del runner.

- [x] T024 [US3] En `ms-control/src/Acity.Api/Program.cs` retirar `using Acity.Infrastructure.Persistence;`, el comentario y la llamada `await DatabaseInitializer.InitializeAsync(...)`; conservar el resto (Serilog, controladores, manejador de excepciones, status code pages, `try/catch/finally`)
- [x] T025 [US3] Eliminar `ms-control/src/Acity.Infrastructure/Persistence/DatabaseInitializer.cs` y el directorio `ms-control/src/Acity.Infrastructure/Persistence/Migrations/` (`20261102144113_InitialSchema.cs` y su `Designer`, `20261102144125_StoredProcedures.cs` y su `Designer`, `AppDbContextModelSnapshot.cs`); conservar `.config/dotnet-tools.json` y el paquete `Design` (los exige la ingeniería inversa, FR-029)
- [x] T026 [US3] Compilar `dotnet build ms-control/src/Acity.Control.slnx` (0 advertencias nuevas, sin referencias entre servicios ni paquetes no autorizados) y buscar con `grep` que no queden referencias a `Migrations`, `DatabaseInitializer`, `MigrateAsync` ni `__EFMigrationsHistory` en `ms-control/src` fuera de `bin`/`obj`
- [x] T027 [US3] Reconstruir y levantar `ms-control` (`docker compose up -d --build ms-control`): debe arrancar tras el runner, `GET http://localhost:8082/health` = 200 con `Response<string>`, el log no muestra migraciones y `CargaMasivaApp` no tiene `__EFMigrationsHistory` (quickstart 12)

## Phase 6: US4 - Modelo de Control limitado a lo que necesita (P2)

**Meta**: se retiran las entidades y mapeos creados solo para administrar el esquema, con decisión documentada por entidad.

**Prueba independiente**: repetir la búsqueda de referencias, comprobar que lo retirado no tenía usos y que Control compila y arranca.

- [x] T028 [US4] Repetir antes de retirar la búsqueda de referencias a cada una de las siete entidades (`Rol`, `Usuario`, `CargaArchivo`, `CargaPeriodo`, `Producto`, `Incidencia`, `CargaEstadoHistorial`) en los `*.cs`, `*.csproj` y `*.json` de los cuatro servicios y del Gateway, excluyendo `bin`/`obj`; registrar en `specs/003-runner-sql-refactor-control/quickstart.md` ("Resultados") la tabla final por entidad: referencias actuales, función de Control implementada o aprobada que la use (o su ausencia) y decisión. Una entidad con uso dudoso se conserva provisionalmente hasta reunir evidencia; solo se conserva en la decisión final con una referencia actual o una función implementada o aprobada. Si aparece una referencia, esa entidad se conserva y se omite de T029 y T030
- [x] T029 [US4] Eliminar `ms-control/src/Acity.Domain/Entities/` (las siete clases retiradas según T028); conservar `Acity.Domain.csproj`
- [x] T030 [US4] Eliminar `ms-control/src/Acity.Infrastructure/Persistence/Configurations/` (las siete configuraciones retiradas según T028)
- [x] T031 [US4] Simplificar `ms-control/src/Acity.Infrastructure/Persistence/AppDbContext.cs` a la forma de los otros tres servicios: retirar `ConfigureConventions` y el `using Microsoft.EntityFrameworkCore.Metadata.Conventions;`, conservar `OnModelCreating` con `ApplyConfigurationsFromAssembly`
- [x] T032 [US4] Compilar `ms-control/src/Acity.Control.slnx` (0 advertencias, 0 referencias rotas), buscar con `grep` que no quedan entidades ni configuraciones retiradas, reconstruir y levantar `ms-control` y comprobar `GET /health` = 200 (quickstart 11)
- [x] T033 [P] [US4] Compilar `ms-autenticacion/src/Acity.Autenticacion.slnx`, `ms-carga-masiva/src/Acity.CargaMasiva.slnx` y `ms-notificaciones/src/Acity.Notificaciones.slnx` por sus rutas para confirmar que siguen compilando sin cambios

## Phase 7: US5 - Ingeniería inversa para los modelos de cada servicio (P2)

**Meta**: el procedimiento de `dotnet ef dbcontext scaffold` queda verificado y documentado sin añadir entidades.

**Prueba independiente**: generar una tabla contra la base del runner en una carpeta temporal y descartarla.

- [x] T034 [US5] Ejecutar desde `ms-control/` `dotnet tool restore` y `dotnet ef dbcontext scaffold` contra `CargaMasivaApp` (cadena de conexión armada desde el entorno, host `localhost,14333`, sin imprimirla; `Encrypt=True;TrustServerCertificate=True`) con `Microsoft.EntityFrameworkCore.SqlServer`, `--project src/Acity.Infrastructure`, `--startup-project src/Acity.Api`, `--table dbo.Rol`, `--no-onconfiguring` y salida (`--output-dir`, `--context-dir`) a una carpeta temporal fuera de las capas; comprobar que solo genera esa tabla, que no se ejecuta en el arranque y que `git status` no muestra archivos nuevos en el repositorio; descartar la salida. Si falla, registrar el error exacto y la solución mínima (sin añadir tecnologías)
- [x] T035 [US5] Anotar en `specs/003-runner-sql-refactor-control/data-model.md`, en el procedimiento, el comando verificado y los hallazgos (sin la cadena de conexión), y dejar explícito que los tres servicios sin entidades aplicarán el procedimiento en su propia feature funcional

## Phase 8: US6 - Validación sobre una base nueva sin mezclar historiales (P2)

**Meta**: el runner no mezcla historiales y el esquema creado cumple las reglas de la Feature 001.

**Prueba independiente**: bases temporales que simulan historiales ajenos; reglas sobre una base creada por el runner.

- [x] T036 [US6] Probar la comprobación previa (quickstart 8) con bases temporales: (a) `CargaMasivaApp_EF` con una tabla `dbo.__EFMigrationsHistory` simulada, (b) `CargaMasivaApp_Tablas` con una tabla de usuario y sin `SchemaMigraciones`; en ambas, tomar la huella del catálogo antes y después de ejecutar el runner (`--no-deps`): error claro, código ≠ 0, base idéntica y sin `SchemaMigraciones` creada; con Compose, los servicios no arrancan
- [x] T037 [US6] Sobre una base temporal creada por el runner (`CargaMasivaApp_T3`), repetir manualmente los escenarios de base de la Feature 001 de `specs/001-persistencia-integridad-carga/quickstart.md` (reserva de periodo, índice filtrado, comparación de códigos, matriz de estados, fallo terminal) y anotar que los resultados coinciden con los obtenidos con el runner y con EF anteriores (quickstart 14)
- [x] T038 [US6] Tomar de nuevo la huella de `CargaMasiva`, `CargaMasivaDb` y los volúmenes y compararla con la de T001: idéntica (quickstart 16)

## Phase 9: Polish y documentación

- [x] T039 [P] Añadir notas breves "Sustituido por la Feature 003" (sin reescribir lo histórico) en `specs/002-fundacion-microservicios/` donde corresponda: `spec.md` (FR-022, FR-025 a FR-034, US2, US3, SC-006 a SC-009), `plan.md`, `research.md` (D-05 a D-07), `data-model.md`, `contracts/http-y-operacion.md`, `quickstart.md` (escenarios 3 a 10) y `tasks.md` (nota de cabecera)
- [x] T040 [P] Añadir en `specs/001-persistencia-integridad-carga/` (`plan.md`, `research.md` R-12 y R-14, `data-model.md`, `spec.md` Assumptions) la nota de que el runner de `database/` vuelve a ser el mecanismo activo (constitución v3.0.0 y Feature 003), conservando el historial
- [x] T041 Eliminar solo las bases temporales `CargaMasivaApp_*` creadas en esta feature (T009 a T037) y comprobar que ya no existen y que `CargaMasivaApp`, `CargaMasiva`, `CargaMasivaDb` y los volúmenes siguen presentes; retirar las carpetas temporales de scripts y salidas
- [x] T042 Comprobar que no hay paquetes ni proyectos nuevos y ningún proyecto o dependencia de pruebas (`git diff` de `*.csproj`; búsqueda de proyectos de tests) (quickstart 17)
- [x] T043 Completar la sección "Resultados" de `specs/003-runner-sql-refactor-control/quickstart.md` con el resultado real de cada escenario (ejecutado / no ejecutado), actualizar los "Abiertos" de `research.md` y los "Pendientes" de `plan.md`, y dejar escritas la limitación del entorno (motor y runner amd64 emulados, sin soporte de Microsoft; validado solo en este equipo) y que el equipo de evaluación no está validado
- [x] T044 Revisión final: `git status` (solo cambios de esta feature; sin `.env`, `bin/`, `obj/`, `logs/` ni secretos), `docker compose ps` (sqlserver y servicios `healthy` sobre `CargaMasivaApp`), tareas marcadas solo si se verificaron; informar al usuario, incluyendo que la lista de Pendientes de la constitución (adaptación y validación del runner y limpieza de Control) queda cumplida y que su enmienda la decide el usuario; entregar el comando de commit sin `Co-Authored-By`

## Dependencias y orden

- **Setup (T001–T002)** → **Foundational (T003–T006)** → **US1 (T007–T016)** → **US2 (T017–T023)** → **US3 (T024–T027)** → **US4 (T028–T033)** → **US5 (T034–T035)** → **US6 (T036–T038)** → **Polish (T039–T044)**.
- US1 depende del servicio `database-runner` de Foundational. US2 depende de US1 (runner modificado). US3 puede editarse en paralelo a US1/US2 (archivos distintos), pero su validación con Compose (T027) requiere US2. US4 sigue a US3 (sobre la misma solución de Control). US5 requiere `CargaMasivaApp` creada en T020. US6 requiere US1 (comprobación previa).
- T019 se ejecuta **antes** de T020 (servicios detenidos desde T003) para no mezclar la prueba de fallo con el arranque correcto.

## Oportunidades de paralelismo

- T005 (`.env.example`/`.env`) en paralelo con T004 y T006 (archivos distintos).
- T033 (compilar los otros tres servicios) en paralelo con cualquier tarea que no cambie su código.
- T039 y T040 (documentos de features distintas).
- Los cambios de código de US3 (T024–T025) en paralelo con la edición del runner (T007–T008), por ser archivos distintos.

## Estrategia de implementación

- **MVP**: Foundational + US1 + US2 + US3 (las tres historias P1): el runner funciona, Compose impone el orden y Control no migra.
- **Incremento**: US4 (limpieza del modelo), US5 (procedimiento verificado) y US6 (no mezclar historiales y reglas de la Feature 001).
- **Cierre**: Polish (documentos y resultados) y comando de commit.

## Notas

- Las entidades retiradas se regeneran con el procedimiento de US5 cuando una feature funcional las necesite; no se agregan por necesidades futuras supuestas.
- Los resultados de validación se registran en `quickstart.md`; lo no ejecutado se indica como tal.
