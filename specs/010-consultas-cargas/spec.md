# Feature Specification: Consultas y seguimiento de cargas en ms-control

**Feature Branch**: `010-consultas-cargas` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v1: Q1 pendiente de decisión del usuario)

**Input**: Consultas de solo lectura en ms-control, expuestas por el Gateway:
- historial de cargas;
- detalle de una carga (estado, historial de estados, resultados por periodo e incidencias);
- contenido del archivo según el README.

El Administrador ve todas las cargas y el Operador solo las propias, también al acceder por identificador. Se reutilizan JWT y `Response<T>`. Esta etapa genera solo la especificación.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v3.5.0; `[F001]`, `[F005]`, `[F006]`, `[F009]` = features anteriores; `[Decisión]` = decisión explícita del usuario (incluye el prompt de esta feature); `[Observado]` = comprobado leyendo el repositorio. **Pendiente** = no decidido; no es requisito.

## Contexto

**Requisitos del README** (§2 Cliente web y §3 paso 5):
- Consultar el historial de cargas.
- Consultar el contenido del archivo Excel subido.
- Ver el estado de cada procesamiento (Pendiente, En proceso, Cargado, Finalizado, Notificado), en tiempo real mediante *polling*.
- Pantallas sugeridas: "Historial de cargas (tabla)" y "Detalle del estado de una carga".
- Sin React, el entregable son las colecciones de Postman.

**Reglas aprobadas que se aplican**:

| Tema | Regla | Fuente |
|---|---|---|
| Roles | El Administrador consulta todas las cargas y sus incidencias; el Operador, solo las propias. Un Operador no accede a la carga de otro usuario | [Constitución §6] [F001 FR-002, US7] |
| Estados | Pendiente, En proceso, Cargado, Finalizado, Notificado y Fallido (este último es una decisión aprobada, aunque el README no lo lista) | [README §2] [Constitución §8] |
| Trazabilidad | El historial de estados y las incidencias permiten reconstruir lo ocurrido sin usar los logs | [F001 US6, SC-002] |
| Seguridad | Control valida el JWT y el rol; el Gateway valida el JWT y reenvía | [Constitución §9] [F005] [F009] |
| Rate Limiting | Se aplica en Control **al endpoint de carga**; no se duplica | [Constitución §9] |
| Respuestas | `Response<T>` con `Code` igual al estado HTTP real; `data` null cuando no hay resultado | [Constitución §5] |
| Persistencia | Cada servicio mapea solo lo que necesita; los modelos se obtienen por ingeniería inversa | [Constitución §6] |

**Estado observado** [Observado]:
- **Endpoints de Control**: solo `POST /cargas` y `/health`. Las consultas se excluyeron de la F005 (FR-002) para esta feature.
- **Rate Limiting**: la política `carga` y `[Authorize(Roles=Administrador,Operador)]` se declaran **a nivel de controlador** en `CargasController`.
- **Entidades mapeadas en Control**: `CargaArchivo`, `CargaEstadoHistorial`, `Incidencia` y `Usuario` (`Id`, `Email`). No están mapeadas `CargaPeriodo` ni `Producto`.
- **Lo que conserva el modelo** (V001):
  - `CargaArchivo`: nombre, usuario, fecha de recepción, ruta en SeaweedFS, estado y fecha de fin.
  - `CargaPeriodo`: periodo, resultado y fecha del resultado.
  - `Producto`: código, nombre, precio y periodo de la carga que lo insertó.
  - `Incidencia`: tipo, número de fila, código, periodo en texto y motivo.
  - `CargaEstadoHistorial`: estado y fecha de cada cambio.
  - **No se guarda el contenido del Excel**, solo su ruta [F001 US1-2].
- **Gateway**: solo reenvía `POST /auth/login`, `POST /auth/refresh` y `POST /cargas`. Las rutas de consulta "se incorporarán cuando se implementen sus features" [F009 Q1].

**Referencias GitHub**: accesibles, consultadas el 2026-11-03 (árbol de archivos). `Clean_architecture_base_net7` separa las consultas (`Handlers/Queries/.../FindAll` y `FindId`) de los comandos. Solo orienta la estructura; no se copia nada.

### Contenido del archivo: diferencia que requiere decisión (Q1)

El README pide "consultar el contenido del archivo Excel subido", pero el modelo **no conserva las filas originales**:
- Los **productos insertados** solo cubren las filas aceptadas de los periodos admitidos.
- **No aparecen**:
  - las filas omitidas por validación, Existentes o no procesadas: tienen una incidencia, pero solo con fila, código y motivo, sin nombre ni precio;
  - las filas de periodos rechazados o bloqueados: tienen una sola incidencia por periodo, no por fila;
  - las filas vacías: se omiten sin incidencia [F006].
- Además, un producto Existente pertenece a otra carga, así que no figura entre los productos de esta.

Por eso, "productos insertados" **no equivale** a "todas las filas del archivo". Las únicas fuentes de las filas originales son el Excel guardado en SeaweedFS y su lectura. La descarga del Excel está excluida por el prompt, pero leerlo no lo está.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consultar el historial de cargas (Priority: P1)

Un usuario autenticado consulta la lista de cargas que le corresponden según su rol, para seguir su estado (incluido el *polling*). [README §2, §3 paso 5]

**Why this priority**: Es el seguimiento básico que pide el README.

**Independent Test**:
1. Con cargas de dos usuarios, consultar el historial como Administrador: deben aparecer todas.
2. Consultarlo como Operador: deben aparecer solo las suyas.
3. Consultarlo sin token: debe responder 401.

**Acceptance Scenarios**:

1. **Given** cargas del Operador A y del Operador B, **When** el Administrador consulta el historial, **Then** recibe 200 con todas las cargas. Cada una incluye identificador, nombre del archivo, usuario responsable (correo), fecha de recepción, estado actual y fecha de fin (null si no terminó). Van ordenadas de la más reciente a la más antigua.
2. **Given** las mismas cargas, **When** el Operador A consulta el historial, **Then** recibe solo las suyas.
3. **Given** un usuario sin cargas, **When** consulta el historial, **Then** recibe 200 con una lista vacía (no 404).
4. **Given** una solicitud sin token o con un token inválido, **When** se consulta, **Then** recibe 401 y no se devuelve ningún dato.
5. **Given** una carga en curso, **When** se consulta repetidamente (*polling*), **Then** cada consulta refleja el estado actual, y las consultas no consumen el límite de solicitudes del endpoint de carga.

---

### User Story 2 - Consultar el detalle de una carga (Priority: P1)

El usuario consulta una carga por su identificador para ver su estado, el historial de estados, el resultado de cada periodo y las incidencias. [README §2 "Detalle del estado de una carga"] [F001 US6]

**Why this priority**: Permite reconstruir lo ocurrido sin consultar los logs.

**Independent Test**: Procesar una carga con periodos admitidos y rechazados y con filas omitidas, y consultar su detalle como propietario, como Administrador y como otro Operador.

**Acceptance Scenarios**:

1. **Given** una carga propia procesada, **When** el Operador consulta su detalle, **Then** recibe 200 con:
   - los datos generales de la carga (los mismos que en el historial);
   - el historial de estados en orden cronológico (estado y fecha);
   - los resultados por periodo (periodo, resultado y fecha);
   - las incidencias en orden de registro (tipo, número de fila, código de producto, periodo en texto, motivo y fecha).
2. **Given** una carga Pendiente sin periodos ni incidencias, **When** se consulta, **Then** los periodos y las incidencias aparecen como listas vacías y el historial contiene solo Pendiente.
3. **Given** la carga de otro usuario, **When** un Operador consulta su detalle por identificador, **Then** recibe 404 con `data` null, la misma respuesta que para una carga inexistente, sin revelar que existe.
4. **Given** cualquier carga, **When** el Administrador consulta su detalle, **Then** recibe 200.
5. **Given** un identificador inexistente o no válido (no numérico o ≤ 0), **When** se consulta, **Then** recibe 404 o 400 respectivamente, con `Response<T>`.
6. **Given** una carga Fallido o Finalizado con incidencias de notificación, **When** se consulta, **Then** el estado y las incidencias se muestran tal cual, sin recalcular ni alterar nada.

---

### User Story 3 - Consultar el contenido del archivo (Priority: P2)

El usuario consulta el contenido de una carga autorizada. [README §2 "Consultar el contenido del archivo excel subido"]

**Why this priority**: Lo pide el README, pero su definición depende de Q1.

**Independent Test**: depende de Q1. Consultar el contenido de una carga propia con filas insertadas y omitidas, y el de la carga de otro usuario (404).

**Acceptance Scenarios**:

1. **Given** una carga autorizada, **When** se consulta su contenido, **Then** se recibe 200 con el contenido definido en Q1. *Depende de Q1.*
2. **Given** la carga de otro usuario o una inexistente, **When** un Operador consulta su contenido, **Then** recibe 404, igual que en US2-3.
3. **Given** una carga que aún no tiene contenido disponible según Q1 (por ejemplo, Pendiente), **When** se consulta, **Then** se recibe 200 con una lista vacía. *Depende de Q1.*

---

### Edge Cases

- **Historial grande**: se devuelve completo. La paginación está excluida por el prompt y se documenta como limitación.
- **Rol desconocido o ausente** en un token válido: 403 de Control, igual que en la carga (F005).
- **Carga borrada**: no existe borrado de cargas, así que el caso no aplica.
- **Consultas concurrentes con el procesamiento**: cada consulta devuelve el estado confirmado en ese momento. No se bloquea el procesamiento ni se cambia ningún estado.
- **Servicio de Control caído a través del Gateway**: 502 `Response<T>` del Gateway [F009].

## Requirements *(mandatory)*

### Functional Requirements

**Historial**
- **FR-001**: Control MUST exponer la consulta del historial de cargas. Cada carga devuelve identificador, nombre del archivo, correo del usuario responsable, fecha de recepción, estado y fecha de fin, ordenadas por fecha de recepción descendente. [README §2, §3 paso 5]
- **FR-002**: Sin cargas, la respuesta MUST ser 200 con una lista vacía. [Decisión: ausencia de datos ≠ error]

**Detalle**
- **FR-003**: Control MUST exponer la consulta del detalle de una carga por identificador. Incluye los datos de FR-001, el historial de estados (orden cronológico), los resultados por periodo y las incidencias (orden de registro). [README §2] [F001 US6]
- **FR-004**: Las secciones sin datos MUST devolverse como listas vacías, no como null. [Decisión]

**Contenido**
- **FR-005**: Control MUST exponer la consulta del contenido de una carga con la definición aprobada en Q1. [README §2] [NEEDS CLARIFICATION: Q1 — ¿qué es "contenido del archivo": los datos procesados en la base o las filas originales del Excel?]

**Autorización** [Constitución §6] [F001 FR-002]
- **FR-006**: Todas las consultas MUST exigir un JWT válido y el rol Administrador u Operador, validados en Control (y el JWT también en el Gateway). [Constitución §9]
- **FR-007**: El Administrador MUST ver todas las cargas. El Operador MUST ver solo aquellas cuyo responsable es su usuario (`sub`), también en las consultas por identificador.
- **FR-008**: Si un Operador consulta por identificador una carga ajena, la respuesta MUST ser idéntica a la de una carga inexistente (404, `data` null), para no revelar su existencia. [Decisión: mismo criterio que el 401 genérico de la F008]

**Restricciones**
- **FR-009**: Las consultas MUST ser de solo lectura: no cambian estados, no reprocesan, no publican mensajes, no escriben incidencias ni historial. [Decisión]
- **FR-010**: Las consultas MUST NOT estar sujetas al Rate Limiting del endpoint de carga. El *polling* del README no debe consumir el límite de subidas. [Constitución §9: Rate Limiting en el endpoint de carga]
- **FR-011**: Las respuestas MUST usar `Response<T>` con `Code` igual al estado HTTP. Las fechas se devuelven en UTC. [Constitución §5]
- **FR-012**: El Gateway MUST reenviar las rutas de consulta con JWT obligatorio y sin comprobar el rol, igual que `POST /cargas`. Las respuestas de Control se reenvían sin cambios. [F009 Q1–Q3]
- **FR-013**: Se reutilizan las entidades y los mapeos existentes de Control. Solo se añaden los mapeos que exigen los datos de esta feature (ver Key Entities). [Constitución §6]
- **FR-014**: Los logs MUST NOT registrar tokens ni el contenido devuelto. [Constitución §10]

**Fuera de alcance** [Decisión]:
- Paginación, filtros, ordenaciones configurables, exportaciones, descarga del Excel, caché y WebSockets.
- Endpoints de modificación o reintento.
- Cambios de esquema y de estados.

### Key Entities *(include if feature involves data)*

- **CargaArchivo** (mapeada en Control): datos generales y estado actual.
- **Usuario** (mapeada: `Id`, `Email`): correo del responsable y comprobación del propietario.
- **CargaEstadoHistorial** (mapeada): historial de estados.
- **Incidencia** (mapeada): incidencias de la carga.
- **CargaPeriodo** (no mapeada en Control; **mapeo adicional justificado**): periodo, resultado y fecha, necesarios para "resultados por periodo" (FR-003). Solo lectura.
- **Producto** (no mapeada en Control): se mapea **solo si** Q1 incluye los productos insertados. Código, nombre, precio y periodo, a través de `CargaPeriodo`. Solo lectura.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Con cargas de 2 Operadores, el Administrador ve el 100 % de las cargas y cada Operador solo las suyas, en el historial, en el detalle y en el contenido.
- **SC-002**: El 100 % de los accesos de un Operador a cargas ajenas por identificador (en el detalle y en el contenido) devuelven la misma respuesta que una carga inexistente (404).
- **SC-003**: Para una carga procesada, el detalle muestra todas las entradas del historial de estados, todos los periodos y todas las incidencias que existen en la base para esa carga (recuento igual).
- **SC-004**: Las consultas sin datos (historial vacío o carga Pendiente) devuelven 200 con listas vacías, nunca un error.
- **SC-005**: 20 consultas seguidas del mismo usuario no producen 429 ni impiden después una subida dentro de su límite.
- **SC-006**: Tras ejecutar todas las consultas, el estado, el historial y las incidencias de las cargas consultadas no cambian (recuento y valores iguales antes y después).
- **SC-007**: Las consultas funcionan igual a través del Gateway que directamente contra Control, y sin token el Gateway responde 401.

## Assumptions

- **Correo del responsable**: se muestra en el historial y el detalle también al Operador (es el suyo). Al Administrador le permite distinguir responsables.
- **Orden**: el historial va de la carga más reciente a la más antigua. Los estados, periodos e incidencias, en orden cronológico o de registro. No hay ordenación configurable.
- **Límite de tamaño**: no hay límite en el número de cargas, periodos o incidencias devueltos (sin paginación, por exclusión expresa).
- **Rutas HTTP**: los nombres concretos (por ejemplo, `GET /cargas`, `GET /cargas/{id}` y la de contenido) se fijan en el plan, sin prefijos nuevos en el Gateway.
- **Pruebas**: con Postman o curl. No hay tests automatizados [Constitución §11].

## Asuntos pendientes (no son requisitos aprobados)

| # | Pregunta | Opciones | Criterios afectados |
|---|---|---|---|
| Q1 | ¿Qué es "contenido del archivo"? | **A — Datos procesados (base de datos)**: productos insertados por la carga (código, nombre, precio y periodo) más las incidencias, que ya explican las filas no insertadas. No refleja todas las filas originales (ver Contexto). Requiere mapear `Producto` en Control; no lee el Excel. **B — Filas originales del Excel**: Control lee el archivo desde SeaweedFS y devuelve sus filas tal cual (`Periodo`, `CodigoProducto`, `NombreProducto`, `Precio`, número de fila), sin descargar el archivo. Refleja exactamente lo subido, pero añade ClosedXML y la lectura de SeaweedFS a Control (Control hoy solo escribe en SeaweedFS y la F005 FR-016 le prohibió leer el Excel en esa feature). **C — Ambas** | FR-005, US3, Key Entities, SC-001 |
