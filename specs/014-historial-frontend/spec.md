# Feature Specification: Pantalla de Historial de cargas en el cliente web

**Feature Branch**: `014-historial-frontend` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v1)

**Input**: Pantalla funcional de Historial en `cargamasiva_front` (README, pantalla 2: tabla).
- Consume `GET /cargas` (F010) por el Gateway con el cliente HTTP común.
- Tabla con identificador, archivo, usuario, recepción, estado y fin; cada fila enlaza al detalle.
- Lista vacía con un mensaje y errores de `Response<T>`.
- Seguimiento de los estados en tiempo real mediante *polling* a intervalo fijo, solo mientras la página está abierta y visible.
- Alcance mínimo.

**Procedencia**: `[README §n]`; `[Constitución §n]` = v3.8.1; `[F010]` = contrato de consultas (`specs/010-consultas-cargas/contracts/consultas-api.md`); `[F011]` = fundación del frontend; `[Decisión]` = decisión del usuario (incluye el prompt).

## Contexto

- **README**: «Consultar el historial de cargas», «Ver el estado de cada procesamiento» y «Muestra el historial de cargas. Permite ver estados en tiempo real (mediante pooling)»; pantalla «2. Historial de cargas (tabla)» [README §2, §3 paso 5, Frontend].
- **Contrato** [F010]:
  - **Respuesta**: `GET /cargas` → 200 con una lista de `{ idCarga, nombreArchivo, usuario, fechaRecepcion, estado, fechaFin }`, ordenada de la más reciente a la más antigua; `[]` si no hay cargas.
  - **Visibilidad**: el Administrador ve todas las cargas; el Operador, solo las suyas.
  - **Límite**: las consultas **no** consumen el límite de subidas, así que el *polling* no produce 429.
- **Estados posibles**: Pendiente, En proceso, Cargado, Finalizado, Notificado y Fallido [Constitución §8].
- **Disponible** [F011]: el cliente HTTP común con renovación, la ruta protegida `/historial` (provisional) y `/historial/:idCarga` (detalle provisional).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver el historial y seguir los estados (Priority: P1)

El usuario abre Historial, ve sus cargas (o todas, si es Administrador) en una tabla, sigue cómo cambian de estado sin recargar y abre el detalle de una carga. [README §2, §3 paso 5]

**Why this priority**: Es el seguimiento que pide el README.

**Independent Test**: Con sesión, abrir `/historial` y subir un Excel en otra pestaña o desde Postman; la fila nueva aparece y su estado avanza hasta Notificado sin recargar.

**Acceptance Scenarios**:

1. **Given** cargas visibles para el usuario, **When** abre `/historial`, **Then** ve una tabla con identificador, nombre del archivo, usuario, fecha de recepción, estado y fecha de fin (vacía si no terminó), en el orden del servidor.
2. **Given** una fila, **When** se pulsa, **Then** se navega a `/historial/{idCarga}`.
3. **Given** que no hay cargas, **When** se abre la página, **Then** se muestra «No hay cargas registradas» en lugar de la tabla.
4. **Given** la página abierta, **When** pasa el intervalo de actualización, **Then** se vuelve a consultar el historial y la tabla refleja los estados y las cargas nuevas sin recargar la página.
5. **Given** la pestaña oculta o la página abandonada, **When** pasa el intervalo, **Then** no se hacen consultas. Al volver a la pestaña, se consulta de inmediato.
6. **Given** un error al consultar (por ejemplo, el Gateway caído), **When** ocurre, **Then** se muestra el mensaje del error y se conservan los últimos datos mostrados; la siguiente consulta correcta borra el error.
7. **Given** la primera carga de la página, **When** aún no hay respuesta, **Then** se muestra un indicador de carga.

### Edge Cases

- **Token vencido durante el *polling***: la renovación de la Feature 011 actúa de forma transparente. Si falla, se vuelve a Login.
- **Consultas solapadas**: si una consulta tarda más que el intervalo, no se lanza otra hasta que termine.
- **Historial grande**: se muestra completo, sin paginación (excluida).
- **Fechas**: el servidor las envía en UTC. Se muestran en la hora local del navegador.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/historial` MUST consultar `GET /cargas` mediante el cliente HTTP común y mostrar una tabla con `idCarga`, `nombreArchivo`, `usuario`, `fechaRecepcion`, `estado` y `fechaFin`, en el orden recibido. [README §2] [F010]
- **FR-002**: Cada fila MUST enlazar a `/historial/{idCarga}`. [Decisión]
- **FR-003**: Una lista vacía MUST mostrar «No hay cargas registradas». [F010]
- **FR-004**: La consulta MUST repetirse a un intervalo fijo mientras la página esté abierta y la pestaña visible, sin consultas solapadas. Se detiene al salir de la página y se reanuda, con una consulta inmediata, al volver a hacerse visible. [README §3 paso 5] [Decisión]
- **FR-005**: Los errores MUST mostrarse con el `message` del `ApiError`, conservando los últimos datos. [Constitución §5]
- **FR-006**: MUST NOT añadirse paginación, filtros, ordenación configurable, WebSockets ni dependencias. [Decisión] [Constitución §12]

### Key Entities

- **Carga en el historial** (contrato TypeScript de F010): `{ idCarga: number; nombreArchivo: string; usuario: string; fechaRecepcion: string; estado: string; fechaFin: string | null }`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una carga subida mientras la página está abierta aparece y alcanza Notificado en la tabla sin recargar, dentro de un intervalo de actualización tras cada cambio en el servidor.
- **SC-002**: Con la pestaña oculta, se hacen 0 consultas; al volver, 1 consulta inmediata.
- **SC-003**: Un Operador ve solo sus cargas y el Administrador todas (lo garantiza el servidor; la pantalla muestra exactamente lo recibido).
- **SC-004**: El *polling* sostenido durante 5 minutos no produce ningún 429 ni errores en la consola.

## Assumptions

- **Intervalo**: 5 segundos, fijo y no configurable. Es suficiente para ver el avance del procesamiento sin sobrecargar.
- **Formato de fechas**: hora local con el formato de `es` (día, mes, año, hora y minutos).
- **Estado**: se muestra como texto, con un color de apoyo por estado (por ejemplo, verde para Notificado y rojo para Fallido). Es una precisión de diseño.
- **Verificación**: manual en el navegador; sin pruebas automatizadas [Constitución §11].
