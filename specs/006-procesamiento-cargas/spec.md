# Feature Specification: Procesamiento de cargas en ms-carga-masiva

**Feature Branch**: `006-procesamiento-cargas` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v3: Q1 y Q2 resueltas por decisión autorizada del usuario; H1, H2 y H3 de la revisión de cierre resueltos por el usuario, 2026-11-03)

**Input**: Consumir la cola `carga_masiva` con un `BackgroundService` dentro de `Acity.Api` de ms-carga-masiva, recuperar el Excel de SeaweedFS y procesarlo con ClosedXML aplicando las reglas aprobadas de periodos, normalización, duplicados, valores ausentes o inválidos, transacciones e incidencias; gestionar estados, exclusividad del consumidor, reentregas y recuperación; publicar en `notificaciones` al completar. Incluye la comprobación con el consumidor real de la carrera documentada en la feature 005. Esta etapa genera solo la especificación.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v3.2.1; `[F001 …]` = `specs/001-persistencia-integridad-carga/` (spec FR-xxx, research R-xx, data-model, contrato de procedimientos); `[F004]` = entidades por servicio; `[F005 …]` = `specs/005-recepcion-registro-cargas/` (contratos implementados); `[Decisión]` = decisión explícita del usuario (incluye el prompt de esta feature); `[Observado]` = comprobado leyendo el repositorio en esta etapa. **Pendiente** = no decidido; no es requisito. Las reglas de F001 y de la constitución **se aplican sin reinterpretarse**; esta spec las referencia y solo precisa su uso en el consumidor.

## Contexto

**Estado observado** [Observado]:
- ms-carga-masiva tiene solo `GET /health`, el manejador global, `Response<T>`, MediatR/FluentValidation y las entidades `CargaArchivo`, `CargaPeriodo`, `Producto` (con `CodigoProducto` en collation `Latin1_General_100_CI_AS_WS`) e `Incidencia` (F004). Sin RabbitMQ, SeaweedFS, ClosedXML ni Polly. En Compose solo recibe la cadena de conexión.
- Base `CargaMasivaApp` (V001–V003): `usp_ReservarPeriodo` (exige la carga En proceso; idempotente por `(CargaId, Periodo)`; inserta la incidencia de grupo de Rechazado/Bloqueado) y `usp_CambiarEstadoCarga` (matriz, historial, `FechaFin` en Finalizado, liberación de reservas y `NoProcesado` al pasar a Fallido; parámetro opcional `@EstadoEsperado` de V003). Tipos de incidencia admitidos: `PeriodoRechazado`, `PeriodoBloqueado`, `FilaOmitida`, `Existente`, `NoProcesado`, `ErrorTecnico`, `ErrorNotificacion`.
- Contratos implementados por la 005 [F005 contracts]: intercambio directo durable `carga_masiva`, cola durable `carga_masiva`, clave `carga_masiva`; mensaje persistente JSON `{ "idCarga", "rutaArchivo", "usuario" }` con `rutaArchivo` = `seaweed://cargas/AAAA/MM/<guid>.xlsx` (ruta en el filer tras `seaweed://`); Control puede publicar duplicados y puede dejar Fallido una carga cuya publicación no se confirmó. **Obligación del consumidor**: si la carga no está Pendiente, confirmar y descartar sin reprocesar [F001 R-02 punto 6] [F005 contrato de mensajería].
- Excel de ejemplo [Observado]: una hoja ("Productos"), fila 1 con los encabezados `Periodo`, `CodigoProducto`, `NombreProducto`, `Precio`; 200 filas de datos con tres periodos. Solo ilustra el formato; no define límites ni validaciones [Constitución §7].

**Referencias GitHub** (`referencias/repositorios.md`): acceso web parcial. Clean Architecture: `Todo.Infrastructure` con `Persistence/` y `Repositories/`; `TodoController` delega en MediatR. API Gateway: no aplica a esta feature. Ninguna referencia muestra consumidores, `BackgroundService`, lectura de Excel ni almacenamiento de archivos.

**Reglas aprobadas que esta feature aplica** (procedencia; no se repiten en detalle):

| Tema | Regla | Fuente |
|---|---|---|
| Agrupación y admisión por periodo | Rechazado si otro periodo igual está Completado; Bloqueado si está Admitido; la carga actual no se cuenta; un grupo no detiene a los demás; grupos rechazados o bloqueados no reservan | [README §3] [Constitución §7] [F001 FR-009–015, R-04] |
| Unicidad de `CodigoProducto` | Global, sin llave compuesta; existente (incluso en otro periodo) → Existente; primera fila aceptada en el orden original gana, aunque los periodos estén intercalados; sin actualizar precio | [README §3] [Constitución §7] [F001 FR-019–022, FR-045, R-05] |
| Normalización | `Trim()` de .NET, comparación sin mayúsculas, internos conservados, sin otras equivalencias; vacío tras limpiar = ausente; la limpieza precede a longitud y unicidad | [Constitución §7] [F001 FR-044, R-07] |
| Ausente frente a inválido | Fila vacía omitida sin incidencia; falta Periodo o Código → omitida con motivo; falta Nombre → "Producto " + código; falta Precio → 0; Precio no numérico, Periodo fuera de AAAA-MM, precio no representable en DECIMAL(18,2) o texto que excede la longitud (código 100, nombre 200) → omitida con motivo, sin redondear ni truncar | [README §3] [Constitución §7] [F001 FR-024–028, FR-047, R-08] |
| Transacciones | Una transacción por periodo con productos, incidencias de negocio y paso a Completado; fallo técnico antes de confirmar → revertir y luego registrar la incidencia técnica; periodos completados se conservan | [Constitución §7] [F001 FR-016–018, R-09] |
| Existente y ganador no persistido | Existente solo con producto confirmado; si el ganador se revierte o no se procesa, las filas afectadas → `NoProcesado` ("ganador no persistido") | [Constitución §7] [F001 FR-049, R-05] |
| Colisión concurrente | Violación del índice de `CodigoProducto` → reevaluar como Existente sin rechazar el grupo; persistente tras reintentos → fallo técnico del periodo | [F001 FR-046, R-06] |
| Fallo técnico terminal | Detener el archivo; carga Fallido; completados conservados; pendientes `NoProcesado` con reservas liberadas (lo hace `usp_CambiarEstadoCarga`) | [Constitución §7, §8] [F001 FR-048, R-03] |
| Estados | Pendiente → En proceso → Cargado → Finalizado; Pendiente/En proceso → Fallido; error transitorio no cambia estado; un reinicio no produce Fallido; Cargado → Finalizado fallido se recupera sin reprocesar | [Constitución §8] [F001 FR-029–031, FR-040–041] |
| Exclusividad y recuperación | Un único consumidor activo garantizado por un mecanismo (cerrojo de aplicación de SQL Server de ámbito de sesión en conexión dedicada); procesamiento secuencial; recuperación de cargas En proceso/Cargado antes de consumir; `IntentosProceso` acota la recuperación; sin barrido, Cronos ni Redis | [Constitución §9] [F001 FR-042–043, R-02, contrato "Cerrojo"] |
| Reentrega | Confirmar el mensaje solo tras persistir el resultado; mensaje de una carga no Pendiente → confirmar y descartar | [Constitución §9] [F001 R-02 punto 6, FR-022] |
| Notificación | Se publica al finalizar el procesamiento aunque haya omisiones de negocio; mensaje `{ "idCarga", "usuario", "fechaFin" }` en la cola `notificaciones` | [README §3 paso 3, §2 Cola 2] [Constitución §8, §9] |
| Archivo ilegible | Formato o contenido inválido = incidencia de entrada, sin reintentos técnicos; fallo de descarga o almacenamiento = técnico | [F001 R-03, Edge Cases] |
| Archivo inválido (precisión de esta feature) | Corrupto, sin hojas o sin los encabezados requeridos → incidencia `ArchivoInvalido` (tipo nuevo por script versionado mínimo), sin productos ni reintentos, recorrido normal En proceso → Cargado → Finalizado y notificación. Archivo inexistente en SeaweedFS = fallo técnico de recuperación del archivo (`ErrorTecnico`, Fallido) | [Decisión Q1, autorizada por el usuario 2026-11-03] [Constitución §7 v3.2.0] |
| Notificación no confirmada (precisión de esta feature) | Reintentos limitados con confirmación; agotados → Finalizado conservado + `ErrorTecnico` que describe la falta de confirmación; sin recuperación automática | [Decisión Q2, autorizada por el usuario 2026-11-03] [Constitución §8 v3.2.0] |
| Polly | Reintentos limitados y Circuit Breaker en cada operación protegida, incluida la transacción de un periodo (circuit breaker propio, decisión H3); parámetros fijados en esta feature (plan) sin duplicar efectos de escritura | [Constitución §9] [F001 FR-032] [Decisión H3] |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Procesar una carga Pendiente de principio a fin (Priority: P1)

Cuando llega el mensaje de una carga Pendiente, el sistema la pasa a En proceso, descarga el Excel, agrupa las filas por periodo, decide la admisión de cada periodo, decide los productos ganadores sobre todo el archivo, persiste cada periodo admitido en su transacción con sus incidencias, deja la carga en Cargado y luego en Finalizado (con fecha de fin) y publica la notificación. [README §2 Microservicio 2, §3 paso 3] [Constitución §7, §8] [F001 R-05]

**Why this priority**: Es el procesamiento real del Excel y la persistencia correcta, núcleo de la evaluación (README §6 "Funcionalidad").

**Independent Test**: Subir el Excel de ejemplo con Control sobre una base sin productos y comprobar historial, periodos, productos, incidencias, `FechaFin` y el mensaje en `notificaciones` (sin consumidor de notificaciones).

**Acceptance Scenarios**:

1. **Given** una carga Pendiente con el Excel de ejemplo y ningún periodo previo, **When** se consume su mensaje, **Then** su historial es Pendiente, En proceso, Cargado, Finalizado; los tres periodos quedan Completado; existe un producto por código distinto con su periodo de origen; `FechaFin` tiene valor; y hay un mensaje en `notificaciones` con `idCarga`, `usuario` y `fechaFin`.
2. **Given** un archivo con varios periodos, uno con un periodo ya Completado por otra carga y otro Admitido por otra carga, **When** se procesa, **Then** el primero queda Rechazado y el segundo Bloqueado (con su incidencia de grupo) y el resto se procesa normalmente.
3. **Given** un código que ya existe por otra carga, **When** aparece en un periodo admitido, **Then** la fila se omite como Existente y el precio del producto existente no cambia.
4. **Given** el mismo código en la fila 3 (periodo B) y en la fila 5 (periodo A), **When** se procesa, **Then** gana la fila 3 y la fila 5 queda Existente, sea cual sea el orden de procesamiento de los periodos.
5. **Given** filas vacías, sin Periodo, sin Código, sin Nombre, sin Precio, con Precio no numérico, con Periodo "2025-13", con precio 1.005, con código de 101 caracteres y con código " p0117 " frente a "P0117", **When** se procesan, **Then** cada una obtiene el resultado de la tabla de reglas (omitida sin incidencia, FilaOmitida con motivo, valor por defecto o Existente), sin redondeos ni truncamientos.
6. **Given** un archivo en el que todos los periodos se rechazan o bloquean, **When** se procesa, **Then** la carga llega a Finalizado sin productos, con sus incidencias de grupo, y se publica la notificación; no queda Fallido.
7. **Given** cualquier carga procesada, **When** se revisan las incidencias, **Then** se distinguen las de grupo, fila y técnicas y todas están vinculadas a la carga (y al periodo y la fila cuando se conocen).

---

### User Story 2 - Garantizar un único consumidor y descartar mensajes que no corresponden (Priority: P1)

Solo una instancia procesa o recupera a la vez; los mensajes de cargas que ya no están Pendiente (reentregas, duplicados de Control, mensajes tardíos de cargas Fallido) se confirman y descartan sin cambiar nada. [Constitución §9] [F001 FR-042, R-02] [F005 contrato de mensajería]

**Why this priority**: Sin exclusividad y descarte, las reentregas y los duplicados tolerados por la 005 duplicarían efectos.

**Independent Test**: Arrancar una segunda instancia del servicio contra la misma base y la misma cola; reinyectar mensajes de cargas ya procesadas, duplicados y de cargas Fallido.

**Acceptance Scenarios**:

1. **Given** una instancia con el cerrojo, **When** arranca otra, **Then** la segunda no consume ni recupera mientras la primera mantenga el cerrojo y lo deja constancia en el log; en ningún momento procesan dos a la vez (si la segunda vuelve a intentar obtener el cerrojo, y cuándo, lo fija el plan).
2. **Given** una carga ya Finalizado, **When** llega otra vez su mensaje, **Then** se confirma y descarta: sin productos, incidencias, historial ni notificación nuevos.
3. **Given** dos mensajes de la misma carga Pendiente (duplicado de Control), **When** se consumen, **Then** se procesa una sola vez y el segundo se descarta.
4. **Given** un mensaje cuyo `idCarga` no existe o cuyo cuerpo no cumple el contrato, **When** se consume, **Then** se confirma y descarta con un log de error, sin cambiar ninguna carga.
5. **Given** que el consumidor pierde la conexión que sostiene el cerrojo, **When** va a procesar el siguiente grupo, **Then** se detiene sin confirmar ese grupo [F001 contrato "Cerrojo"].

---

### User Story 3 - Recuperar ejecuciones interrumpidas sin duplicar ni reprocesar (Priority: P1)

Al arrancar, con el cerrojo y antes de consumir, el sistema reanuda las cargas En proceso y completa las Cargado; omite los periodos Completado, retoma los Admitido propios y cuenta el intento; al superar el límite de intentos la carga pasa a Fallido. [Constitución §8, §9] [F001 FR-041, FR-043, R-02, R-03]

**Why this priority**: La constitución exige que una caída o reinicio no produzca Fallido ni duplicados.

**Independent Test**: Detener el servicio en mitad de una carga (tras confirmar uno o más periodos) y en el paso Cargado → Finalizado; volver a arrancarlo y revisar el resultado.

**Acceptance Scenarios**:

1. **Given** una carga En proceso con P1 Completado y P2 sin confirmar al caer el servicio, **When** arranca de nuevo, **Then** conserva P1, procesa P2 y P3 una sola vez, llega a Finalizado y publica una notificación, sin productos ni incidencias duplicados.
2. **Given** una carga Cargado cuyo paso a Finalizado falló, **When** se recupera, **Then** pasa a Finalizado sin reprocesar productos y publica la notificación.
3. **Given** una recuperación interrumpida por otra caída, **When** se repite, **Then** el resultado final es el mismo; cada ejecución iniciada suma un intento.
4. **Given** una carga que supera el límite de intentos, **When** se recupera, **Then** queda Fallido con su incidencia técnica, sus periodos Admitido pasan a `NoProcesado` con reservas liberadas y los Completado se conservan.
5. **Given** cargas Pendiente sin mensaje consumido, **When** el servicio arranca, **Then** la recuperación no las toca y siguen Pendiente.
6. **Given** el mensaje reentregado de una carga ya recuperada, **When** llega tras la recuperación, **Then** se descarta (US2-2).

---

### User Story 4 - Tratar fallos técnicos con el resultado aprobado (Priority: P2)

Errores transitorios se reintentan sin cambiar el estado; un fallo técnico terminal detiene el archivo y deja la carga Fallido con los completados conservados y los pendientes `NoProcesado`; una colisión de código se reevalúa como Existente. [Constitución §7, §8] [F001 FR-017, FR-046, FR-048, R-03, R-06]

**Why this priority**: Afecta la trazabilidad ante fallos, no el flujo principal.

**Independent Test**: Detener SeaweedFS, SQL Server o RabbitMQ en distintos momentos y forzar una colisión de código; revisar estados, periodos e incidencias.

**Acceptance Scenarios**:

1. **Given** SeaweedFS no disponible, **When** se agotan los reintentos de descarga, **Then** la carga queda Fallido con incidencia `ErrorTecnico`, sin periodos reservados, y el mensaje se confirma.
2. **Given** un fallo técnico en P2 tras confirmar P1, **When** se agotan los reintentos, **Then** P1 sigue Completado, P2 queda Fallido sin productos ni incidencias de negocio, existe la incidencia técnica, P3 queda `NoProcesado` con la incidencia "No procesado por interrupción de la carga" y la carga queda Fallido; no se publica notificación.
3. **Given** un fallo transitorio que se resuelve dentro de los reintentos, **When** se procesa, **Then** el resultado es el mismo que sin fallo y el estado no cambió por el error.
4. **Given** un producto insertado por otro proceso con el mismo código entre la decisión y la confirmación de un grupo, **When** se confirma, **Then** la fila queda Existente tras reevaluar y el grupo se completa; una colisión persistente se trata como fallo técnico del periodo.
5. **Given** una caída del servicio (no un fallo técnico de una operación), **When** reinicia, **Then** la carga no pasa a Fallido por ese motivo (US3).
6. **Given** una carga cuyo archivo SeaweedFS confirma que no existe, **When** se intenta descargar, **Then** no se reintenta: la carga pasa de En proceso a Fallido con una incidencia `ErrorTecnico` de recuperación del archivo, sin periodos reservados ni notificación. [Decisión Q1]
7. **Given** un archivo corrupto, sin hojas o sin alguno de los cuatro encabezados, **When** se procesa, **Then** no se reintenta, se registra una incidencia `ArchivoInvalido` sin productos ni periodos, la carga recorre En proceso → Cargado → Finalizado (con `FechaFin`) y se publica la notificación. [Decisión Q1]
8. **Given** la carga Finalizado y la publicación en `notificaciones` sin confirmación tras los reintentos, **When** se agotan, **Then** la carga sigue Finalizado, existe una incidencia `ErrorTecnico` que describe la falta de confirmación (sin afirmar que no se publicó), el mensaje de `carga_masiva` se confirma y, al reiniciar el servicio, la carga no se vuelve a notificar. [Decisión Q2]

---

### User Story 5 - Comprobar con el consumidor real la carrera documentada en la feature 005 (Priority: P2)

Control solo pasa a Fallido una carga que sigue Pendiente (V003); un mensaje tardío de una carga Fallido no la reactiva. Esta historia comprueba ambas garantías con el consumidor real. [F005 spec US5-4/US5-5, FR-023, contrato V003] [Decisión]

**Why this priority**: Cierra una verificación que la 005 no pudo ejecutar de punta a punta sin consumidor.

**Independent Test**: Provocar las secuencias descritas con el consumidor en marcha y revisar estado, historial e incidencias.

**Acceptance Scenarios**:

1. **Given** una carga que el consumidor ya pasó a En proceso, **When** se ejecuta la misma operación de fallo de Control (`usp_CambiarEstadoCarga` con Fallido y origen esperado Pendiente), **Then** devuelve "sin cambio": la carga sigue su recorrido, sin historial Fallido ni incidencia `ErrorTecnico`, y termina Finalizado.
2. **Given** una carga que Control dejó Fallido (publicación no confirmada), **When** llega después su mensaje al consumidor, **Then** se confirma y descarta: sigue Fallido, sin historial, periodos, productos, incidencias ni notificación nuevos.
3. **Given** la carrera simultánea (Control intenta Fallido mientras el consumidor intenta En proceso), **When** ambos actúan sobre la misma carga Pendiente, **Then** exactamente uno de los dos cambios se aplica y el resultado es coherente: Fallido sin procesamiento posterior, o En proceso sin Fallido de Control.

---

### Edge Cases

- Archivo inexistente en SeaweedFS (ausencia confirmada por el almacenamiento): fallo técnico de recuperación del archivo, **sin reintentos** (no es transitorio) → `ErrorTecnico` y Fallido (US4-6). SeaweedFS no disponible o error transitorio de acceso: técnico **con** reintentos; agotados → `ErrorTecnico` y Fallido (US4-1). [Decisión Q1]
- Archivo corrupto, sin hojas o sin alguno de los cuatro encabezados: archivo inválido → `ArchivoInvalido`, sin productos ni reintentos, Finalizado y notificación (US4-7). [Decisión Q1]
- Encabezados en otro orden o con espacios: los cuatro campos se identifican por su nombre en la primera fila de la primera hoja (Assumptions). Columnas adicionales se ignoran.
- Periodo guardado por Excel como fecha o como número: se toma su texto mostrado; si no cumple AAAA-MM, la fila se omite con motivo (no se convierte por suposición).
- Precio en celda numérica con ruido binario (P0047 guardado como `75.51000000000001`, mismo valor numérico que 75.51): se evalúa por su valor, con la representación decimal más corta que lo preserva (75.51), y luego se comprueba exacto en DECIMAL(18,2); 1.005 se omite. [Decisión H1] [Constitución §7 v3.2.1]
- Precio en celda de texto con coma ("1,5") o con separador de miles: no numérico, se omite con su incidencia. [Decisión H2] [Constitución §7 v3.2.1]
- Código con espacios internos ("ab c"): se conserva; con solo espacios: ausente.
- Duplicado cuya fila ganadora pertenece a un periodo que luego falla: `NoProcesado` "ganador no persistido", nunca Existente [F001 FR-049].
- Mensaje para una carga En proceso o Cargado después de la recuperación: se descarta (la recuperación ya la tomó).
- Fallo al publicar la notificación tras Finalizado: la carga conserva Finalizado, se registra `ErrorTecnico` describiendo la falta de confirmación y no hay recuperación automática (US4-8). Si el broker confirmó, aunque luego falle algo, no se registra nada. [Decisión Q2]
- Archivo sin filas de datos (solo encabezados): Finalizado sin productos ni periodos y notificación publicada (filas vacías no generan nada) [README §3].
- Longitud de textos de auditoría (`Incidencia.CodigoProducto` 100, `PeriodoTexto` 50, `Motivo` 500): truncado explícito indicado en el motivo [F001 R-08].

## Requirements *(mandatory)*

### Functional Requirements

**Consumo y exclusividad**

- **FR-001**: ms-carga-masiva MUST consumir la cola `carga_masiva` mediante un `BackgroundService` dentro de `Acity.Api`, sin proyectos Worker, declarando la misma topología que publica Control (intercambio directo durable `carga_masiva`, cola durable `carga_masiva`, clave `carga_masiva`). [Constitución §3, §9] [F005 contrato de mensajería]
- **FR-002**: El consumo MUST ser de un mensaje a la vez (sin procesamiento concurrente) y con confirmación manual: un mensaje se confirma solo después de persistir el resultado de su carga o de decidir descartarlo. [Constitución §9] [F001 R-02 punto 6]
- **FR-003**: Antes de recuperar o consumir, la instancia MUST obtener un cerrojo exclusivo de aplicación de SQL Server con propietario de sesión, sin espera, sobre una conexión dedicada mantenida durante toda la vida del consumidor; sin el cerrojo MUST NOT consumir ni recuperar. Antes de cada grupo MUST comprobar que la conexión del cerrojo sigue viva; si no, MUST detenerse sin confirmar. [Constitución §9] [F001 FR-042, R-02, contrato "Cerrojo"]
- **FR-004**: Un mensaje cuya carga no esté Pendiente, cuyo `idCarga` no exista o cuyo cuerpo no cumpla el contrato MUST confirmarse y descartarse sin cambios, con un log (advertencia o error). [F001 FR-022, R-02] [F005 contrato de mensajería]
- **FR-005**: El paso Pendiente → En proceso MUST hacerse con `usp_CambiarEstadoCarga` con origen esperado Pendiente; si no se aplica (la carga cambió, p. ej. Control la dejó Fallido), el mensaje MUST descartarse. [F005 FR-032] [Constitución §8]

**Lectura del archivo**

- **FR-006**: El sistema MUST descargar el archivo desde SeaweedFS usando `rutaArchivo` (ruta del filer tras `seaweed://`) y leerlo con ClosedXML, sin guardar su contenido en la base ni en los logs. [README §2 Microservicio 2] [Constitución §9, §10, §12] [F001 FR-007]
- **FR-007**: Los cuatro campos MUST identificarse por los encabezados `Periodo`, `CodigoProducto`, `NombreProducto` y `Precio` de la primera fila de la primera hoja; el orden de las filas de datos MUST conservarse y el número de fila del Excel MUST usarse en las incidencias. [Constitución §7] [F001 FR-045]
- **FR-008**: Un archivo corrupto (no legible como Excel), sin hojas o sin alguno de los cuatro encabezados MUST tratarse como archivo inválido: registrar una incidencia `ArchivoInvalido` vinculada a la carga (sin periodo ni fila), sin reservar periodos, sin productos y sin reintentos técnicos; la carga MUST seguir las transiciones aprobadas En proceso → Cargado → Finalizado (sin saltos desde Pendiente ni En proceso) y MUST publicarse la notificación. Esta finalización sin productos es una precisión autorizada del caso de archivo inválido. [Decisión Q1, autorizada por el usuario] [F001 R-03] [Constitución §7, §8]
- **FR-008a**: Un script versionado nuevo y mínimo en `database/scripts/` MUST ampliar el `CHECK` de `Incidencia.Tipo` con el valor `ArchivoInvalido`, sin otros cambios de esquema; V001 a V003 no se modifican. [Decisión Q1, autorizada por el usuario] [Constitución §6]
- **FR-008b**: Si SeaweedFS confirma que el archivo no existe, MUST tratarse como fallo técnico de recuperación del archivo **sin reintentos**: incidencia `ErrorTecnico` y Fallido desde En proceso. Los errores transitorios de acceso (no disponible, tiempo de espera, errores del servidor) MUST reintentarse de forma limitada y, agotados, terminar igual; MUST NOT reintentarse una ausencia confirmada ni un archivo inválido. [Decisión Q1] [Constitución §8, §9]

**Reglas de procesamiento** (aplican, sin cambios, las fuentes de la tabla "Reglas aprobadas")

- **FR-009**: El sistema MUST normalizar y validar cada fila según [Constitución §7] y [F001 FR-024–028, FR-044, FR-047, R-07, R-08], registrando `FilaOmitida` con su motivo para las omitidas (salvo filas completamente vacías, que se omiten sin incidencia).
- **FR-009a**: El precio MUST leerse así [Decisión H1/H2] [Constitución §7 v3.2.1]: una celda numérica por su valor numérico (no por su formato visual), convertido a decimal con la representación decimal más corta que preserva ese valor y comprobado después exacto en DECIMAL(18,2), sin redondear ni truncar; una celda de texto solo es numérica con punto como separador decimal y sin separadores de miles. MUST NOT añadirse otras restricciones ni aplicarse redondeos generales.
- **FR-010**: El sistema MUST reservar cada periodo válido con `usp_ReservarPeriodo` mientras la carga está En proceso, una vez por periodo y fuera de transacciones de la aplicación; los resultados Rechazado y Bloqueado y sus incidencias de grupo los registra el procedimiento. [F001 FR-009–015, contrato `usp_ReservarPeriodo`]
- **FR-011**: El sistema MUST decidir los productos ganadores sobre todo el archivo antes de persistir, según [F001 R-05]: entre filas válidas de periodos Admitido gana la de menor número de fila por código; los códigos confirmados por **otras** cargas son Existente; los de la propia carga ya confirmados (en recuperación) se tratan igual que en la primera ejecución.
- **FR-012**: Cada periodo Admitido MUST persistirse en una transacción con sus productos ganadores, sus incidencias de negocio (incluidas las `Existente` de los duplicados que dependen de sus ganadores) y su paso a Completado; los procedimientos MUST NOT invocarse dentro de esa transacción. [F001 FR-016, R-05, R-09, contrato "Regla común"]
- **FR-013**: Una violación del índice único de `CodigoProducto` MUST revertir el grupo, releer los códigos existentes, recalcular los Existente y reintentar, sin rechazar el grupo; otras violaciones son errores técnicos. Persistente tras los reintentos → fallo técnico del periodo. [F001 FR-046, R-06]
- **FR-014**: Si el ganador de un duplicado no llega a persistirse, la fila duplicada MUST registrarse como `NoProcesado` con motivo "ganador no persistido" y nunca como Existente. [F001 FR-049, R-05]

**Estados, fallos y recuperación**

- **FR-015**: Resueltos todos los grupos, la carga MUST pasar a Cargado y luego a Finalizado con `usp_CambiarEstadoCarga` (que fija `FechaFin`); una omisión o rechazo de negocio MUST NOT producir Fallido. [README §2] [Constitución §8] [F001 FR-029–031, FR-036]
- **FR-016**: Las operaciones contra SeaweedFS, SQL Server y RabbitMQ MUST protegerse con reintentos limitados y Circuit Breaker de Polly, con parámetros del plan por operación según su riesgo de duplicación; MUST NOT reintentarse un archivo ilegible ni una colisión fuera de FR-013. [Constitución §9] [F001 FR-032]
- **FR-017**: Ante un fallo técnico terminal, el sistema MUST revertir el grupo en curso, registrar la incidencia `ErrorTecnico` y pasar la carga a Fallido con `usp_CambiarEstadoCarga` indicando el periodo fallido (el procedimiento deja `NoProcesado` los demás Admitido y libera reservas); el mensaje se confirma y no se publica notificación. [Constitución §7, §8] [F001 FR-017, FR-048, R-03]
- **FR-018**: Al arrancar, con el cerrojo y antes de consumir, el sistema MUST recuperar las cargas En proceso (reanudar) y Cargado (completar Finalizado y notificar) sin tocar las Pendiente; cada ejecución iniciada o reanudada MUST incrementar `IntentosProceso` con una sola sentencia; al superar el límite (valor del plan) la carga MUST pasar a Fallido con su incidencia técnica. [Constitución §8, §9] [F001 FR-041, FR-043, R-02 puntos 2–5]
- **FR-019**: Un error transitorio que se resuelve dentro de los reintentos y una caída o reinicio del proceso MUST NOT cambiar el estado ni duplicar productos, incidencias, historial o notificaciones. [Constitución §8] [F001 FR-022, FR-031, FR-040]

**Notificación**

- **FR-020**: Tras Finalizado, el sistema MUST publicar en la cola `notificaciones` el mensaje `{ "idCarga", "usuario", "fechaFin" }` del README, con `usuario` = correo del responsable y `fechaFin` = `CargaArchivo.FechaFin`, con confirmación del intermediario y mensaje persistente; la topología (intercambio directo y cola durables) la fija el plan como en la 005. [README §2 Cola 2, §3 paso 3] [Constitución §8, §9]
- **FR-021**: Para obtener el correo en recuperación (sin mensaje de entrada), ms-carga-masiva MUST incorporar la entidad `Usuario` con solo `Id` y `Email` por ingeniería inversa, como en Control. [F004 procedimiento] [F005 D-03]
- **FR-022**: Si la publicación de la notificación no obtiene confirmación tras los reintentos limitados, la carga MUST conservar Finalizado (nunca Fallido) y MUST registrarse una incidencia `ErrorTecnico` vinculada a la carga cuyo motivo describa la **falta de confirmación** del intermediario, sin afirmar que el mensaje no se publicó. MUST NOT existir recuperación automática, columnas nuevas ni uso de incidencias como mecanismo de control, y MUST NOT republicarse las cargas Finalizado al arrancar. No se garantiza la notificación posterior de esa carga (limitación aceptada). [Decisión Q2, autorizada por el usuario] [Constitución §8 v3.2.0]

**Carrera documentada en la 005**

- **FR-023**: El consumidor MUST comportarse según US5: una carga que tomó (En proceso o posterior) no puede pasar a Fallido por la operación de Control, y un mensaje de una carga Fallido se descarta sin cambios. [F005 FR-023, FR-032]

**Límites de alcance**

- **FR-024**: Esta feature MUST NOT incluir consultas de seguimiento, endpoints de negocio, envío de correos, el consumidor de `notificaciones`, Autenticación, Gateway, React, colas adicionales, frameworks de mensajería, servicios de barrido, Outbox ni pruebas automatizadas. [Decisión] [Constitución §9, §11, §12]
- **FR-025**: RabbitMQ y SeaweedFS MUST configurarse para ms-carga-masiva por variables de entorno en Compose, reutilizando los servicios existentes; ningún otro servicio cambia. [Constitución §11] [F005 FR-031]

### Key Entities *(include if feature involves data)*

Sin tablas ni columnas nuevas; solo se amplía el `CHECK` de `Incidencia.Tipo` con `ArchivoInvalido` (FR-008a) [F001] [F004] [Decisión Q1].

- **Carga del archivo**: estado, `IntentosProceso`, `FechaFin`; pasa por En proceso, Cargado, Finalizado o Fallido.
- **Periodo de la carga**: resultado por periodo (Admitido, Completado, Rechazado, Bloqueado, Fallido, NoProcesado) y reserva.
- **Producto**: código único global, nombre, precio y periodo de origen.
- **Incidencia**: grupo, fila, técnica o `ArchivoInvalido` (archivo), con motivo, carga, periodo y fila cuando se conocen.
- **Historial de estados**: lo escribe `usp_CambiarEstadoCarga`.
- **Usuario** (nueva en ms-carga-masiva, tabla existente): `Id` y `Email`.
- **Mensaje de notificación**: `idCarga`, `usuario`, `fechaFin`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Con el Excel de ejemplo sobre una base sin productos, el 100 % de las filas termina en exactamente un resultado (producto, Existente, FilaOmitida, NoProcesado u omisión de fila vacía), los tres periodos quedan Completado, el historial es Pendiente → En proceso → Cargado → Finalizado y se publica exactamente una notificación.
- **SC-002**: En los escenarios de US1-2 a US1-6, el resultado por grupo y por fila coincide con las reglas aprobadas en el 100 % de los casos.
- **SC-003**: Tras reentregas, duplicados de Control, mensajes tardíos y reinicios, existe como máximo un producto por `CodigoProducto`, un resultado por (carga, periodo), ninguna incidencia ni entrada de historial duplicada y como máximo una notificación por carga Finalizado.
- **SC-004**: Con dos instancias en marcha, en ningún momento procesan o recuperan dos a la vez.
- **SC-005**: Tras una caída en mitad de una carga, el 100 % de los periodos Completado se conserva y la carga termina Finalizado sin pasar por Fallido (salvo límite de intentos).
- **SC-006**: Tras un fallo técnico terminal simulado, los completados se conservan, los pendientes quedan `NoProcesado`, no quedan reservas de la carga Fallido y no se publica notificación.
- **SC-007**: Las tres secuencias de US5 producen el resultado esperado.
- **SC-009**: Un archivo inválido deja 0 productos y 0 periodos, una incidencia `ArchivoInvalido`, historial Pendiente → En proceso → Cargado → Finalizado y una notificación; un archivo inexistente deja Fallido con `ErrorTecnico` sin reintentos; una notificación no confirmada deja Finalizado con `ErrorTecnico` y 0 republicaciones tras reiniciar.
- **SC-008**: 0 coincidencias de contraseñas, cadenas de conexión o contenido del Excel en los logs de ms-carga-masiva.

## Verificaciones de la feature 005

| Verificación | Estado | Dónde |
|---|---|---|
| `usp_CambiarEstadoCarga` con `@EstadoEsperado='Pendiente'` no cambia una carga En proceso | **Realizada** en la 005 a nivel de procedimiento (base temporal) | `specs/005-…/quickstart.md`, escenario 2 |
| Control deja Fallido con historial e incidencia si RabbitMQ no está disponible | **Realizada** en la 005 | escenario 16 |
| Control no sobrescribe una carga ya tomada (`usp_CambiarEstadoCarga` con origen esperado Pendiente sobre una carga En proceso real → `Cambiado = 0`, la carga termina Finalizado sin Fallido) | **Realizada en la 006** (2026-11-03) | `specs/006-…/quickstart.md`, escenario 21 |
| El 202 de Control ante `Cambiado = 0` tras una publicación sin confirmación | **Pendiente** de punta a punta (requiere que el broker reciba sin confirmar); comprobado solo el procedimiento y el código | — |
| Un mensaje tardío no reactiva una carga Fallido | **Realizada en la 006** | escenario 22 |
| Carrera simultánea Control/consumidor sobre una carga Pendiente | **Realizada en la 006** (80 cargas; mezcla real 4/16 en la cuarta ronda; nunca ambos) | escenario 23 |
| `nack`/`return` del intermediario en la publicación de Control | **Pendiente**, fuera del alcance de esta feature | — |

Ningún resultado de esta tabla se declara antes de ejecutarlo.

## Assumptions

- Se procesa solo la primera hoja; la primera fila contiene los encabezados (como en el ejemplo); se identifican por nombre exacto tras `Trim()`, sin distinguir mayúsculas; columnas adicionales se ignoran. Es una lectura del formato aprobado de cuatro campos, no una validación nueva. [Constitución §7] [Observado]
- El texto de un Periodo es su valor mostrado; no se reinterpretan fechas de Excel como periodos.
- El límite de `IntentosProceso`, los parámetros de Polly, la topología y el formato de `fechaFin` (UTC, ISO 8601) los fija el plan dentro de lo aprobado. [F001 R-02 punto 5] [Constitución §9]
- La verificación es manual sobre una base temporal y con Control real para subir archivos; sin pruebas automatizadas. [Constitución §11]
- Validado solo en el equipo de desarrollo (SQL Server amd64 emulado); el equipo de evaluación queda sin validar.

## Asuntos pendientes (no son requisitos aprobados)

Ninguno que requiera decisión del usuario. Resueltos por decisión autorizada del usuario (2026-11-03): **Q1** archivo inválido (FR-008, FR-008a, FR-008b) y **Q2** notificación no confirmada (FR-022).

**Hallazgos de la revisión de cierre, resueltos por el usuario (2026-11-03)**: H1 y H2 → FR-009a y constitución §7 v3.2.1; H3 → se conserva FR-016 y la transacción de un periodo incorpora un circuit breaker propio (diseño en `research.md` D-12; **implementación y verificación pendientes**: tareas T049 y T050).

**Limitación aceptada** (Q2): si la notificación no se confirma, la carga queda Finalizado con su incidencia y no se notifica después de forma automática; una notificación sin confirmación pudo haber llegado.

Para el plan, sin aprobación por detalle local: límite de intentos, parámetros de Polly, topología de `notificaciones`, formato de `fechaFin`, códigos de log, número de fila reportado (el de Excel).
