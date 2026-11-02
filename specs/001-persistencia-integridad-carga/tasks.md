# Tasks: Persistencia y reglas de integridad para la carga masiva

**Input**: `specs/001-persistencia-integridad-carga/` (plan.md, spec.md, research.md, data-model.md, contracts/procedimientos-almacenados.md, quickstart.md)

> **Estrategia de despliegue sustituida (constitución v2.0.0)**: las tareas del runner y de los scripts (T004–T008, T022) se ejecutaron y verificaron como implementación anterior y se conservan como historial; **ya no son el flujo activo** y se consideran sustituidas por migraciones EF Core administradas desde ms-control. No hay tareas del runner pendientes. Las reglas de negocio, el modelo y los contratos de procedimientos se conservan. Quedan para la feature de ms-control: la migración inicial (usando `referencias/V001__esquema_inicial.sql` como referencia), la incorporación de los procedimientos mediante SQL en las migraciones (`referencias/V002__procedimientos.sql`), la aplicación automática al iniciar y su verificación. `database/` se conserva sin eliminar.

**Alcance**: solo `database/` y la declaración de SQL Server y del runner en `compose.yaml`. No se tocan `ms-*` ni `api-gateway` (consumidor, Autenticación y Polly son de sus features). Sin pruebas automatizadas (constitución §11): la verificación es manual con `quickstart.md`. Sin EF Migrations, sin Dapper, sin contraseñas en scripts.

**Formato**: `- [ ] ID [P] [US] Descripción con ruta`. `[P]` = archivos distintos y sin dependencia pendiente. Los scripts `V001`/`V002` son un solo archivo cada uno, por eso sus tareas son secuenciales.

## Phase 1: Setup

- [x] T001 Crear `database/scripts/` y `database/` según plan.md (Project Structure); nombres de archivo: `V001__esquema_inicial.sql`, `V002__procedimientos.sql`, `run-scripts.sh`, `Dockerfile`
- [x] T002 [P] Verificar contra la documentación oficial el nombre exacto de la collation `Latin1_General_100_CI_AS_WS` (insensible a mayúsculas, sensible a acentos y ancho; research R-07) y anotar el resultado en `specs/001-persistencia-integridad-carga/research.md` R-07; si el nombre difiere, corregirlo en data-model.md
- [x] T003 [P] Verificar en la documentación oficial `sp_getapplock` (modo exclusivo, propietario de sesión, timeout 0) y anotar el resultado en research R-02/R-12 (pendiente declarado en research)

## Phase 2: Foundational (bloquea todo)

**Propósito**: motor SQL Server y runner de scripts versionados (único responsable del esquema; research R-12).

- [x] T004 Declarar en `compose.yaml` el servicio SQL Server 2022 con `platform: linux/amd64` explícito y contraseña de SA tomada de una variable de entorno (sin valores en el repositorio); declarar el servicio del runner de un solo uso que depende de que SQL Server esté sano
- [x] T005 Escribir `database/run-scripts.sh`: aplica `Vnnn__*.sql` en orden dentro de **una sola sesión** que mantiene un cerrojo de aplicación exclusivo (timeout 0, otro recurso distinto del consumidor); consulta `SchemaMigraciones` antes de cada script y omite los aplicados; falla si el checksum de un script aplicado cambió o si un script nuevo tiene número menor que el último aplicado; cada script corre en una transacción con `XACT_ABORT`, sale con código de error (`-b`) y no se registra si falla; crea la base si no existe
- [x] T006 Escribir `database/Dockerfile` del runner usando como base la imagen de SQL Server (incluye `sqlcmd`; research R-11) y copiar `scripts/` y `run-scripts.sh`
- [x] T007 Encabezado `SET QUOTED_IDENTIFIER ON` en `V001__esquema_inicial.sql` y tabla `SchemaMigraciones` (`Script NVARCHAR(200)` PK, `Checksum CHAR(64)`, `FechaAplicado DATETIME2`, todas No nulas). **Desviación**: la crea el runner de forma idempotente en `database/bootstrap.sql` (necesita existir antes de consultar qué está aplicado), no V001
- [x] T008 Verificación operativa (research R-11): levantar SQL Server amd64 con Compose, comprobar que arranca y acepta conexiones, ejecutar el runner contra él y registrar el resultado (incluido si la emulación falla en este equipo) en research R-11. Si no funciona, **detener y reportar**: no se declara Docker funcional ni se cambia el motor aprobado

**Checkpoint**: el runner aplica y registra scripts; quickstart 1 y 2 verificables.

## Phase 3: US8 - Identidad y roles (P2, pero base de las FK) 

**Meta**: roles y usuarios persistidos; la creación de los usuarios de demostración la hace ms-autenticacion (fuera de esta feature).

- [x] T009 [US8] En `V001__esquema_inicial.sql` crear `Rol` (`Id SMALLINT` PK, `Nombre NVARCHAR(50)` UNIQUE No nulo) e insertar los roles de referencia “Administrador” y “Operador” (sin secretos), de forma que reaplicar no duplique
- [x] T010 [US8] En `V001__esquema_inicial.sql` crear `Usuario` (`Id INT IDENTITY` PK; `Email NVARCHAR(256)` UNIQUE No nulo; `PasswordHash VARCHAR(255)` No nulo; `RolId SMALLINT` FK → `Rol` No nulo); sin usuarios ni contraseñas en el script

## Phase 4: US1 - Registrar la carga y su responsable (P1)

**Meta**: cada carga con idCarga, archivo, responsable, fecha y estado.

- [x] T011 [US1] En `V001__esquema_inicial.sql` crear `CargaArchivo`: `Id BIGINT IDENTITY` PK; `NombreArchivo NVARCHAR(260)` No nulo; `UsuarioId INT` FK → `Usuario` No nulo; `FechaRecepcion DATETIME2` No nulo; `RutaArchivo NVARCHAR(500)` No nulo; `Estado VARCHAR(20)` CHECK ∈ {Pendiente, En proceso, Cargado, Finalizado, Notificado, Fallido} No nulo; `FechaFin DATETIME2` nulo; `IntentosProceso INT` DEFAULT 0 No nulo. Sin columna binaria ni tipos `MAX`
- [x] T012 [US1] En `V001__esquema_inicial.sql` crear el índice `CargaArchivo(UsuarioId, FechaRecepcion)` para la consulta del Operador sobre sus cargas

## Phase 5: US2 - Admitir, rechazar o bloquear periodos (P1)

**Meta**: resultado por periodo separado del estado de la carga y protección de concurrencia por restricciones.

- [x] T013 [US2] En `V001__esquema_inicial.sql` crear `CargaPeriodo`: `Id BIGINT IDENTITY` PK; `CargaId BIGINT` FK → `CargaArchivo` No nulo; `Periodo CHAR(7)` CHECK formato AAAA-MM No nulo; `Resultado VARCHAR(20)` CHECK ∈ {Admitido, Completado, Rechazado, Bloqueado, Fallido, NoProcesado} No nulo; `PeriodoReservado CHAR(7)` nulo; `FechaResultado DATETIME2` No nulo; UNIQUE `(CargaId, Periodo)`
- [x] T014 [US2] En `V001__esquema_inicial.sql` añadir a `CargaPeriodo` el CHECK `(Resultado IN ('Admitido','Completado') AND PeriodoReservado = Periodo) OR (Resultado NOT IN ('Admitido','Completado') AND PeriodoReservado IS NULL)` y el **índice único filtrado** sobre `PeriodoReservado` con `WHERE PeriodoReservado IS NOT NULL` (no usar `IN`/`OR` en el filtro; data-model.md)
- [x] T015 [US2] En `V002__procedimientos.sql` (encabezado `SET QUOTED_IDENTIFIER ON`) implementar `usp_ReservarPeriodo(@CargaId, @Periodo, @Resultado OUTPUT)` según contracts/: falla de inmediato si hay transacción abierta; abre y confirma su propia transacción; si ya existe resultado de (CargaId, Periodo) lo devuelve sin cambios; excluyendo la carga actual, otro periodo Completado → Rechazado, otro Admitido → Bloqueado, si no → Admitido con `PeriodoReservado`; Rechazado/Bloqueado inserta la incidencia de grupo en la misma transacción sin reservar; ante choque con el índice filtrado reevalúa **una vez** y, si vuelve a chocar, falla con error técnico (sin bucle)

## Phase 6: US3 - Productos sin duplicar CodigoProducto (P1)

**Meta**: unicidad global con la comparación aprobada; sin llave compuesta con periodo.

- [x] T016 [US3] En `V001__esquema_inicial.sql` crear `Producto`: `Id BIGINT IDENTITY` PK; `CodigoProducto NVARCHAR(100)` con la collation verificada en T002, No nulo; `NombreProducto NVARCHAR(200)` No nulo; `Precio DECIMAL(18,2)` No nulo; `CargaPeriodoId BIGINT` FK → `CargaPeriodo` No nulo
- [x] T017 [US3] En `V001__esquema_inicial.sql` crear el índice **único global** sobre `Producto(CodigoProducto)`, sin llave compuesta con periodo

## Phase 7: US6 - Incidencias e historial (P2)

**Meta**: incidencias persistentes y trazabilidad de estados.

- [x] T018 [US6] En `V001__esquema_inicial.sql` crear `Incidencia`: `Id BIGINT IDENTITY` PK; `CargaId BIGINT` FK No nulo; `CargaPeriodoId BIGINT` FK nulo; `Tipo VARCHAR(30)` CHECK ∈ {PeriodoRechazado, PeriodoBloqueado, FilaOmitida, Existente, NoProcesado, ErrorTecnico, ErrorNotificacion} No nulo; `NumeroFila INT` nulo; `CodigoProducto NVARCHAR(100)` nulo; `PeriodoTexto NVARCHAR(50)` nulo; `Motivo NVARCHAR(500)` No nulo; `FechaRegistro DATETIME2` No nulo; índice `(CargaId, Id)`
- [x] T019 [US6] En `V001__esquema_inicial.sql` crear `CargaEstadoHistorial`: `Id BIGINT IDENTITY` PK; `CargaId BIGINT` FK No nulo; `Estado VARCHAR(20)` con el mismo CHECK de seis estados, No nulo; `FechaCambio DATETIME2` No nulo; índice `(CargaId, Id)`

## Phase 8: US5 y US7 - Atomicidad, fallos y finalización (P1/P2)

**Meta**: transiciones de estado con historial y liberación de reservas ante fallo terminal.

- [x] T020 [US5] En `V002__procedimientos.sql` implementar `usp_CambiarEstadoCarga(@CargaId, @NuevoEstado, @PeriodoFallido NULL, @Cambiado OUTPUT)` según contracts/: falla si hay transacción abierta; valida la matriz de data-model.md (Pendiente → En proceso|Fallido; En proceso → Cargado|Fallido; Cargado → Finalizado; Finalizado → Notificado; Notificado y Fallido terminales); actualiza `Estado` y `FechaFin` al llegar a Finalizado; inserta historial; una repetición o transición inválida no escribe nada y devuelve 0
- [x] T021 [US5] En `usp_CambiarEstadoCarga` (mismo archivo), al pasar a Fallido: el periodo indicado en `@PeriodoFallido` (si es Admitido) queda Fallido; los demás Admitido quedan NoProcesado con una incidencia `NoProcesado` “No procesado por interrupción de la carga” en la misma transacción; todos con `PeriodoReservado` NULL; los Completado se conservan (FR-048)

## Phase 9: Cierre y verificación manual

- [x] T022 Ejecutar el runner desde cero contra una base vacía y revisar quickstart 1–3 (scripts registrados con checksum, 8 tablas, sin binario del Excel ni contraseñas)
- [x] T023 Verificar con consultas manuales los escenarios de base de quickstart 5–9, 20, 14 (parte de base) y 25–27 que no dependen del consumidor; el 10 y la omisión de precio/texto del 25 son lógica de la aplicación (la base redondea 10.005 a 10.01 sin avisar, por eso valida la aplicación) (reserva, Rechazado/Bloqueado, índice filtrado, comparación de códigos con `Trim()`, acentos, matriz de transiciones y liberación de reservas); anotar resultados en `quickstart.md`
- [x] T024 [P] Actualizar `specs/001-persistencia-integridad-carga/checklists/requirements.md` o research con los hallazgos de T002, T003 y T008 y las discrepancias encontradas
- [x] T025 Confirmar que no hay contraseñas, SA ni secretos en `database/` ni `compose.yaml` (solo variables de entorno)

## Dependencias

- Phase 1 → Phase 2 (T004–T007) → T008 (puerta operativa: si falla, no continuar).
- V001 en orden: T007 → T009–T010 (Rol, Usuario) → T011–T012 (CargaArchivo) → T013–T014 (CargaPeriodo) → T016–T017 (Producto) → T018–T019 (Incidencia, historial); todas editan el mismo archivo.
- V002: T015 depende de T013–T014 y T018; T020–T021 dependen de T011, T013, T018, T019.
- Phase 9 depende de todo lo anterior.

## Paralelismo

- T002, T003 en paralelo (solo documentación).
- T005 y T006 pueden escribirse en paralelo con T007 (archivos distintos).
- T024 en paralelo con T025.

## Estrategia

1. Setup y verificación documental (T001–T003).
2. Runner y motor (T004–T008); si T008 falla, reportar antes de seguir.
3. Esquema V001 por historias (T009–T019) y procedimientos V002 (T015, T020–T021).
4. Verificación manual (T022–T025).

MVP: Phases 1–2 + US1, US2, US3 y US5 (tablas y los dos procedimientos). US6/US7/US8 completan el esquema y no añaden mecanismos nuevos.

## Fuera de alcance (diferido)

Consumidor, cerrojo del consumidor, recuperación, Polly, usuarios de demostración e `PasswordHasher` (ms-autenticacion), archivo ilegible, notificaciones no publicadas.
