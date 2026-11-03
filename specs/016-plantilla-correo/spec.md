# Feature Specification: Plantilla del correo de notificación

**Feature Branch**: `016-plantilla-correo` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v2: Q1–Q3 resueltas por el usuario, 2026-11-03)

**Input**: Plantilla del correo de notificación de ms-notificaciones, sencilla pero amigable y con colores. Informa el nombre del archivo, el resultado por periodo y las incidencias. Reabre la decisión Q1 de la Feature 007, que fijaba texto plano solo con `idCarga` y `fechaFin`. Esta etapa genera solo la especificación y la enmienda de la constitución autorizada.

**Procedencia**: `[README §n]`; `[Constitución §n]` = v3.9.0; `[F007]` = notificación por correo; `[F001]`/`[F006]` = periodos e incidencias; `[Decisión]` = decisión del usuario (incluye el prompt y la autorización de enmienda del 2026-11-03).

## Contexto

- **README**: «Envía un correo al usuario indicando que la carga finalizó» [README §2 y §3, paso 4]. No fija el contenido.
- **Antes** [F007 Q1]: asunto «Carga {idCarga} finalizada»; cuerpo en texto plano con `idCarga` y `fechaFin`; sin resúmenes ni plantillas.
- **Ahora** [Decisión; Constitución §8 v3.9.0]: una plantilla con colores que incluye el nombre del archivo, el resultado por periodo y las incidencias.
- **Se conserva de la Feature 007** sin cambios:
  - el mismo asunto y destinatario (el `usuario` del mensaje);
  - el descarte por estado (solo se envía si la carga está Finalizado);
  - los reintentos limitados y `ErrorNotificacion` al agotarlos;
  - Notificado solo después del envío;
  - MailKit y la configuración SMTP por variables de entorno.
- **Datos que se leen** (solo lectura, sin cambios de esquema):
  - `CargaArchivo.NombreArchivo`;
  - `CargaPeriodo` (periodo y resultado);
  - `Incidencia` (tipo, número de fila, código de producto, periodo y motivo).
  - Hoy ms-notificaciones solo mapea `CargaArchivo` (`Id`, `Estado`) e `Incidencia`.
- **Resultados posibles por periodo**: Admitido, Completado, Rechazado, Bloqueado, Fallido y NoProcesado [F001].

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Recibir un correo informativo de la carga (Priority: P1)

Cuando su carga termina, el usuario recibe un correo legible y con colores que le dice qué archivo se procesó, cómo quedó cada periodo y qué incidencias hubo, sin tener que entrar a la aplicación. [Decisión]

**Why this priority**: Es la única historia: sustituye el correo de texto mínimo.

**Independent Test**: Subir el Excel de ejemplo (con incidencias) y un Excel sin incidencias, y revisar los correos en Mailpit.

**Acceptance Scenarios**:

1. **Given** una carga Finalizado, **When** se envía el correo, **Then** muestra:
   - el asunto «Carga {idCarga} finalizada»;
   - el nombre del archivo, el `idCarga` y la fecha de fin;
   - una tabla de periodos con su resultado, cada uno con un color: verde para Completado, rojo para Rechazado o Fallido, ámbar para Bloqueado o NoProcesado;
   - las incidencias: un resumen por tipo con su recuento y el detalle de las primeras 20 (tipo, fila, código, periodo y motivo); si hay más, el aviso «y N más; consulte el detalle de la carga» (Q1);
   - un botón o enlace «Ver detalle» a `{URL del frontend}/historial/{idCarga}` si la URL está configurada (Q3).
2. **Given** una carga sin incidencias, **When** se envía, **Then** el correo indica «Sin incidencias».
3. **Given** una carga con archivo inválido (sin periodos), **When** se envía, **Then** el correo indica «Sin periodos procesados» y muestra la incidencia `ArchivoInvalido`.
4. **Given** un cliente de correo que no muestra el formato enriquecido, **When** se abre el correo, **Then** se lee la versión en texto plano equivalente: los mismos datos, periodos, resumen e incidencias, y el enlace si está configurado (Q2).
5. **Given** cualquier carga, **When** se envía el correo, **Then** el texto nunca afirma que la carga terminó «sin errores» si tiene incidencias o periodos no completados [Constitución §8].
6. **Given** un fallo al leer los datos adicionales o al enviar, **When** ocurre, **Then** se aplican los mismos reintentos, `ErrorNotificacion` y la misma regla de Notificado de la Feature 007.

### Edge Cases

- **Muchas incidencias**: el Excel de ejemplo genera 84. Se muestran el resumen por tipo con las 84 contadas, las primeras 20 en detalle y «y 64 más; consulte el detalle de la carga» (Q1).
- **URL del frontend no configurada**: el correo se envía sin el enlace «Ver detalle»; no es un error (Q3).
- **Valores con caracteres especiales**: el nombre del archivo y los motivos pueden contener `<`, `&` o comillas. Se muestran escapados, sin romper el formato ni inyectar contenido.
- **Reentrega del mensaje**: el correo se vuelve a construir con los datos del momento. El descarte por estado de la Feature 007 sigue evitando reenvíos deliberados.
- **Logs**: no se registra el cuerpo del correo.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El correo MUST conservar el asunto, el destinatario, los reintentos, `ErrorNotificacion`, el descarte por estado y la regla de Notificado de la Feature 007. [F007] [Constitución §8]
- **FR-002**: El correo MUST incluir el nombre del archivo, el `idCarga`, la fecha de fin, una tabla de resultados por periodo con un color por resultado y las incidencias. [Decisión] [Constitución §8 v3.9.0]
- **FR-003**: El correo MUST mostrar un resumen de incidencias por tipo con su recuento (sobre el total) y el detalle de las primeras 20, en el orden de registro. Si hay más, MUST indicar «y N más; consulte el detalle de la carga». [Decisión Q1, 2026-11-03]
- **FR-004**: El correo MUST enviarse con dos versiones equivalentes en el mismo mensaje: HTML con estilos integrados (sin hojas externas ni imágenes) y texto plano. [Decisión Q2, 2026-11-03]
- **FR-005**: Si la URL pública del frontend está configurada por variable de entorno, el correo MUST incluir «Ver detalle» con `{URL}/historial/{idCarga}`. Si no lo está, se omite sin error. [Decisión Q3, 2026-11-03] [Constitución §11]
- **FR-006**: Los datos MUST leerse sin cambios de esquema, solo para consulta. Los valores del usuario (nombre del archivo, motivos y códigos) MUST escaparse. [Constitución §6]
- **FR-007**: MUST NOT añadirse dependencias, adjuntos, imágenes externas ni motores de plantillas. [Constitución §12] [Decisión]

### Key Entities

- **Resumen del correo**: el nombre del archivo, `idCarga`, `fechaFin`, los periodos (periodo y resultado) y las incidencias (tipo, fila, código, periodo y motivo). Se construye con datos de lectura de la carga.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En el 100 % de los correos de prueba (con y sin incidencias, y con archivo inválido) aparecen el nombre del archivo, los periodos con su resultado coloreado y el bloque de incidencias o «Sin incidencias».
- **SC-002**: Los recuentos del correo coinciden con el detalle de la carga (Feature 015): todos los periodos, el total de incidencias por tipo y como máximo 20 en detalle, con el aviso del resto.
- **SC-003**: En Mailpit, el correo tiene una parte HTML y otra de texto plano, ambas con los mismos datos. El enlace «Ver detalle» abre el detalle de la carga en el frontend cuando la URL está configurada.
- **SC-004**: Los flujos de la Feature 007 no cambian: una carga Finalizado acaba en Notificado tras el envío y los fallos registran `ErrorNotificacion`.

## Assumptions

- **Estilos**: integrados en el propio correo, sin hojas externas ni imágenes, porque los clientes de correo suelen ignorar los estilos externos.
- **Colores**: verde para Completado; rojo para Rechazado y Fallido; ámbar para Bloqueado y NoProcesado; gris para Admitido (no debería aparecer en una carga finalizada).
- **Verificación**: manual, con Mailpit en una base temporal. Sin pruebas automatizadas [Constitución §11].

## Decisiones del usuario (2026-11-03)

| # | Pregunta | Decisión | Requisitos |
|---|---|---|---|
| Q1 | Volumen de incidencias | Resumen por tipo con su recuento, más el detalle de las primeras 20 y el aviso «y N más; consulte el detalle de la carga» | FR-003, SC-002 |
| Q2 | Formato | HTML con estilos integrados y texto plano equivalente (multipart) | FR-004, US1-4, SC-003 |
| Q3 | Enlace al detalle | «Ver detalle» con la URL del frontend configurable por entorno; se omite si no está configurada | FR-005, SC-003 |

## Asuntos pendientes (no son requisitos aprobados)

Ninguno que requiera decisión. Se fijan en el plan: el nombre de la variable de entorno de la URL del frontend y el diseño concreto de la plantilla.
