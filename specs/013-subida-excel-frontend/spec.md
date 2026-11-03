# Feature Specification: Pantalla de Subida de Excel en el cliente web

**Feature Branch**: `013-subida-excel-frontend` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v1)

**Input**: Pantalla funcional de Subida de Excel en `cargamasiva_front` (README, pantalla 1).
- Selección de un único `.xlsx`, enviado con `POST /cargas` (multipart, campo `archivo`) por el Gateway, con el cliente HTTP común.
- Muestra el 202 (`idCarga`, estado Pendiente) con un enlace al detalle, o los errores de `Response<T>`.
- Botón deshabilitado durante el envío.
- Alcance mínimo; esta etapa genera solo la especificación.

**Procedencia**: `[README §n]`; `[Constitución §n]` = v3.8.1; `[F005]` = contrato de recepción (`specs/005-recepcion-registro-cargas/contracts/http-recepcion-carga.md`); `[F009]` = Gateway; `[F011]` = fundación del frontend; `[Decisión]` = decisión del usuario (incluye el prompt).

## Contexto

- **README**: "El usuario sube un Excel desde el cliente web; el archivo se envía al API Gateway con el JWT" [README §2 Frontend, §3 paso 1].
- **Contrato** [F005]:
  - **Respuesta correcta**: 202 «Carga registrada» con `data { idCarga, estado: "Pendiente" }`.
  - **Errores**:
    - 400 «Error de validación» con `errors`: sin archivo, varios archivos, 0 bytes, extensión distinta de `.xlsx`, nombre de más de 260 caracteres, o tamaño mayor de 10 MiB y menor de 11 MiB;
    - 401, 403;
    - 413 (corte del servidor por encima de 11 MiB);
    - 429 «Demasiadas solicitudes» (10 por minuto por usuario);
    - 500.
- **Disponible** [F011]: el cliente HTTP común (Bearer, renovación ante 401, `ApiError`), la ruta protegida `/subir` (página provisional) y la ruta `/historial/:idCarga` (detalle provisional).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Subir un Excel (Priority: P1)

Un usuario autenticado elige su `.xlsx`, lo envía y obtiene el identificador de la carga registrada, con acceso a su detalle. [README §2, §3 paso 1]

**Why this priority**: Es el inicio del flujo funcional del reto.

**Independent Test**: Con sesión, subir el Excel de ejemplo desde `/subir`; repetir sin archivo, con un archivo no `.xlsx` y superando el límite de frecuencia.

**Acceptance Scenarios**:

1. **Given** una sesión activa y un `.xlsx` seleccionado, **When** se envía, **Then** se muestra «Carga registrada» con el `idCarga` y el estado Pendiente, y un enlace a `/historial/{idCarga}`.
2. **Given** el envío en curso, **When** se espera la respuesta, **Then** el botón queda deshabilitado, indica que se está subiendo y no se permite un segundo envío.
3. **Given** que no se eligió ningún archivo, **When** se intenta enviar, **Then** el formulario lo impide (archivo requerido).
4. **Given** un archivo rechazado por el servidor (400), **When** se envía, **Then** se muestran el mensaje y cada motivo de `errors` (por ejemplo, la extensión o el tamaño).
5. **Given** que se supera el límite de frecuencia (429), un archivo cortado por tamaño (413) o un fallo técnico (500), **When** se envía, **Then** se muestra el mensaje del servidor.
6. **Given** un token de acceso vencido, **When** se envía, **Then** la renovación de la Feature 011 actúa de forma transparente y la subida se completa. Si la renovación falla, se vuelve a Login [F011].
7. **Given** una subida correcta, **When** se elige otro archivo, **Then** se puede subir de nuevo; el resultado anterior se reemplaza al enviar.

### Edge Cases

- **Archivo de más de 11 MiB**: el servidor corta la conexión. El cliente puede recibir un error de conexión en lugar del 413, y muestra el mensaje genérico «No se pudo conectar con el servidor» (comportamiento observado en la Feature 009, no se cambia).
- **Selector**: filtra por `.xlsx` (`accept`), pero no impide elegir otros archivos; la validación la hace el servidor. No se inventan reglas de tamaño ni de nombre en el cliente [Constitución §4].
- **Resultado del procesamiento**: el 202 solo indica que la carga se registró. Su procesamiento posterior se consulta en el detalle (otra feature).
- **Logs**: no se escriben tokens ni el contenido del archivo en la consola.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/subir` MUST mostrar un selector de un único archivo (`accept=".xlsx"`, requerido) y un botón de envío. [README §2] [Decisión]
- **FR-002**: El envío MUST hacerse con `POST /cargas` (multipart, campo `archivo`) mediante el cliente HTTP común; sin URL del Gateway en el código. [F005] [F011] [Constitución §11]
- **FR-003**: Con 202, MUST mostrarse `message`, `idCarga` y `estado`, con un enlace a `/historial/{idCarga}`. [F005] [Decisión]
- **FR-004**: Los errores MUST mostrarse con `message` y la lista `errors` del `ApiError`, sin textos inventados. [Constitución §5]
- **FR-005**: Durante el envío, el botón MUST deshabilitarse y no se permite un envío doble. [Decisión]
- **FR-006**: MUST NOT añadirse validaciones de tamaño, nombre ni contenido en el cliente, progreso de subida, arrastrar y soltar, subidas múltiples ni dependencias. [Decisión] [Constitución §4, §12]

### Key Entities

- **Respuesta de carga registrada** (contrato TypeScript de F005): `{ idCarga: number; estado: string }`. Es el único modelo nuevo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Subir el Excel de ejemplo muestra el `idCarga` y el enlace al detalle en el 100 % de los intentos dentro del límite de frecuencia.
- **SC-002**: Los rechazos del servidor (400, 413 y 429) se muestran con su mensaje del servidor en el 100 % de los casos, sin salir de la pantalla.
- **SC-003**: Durante el envío no es posible lanzar una segunda subida: 1 envío equivale a 1 petición.
- **SC-004**: Ningún token aparece en la consola del navegador.

## Assumptions

- **Texto del botón**: «Subir» y, durante el envío, «Subiendo…». Es una precisión de diseño.
- **Después del éxito**: el selector se limpia para facilitar otra subida.
- **Verificación**: manual en el navegador (`http://localhost:3000`). Sin pruebas automatizadas [Constitución §11].
