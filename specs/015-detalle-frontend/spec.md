# Feature Specification: Pantalla de Detalle de una carga en el cliente web

**Feature Branch**: `015-detalle-frontend` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v1)

**Input**: Pantalla funcional de Detalle en `cargamasiva_front` (README, pantalla 3), en `/historial/:idCarga`.
- Consume `GET /cargas/{id}` (estado, historial de estados, resultados por periodo e incidencias) y `GET /cargas/{id}/contenido` (filas del Excel original), de F010, por el Gateway.
- Muestra 404 «La carga no existe.» y los errores 422, 500 y 503 del contenido sin presentarlos como contenido vacío.
- Mantiene el *polling* del detalle mientras la carga no esté en un estado terminal y la pestaña esté visible.
- Carga el contenido bajo demanda.
- Alcance mínimo.

**Procedencia**: `[README §n]`; `[Constitución §n]` = v3.8.1; `[F010]` = contrato de consultas (`specs/010-consultas-cargas/contracts/consultas-api.md`); `[F011]` y `[F014]` = cliente HTTP y patrón de *polling*; `[Decisión]` = decisión del usuario (incluye el prompt).

## Contexto

- **README**: pantalla «3. Detalle del estado de una carga»; «Consultar el contenido del archivo excel subido» y «Ver el estado de cada procesamiento» en tiempo real [README §2, §3 paso 5, Frontend].
- **Contrato** [F010]:
  - **Detalle**: `GET /cargas/{id}` → `{ idCarga, nombreArchivo, usuario, fechaRecepcion, estado, fechaFin, historial[{estado, fecha}], periodos[{periodo, resultado, fecha}], incidencias[{tipo, numeroFila, codigoProducto, periodoTexto, motivo, fecha}] }`. Las secciones vacías llegan como `[]`.
  - **Contenido**: `GET /cargas/{id}/contenido` → `{ idCarga, nombreArchivo, hoja, encabezados[], filas[{numeroFila, celdas[]}] }`, con las celdas sin normalizar y `null` en las vacías.
  - **Errores**:
    - inexistente o ajena → 404 «La carga no existe.»;
    - `id ≤ 0` → 400;
    - en el contenido: 422 «El archivo no se puede leer como Excel.», 500 «No se pudo recuperar el archivo asociado a la carga.» y 503 «El archivo no está disponible temporalmente. Intente nuevamente más tarde.».
- **Estados terminales**: Notificado y Fallido. Finalizado puede quedarse así si la notificación falla [Constitución §8].
- **Disponible**: el cliente HTTP con renovación [F011], la ruta protegida `/historial/:idCarga` (página provisional) y el patrón de *polling* con visibilidad [F014].

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver el detalle y seguir el estado (Priority: P1)

El usuario abre el detalle de una carga y ve sus datos, el historial de estados, los resultados por periodo y las incidencias, que se actualizan mientras la carga avanza. [README §2 pantalla 3]

**Why this priority**: Permite entender qué ocurrió con la carga sin consultar los logs.

**Independent Test**: Abrir `/historial/{id}` de una carga procesada y de una que se está procesando, y la de otro usuario (siendo Operador).

**Acceptance Scenarios**:

1. **Given** una carga visible, **When** se abre su detalle, **Then** se muestran:
   - los datos generales: identificador, archivo, usuario, recepción, estado actual y fin;
   - el historial de estados en orden cronológico;
   - una tabla de periodos (periodo, resultado y fecha);
   - una tabla de incidencias (tipo, fila, código, periodo, motivo y fecha).
2. **Given** secciones sin datos (por ejemplo, una carga Pendiente), **When** se muestran, **Then** cada sección indica «Sin periodos» o «Sin incidencias», en lugar de tablas vacías.
3. **Given** una carga que no está en un estado terminal (Pendiente, En proceso, Cargado o Finalizado), **When** la página está abierta y visible, **Then** el detalle se vuelve a consultar cada 5 s y refleja el avance.
4. **Given** una carga en Notificado o Fallido, **When** se muestra, **Then** no se programan más consultas.
5. **Given** una carga inexistente, o ajena para un Operador, **When** se abre, **Then** se muestra «La carga no existe.» con un enlace para volver al Historial, sin más consultas.
6. **Given** un error temporal en una actualización, **When** ocurre, **Then** se muestra el mensaje y se conservan los datos anteriores.
7. **Given** la pestaña oculta, **When** pasa el intervalo, **Then** no se consulta. Al volver, se consulta de inmediato si la carga no es terminal.

---

### User Story 2 - Ver el contenido del Excel (Priority: P2)

El usuario pide ver el contenido del archivo y obtiene las filas tal como se subieron. [README §2] [F010 Q1 = B]

**Why this priority**: Lo pide el README, pero se carga bajo demanda para no descargar el Excel en cada actualización.

**Independent Test**: En el detalle, pulsar «Ver contenido» en una carga con su archivo, y forzar los errores 422, 500 y 503 en una base temporal (como en la Feature 010).

**Acceptance Scenarios**:

1. **Given** el detalle abierto, **When** se pulsa «Ver contenido», **Then** se muestran la hoja, los encabezados y las filas con su número de fila. Las celdas vacías se ven en blanco y los valores, tal cual.
2. **Given** la consulta del contenido en curso, **When** se espera la respuesta, **Then** el botón queda deshabilitado y se indica «Cargando contenido…».
3. **Given** un 422, un 500 o un 503, **When** se recibe, **Then** se muestra el mensaje del servidor como error, **nunca** como un contenido vacío. El botón permite reintentar.
4. **Given** un Excel sin filas de datos, **When** se muestra, **Then** aparecen los encabezados y se indica «El archivo no tiene filas de datos».
5. **Given** el contenido ya mostrado, **When** el detalle se actualiza por *polling*, **Then** el contenido no se vuelve a pedir.

### Edge Cases

- **Finalizado sin notificación** (falló el correo): la carga puede quedarse en Finalizado, así que el *polling* continúa mientras la página esté abierta y visible. Es una consulta ligera y no consume el límite de subidas [F010].
- **Identificador no numérico** en la URL (por ejemplo, `/historial/abc`): el Gateway devuelve 404 «No encontrado» y se muestra ese mensaje, igual que en el escenario 5.
- **Excel grande**: se muestran todas las filas, sin paginación (excluida).
- **Logs**: no se escriben tokens ni el contenido del Excel en la consola.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/historial/:idCarga` MUST consultar `GET /cargas/{id}` y mostrar los datos generales, el historial de estados, los periodos y las incidencias, con un mensaje para las secciones vacías. [README §2] [F010]
- **FR-002**: El detalle MUST actualizarse cada 5 s mientras el estado no sea Notificado ni Fallido y la pestaña esté visible, sin consultas solapadas, y detenerse al salir de la página. [README §3 paso 5] [F014] [Decisión]
- **FR-003**: Un 404 (o cualquier error de la primera consulta) MUST mostrarse con su mensaje y un enlace al Historial, sin seguir consultando tras un 404. [F010] [Constitución §5]
- **FR-004**: El contenido MUST pedirse solo al pulsar «Ver contenido» (`GET /cargas/{id}/contenido`) y mostrarse como una tabla de encabezados y filas sin transformar los valores, con el número de fila. [F010] [Decisión]
- **FR-005**: Los errores del contenido (422, 500 y 503) MUST mostrarse con el mensaje del servidor y nunca como un contenido vacío. MUST permitirse reintentar. [F010 FR-005d]
- **FR-006**: MUST NOT añadirse paginación, filtros, descarga del archivo ni dependencias. [Decisión] [Constitución §12]

### Key Entities

Contratos TypeScript de F010:
- `CargaDetalleDto`, con `EstadoHistorialDto`, `ResultadoPeriodoDto` e `IncidenciaDto`;
- `ContenidoArchivoDto`, con `FilaContenidoDto`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Para una carga procesada, el detalle muestra el mismo número de entradas de historial, periodos e incidencias que devuelve el servidor (100 %).
- **SC-002**: Al abrir el detalle de una carga recién subida, se ve su avance hasta un estado terminal sin recargar, y después se hacen 0 consultas nuevas.
- **SC-003**: Al abrir el detalle de una carga ajena, un Operador ve «La carga no existe.» en el 100 % de los casos.
- **SC-004**: El contenido del Excel de ejemplo muestra 200 filas y se pide 1 sola vez por cada pulsación del botón, nunca con el *polling*.
- **SC-005**: Los errores 422, 500 y 503 se muestran como errores en el 100 % de los casos.

## Assumptions

- **Intervalo**: 5 s, igual que en el Historial (F014).
- **Organización**: el *polling* del detalle vive en un hook de funcionalidad, igual que en la Feature 014, porque coordina estado, consultas y efectos. El contenido se gestiona en la página o en ese mismo hook, según decida el plan.
- **Formato**: fechas en hora local (`es`) y estados con los mismos colores que el Historial.
- **Verificación**: manual en el navegador; sin pruebas automatizadas [Constitución §11].
