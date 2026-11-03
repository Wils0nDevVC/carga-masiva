# Feature Specification: Notificación por correo en ms-notificaciones

**Feature Branch**: `007-notificacion-correo` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v3: Q1–Q3 resueltas; Mailpit aprobado como SMTP local de pruebas, 2026-11-03)

**Input**: Consumir la cola `notificaciones` con un `BackgroundService` en `Acity.Api` de ms-notificaciones, enviar el correo con MailKit y configuración externa, cambiar Finalizado → Notificado solo tras un envío exitoso (con historial), conservar Finalizado ante un fallo de envío con su incidencia y los reintentos aprobados, y tratar reentregas y cargas ya notificadas sin repetir deliberadamente el envío. Esta etapa genera solo la especificación.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v3.4.0; `[F001 …]` = `specs/001-persistencia-integridad-carga/`; `[F004]` = entidades por servicio; `[F006 …]` = `specs/006-procesamiento-cargas/` (contrato `mensaje-notificaciones.md`); `[Decisión]` = decisión explícita del usuario (incluye el prompt de esta feature); `[Observado]` = comprobado leyendo el repositorio. **Pendiente** = no decidido; no es requisito.

## Contexto

**Estado observado** [Observado]: ms-notificaciones tiene solo `GET /health`, el manejador global, `Response<T>` y la entidad `Incidencia` (F004; `CargaArchivo` se difirió "porque marcar Notificado se hace con el procedimiento"). Sin RabbitMQ, MailKit ni Polly; en Compose solo recibe la cadena de conexión. El esquema admite el tipo de incidencia `ErrorNotificacion` y `usp_CambiarEstadoCarga` aplica Finalizado → Notificado con historial (y acepta el origen esperado opcional de V003).

**Contrato de entrada** [F006 `contracts/mensaje-notificaciones.md`]: intercambio directo durable `notificaciones`, cola durable `notificaciones`, clave `notificaciones`; mensaje persistente JSON `{ "idCarga", "usuario", "fechaFin" }` (`usuario` = correo del responsable; `fechaFin` UTC ISO 8601). Se publica una vez por carga que llega a Finalizado (también archivo inválido o todos los periodos rechazados). Un reintento del publicador puede duplicar el mensaje. **Obligación ya fijada para este consumidor**: enviar y marcar Notificado solo si la carga está Finalizado; descartar en otro caso.

**Referencias GitHub** (`referencias/repositorios.md`): consultadas con acceso web parcial en las features 005 y 006; no muestran consumidores de colas ni envío de correo. No se volvieron a consultar en esta etapa.

**Reglas aprobadas que se aplican**:

| Tema | Regla | Fuente |
|---|---|---|
| Responsabilidad | Escuchar `notificaciones`, enviar el correo al usuario indicando que la carga finalizó, con MailKit, y actualizar a Notificado | [README §2 Microservicio 3, §3 paso 4] [Constitución §8] |
| Notificado | Solo tras el envío exitoso; la notificación es por carga, no por fila | [Constitución §8] [F001 FR-037] |
| Fallo del correo | La carga conserva Finalizado, se registra la incidencia (`ErrorNotificacion`) y la notificación se reintenta sin reprocesar el archivo; nunca Fallido | [Constitución §8] [F001 FR-037, R-03] |
| Transición | Finalizado → Notificado con su historial; Notificado es terminal; repetir una transición no duplica el historial | [Constitución §8] [F001 FR-029, FR-040] |
| Reintentos | Limitados, con Polly y Circuit Breaker; parámetros fijados en esta feature; sin interfaz ni endpoint de reintentos; reintentar no duplica efectos de escritura | [Constitución §9] [F001 FR-032] |
| Configuración | MailKit configurable por variables de entorno; secretos solo por entorno | [README §4 Correo] [Constitución §6, §10] |
| Mensajes no confirmados por el publicador | No se recuperan automáticamente (limitación aceptada en la 006); esta feature no lo reabre | [Constitución §8 v3.2.0] [F006 Q2] |
| Circuito abierto | Rechazo por circuito abierto ≠ fallo terminal (no se registra como fallo definitivo por sí solo) | [F006 FR-016a, decisión H3] |
| Logs | Sin contraseñas, credenciales SMTP ni tokens | [Constitución §10] |
| Tipo de incidencia del fallo de correo | `ErrorNotificacion`: existe en el `CHECK` de `Incidencia.Tipo` (V001, conservado por V004) y está aprobado en F001 (data-model, R-03); no se modifica el contrato | [F001 data-model, R-03] [Observado] |

**Exigido por el README frente a precisiones operativas del usuario** (2026-11-03):

| Exigido por el README | Precisión operativa del usuario |
|---|---|
| Enviar un correo al usuario indicando que la carga finalizó, con MailKit (§2, §3 paso 4) | Q1: asunto "Carga {idCarga} finalizada"; cuerpo en texto plano con `idCarga` y `fechaFin`; destinatario = `usuario` del mensaje; sin resúmenes, consultas adicionales, adjuntos ni plantillas; "finalizada" no se presenta como "sin errores" |
| Actualizar el estado final a Notificado (§2, §3 paso 4) | Q2: si el correo salió pero Notificado no pudo persistirse tras los reintentos, el mensaje no se confirma; en la reentrega se consulta primero el estado (Notificado → confirmar sin reenviar; Finalizado → puede reenviarse); posible correo duplicado documentado |
| MailKit configurable por variables de entorno (§4) | Q3: los reintentos limitados satisfacen "la notificación se reintenta"; agotados → `ErrorNotificacion`, Finalizado, mensaje confirmado, sin recuperación posterior; si la incidencia no puede persistirse, no se confirma y se usa el manejo de fallos existente sin reentrega inmediata en bucle |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Notificar una carga finalizada (Priority: P1)

Cuando llega el mensaje de una carga Finalizado, el sistema envía un correo al usuario indicando que la carga finalizó y, solo si el envío termina con éxito, pasa la carga a Notificado con su historial. [README §2, §3 paso 4] [Constitución §8]

**Why this priority**: Cierra el flujo del README (Finalizado → Notificado).

**Independent Test**: Publicar en `notificaciones` el mensaje de una carga Finalizado (o procesar una carga real con ms-carga-masiva) con Mailpit como SMTP local y revisar el correo capturado, el estado y el historial.

**Acceptance Scenarios**:

1. **Given** una carga Finalizado y su mensaje en `notificaciones`, **When** se consume, **Then** se envía un correo a la dirección `usuario` del mensaje con asunto "Carga {idCarga} finalizada" y cuerpo en texto plano con `idCarga` y `fechaFin` (Q1), y la carga pasa a Notificado con una entrada de historial Notificado.
2. **Given** una carga Finalizado con omisiones de negocio, archivo inválido o todos los periodos rechazados, **When** llega su mensaje, **Then** se notifica igual: el correo indica que la carga finalizó, sin afirmar que terminó sin errores (Q1).
3. **Given** la transición aplicada, **When** se revisa el historial, **Then** hay exactamente una entrada Notificado.

---

### User Story 2 - Tratar reentregas y mensajes que no corresponden (Priority: P1)

Un mensaje de una carga que no está Finalizado (ya Notificado, Fallido u otro estado), un duplicado del publicador o un mensaje inválido se confirma y descarta sin enviar correo. [F006 contrato] [F001 FR-022]

**Why this priority**: El publicador puede duplicar mensajes y RabbitMQ puede reentregarlos; sin esta regla se enviarían correos deliberadamente repetidos.

**Independent Test**: Publicar a mano el mensaje de una carga Notificado, dos veces el de una Finalizado, uno de una carga inexistente y uno inválido.

**Acceptance Scenarios**:

1. **Given** una carga ya Notificado, **When** llega de nuevo su mensaje, **Then** no se envía correo, no cambia el estado ni el historial, y el mensaje se confirma.
2. **Given** dos mensajes de la misma carga Finalizado, **When** se consumen en orden, **Then** se envía un correo por el primero y el segundo se descarta (la carga ya está Notificado).
3. **Given** un mensaje de una carga inexistente, Pendiente, En proceso, Cargado o Fallido, **When** se consume, **Then** se descarta con log, sin correo ni cambios.
4. **Given** un mensaje que no cumple el contrato (JSON inválido, `idCarga` ausente, `usuario` vacío), **When** se consume, **Then** se descarta con log de error, sin correo ni cambios.

---

### User Story 3 - Conservar Finalizado ante un fallo de envío (Priority: P1)

Si el envío del correo falla tras los reintentos aprobados, la carga conserva Finalizado, se registra una incidencia `ErrorNotificacion` y el archivo no se reprocesa. [Constitución §8] [F001 FR-037]

**Why this priority**: Es la regla aprobada para el fallo propio de esta feature.

**Independent Test**: Detener Mailpit (o provocar errores SMTP con su función Chaos, si se confirma) y consumir el mensaje de una carga Finalizado.

**Acceptance Scenarios**:

1. **Given** el SMTP no disponible, **When** se agotan los reintentos, **Then** la carga sigue Finalizado, existe una incidencia `ErrorNotificacion` con el motivo (sin credenciales ni detalles internos), no hay historial Notificado y no se reprocesa nada de la carga. El mensaje se confirma y no hay recuperación automática posterior de esa notificación (Q3).
2. **Given** el fallo definitivo de envío y la base sin aceptar la incidencia `ErrorNotificacion`, **When** se intenta registrarla, **Then** el mensaje no se confirma y se aplica el manejo de fallos existente (espera antes de volver a consumir), sin reentrega inmediata en bucle (Q3).
3. **Given** un fallo transitorio de envío que se resuelve dentro de los reintentos, **When** se consume, **Then** se envía un único correo desde la perspectiva del servicio y la carga queda Notificado, sin `ErrorNotificacion`.
4. **Given** el servidor rechaza el destinatario o las credenciales (error permanente), **When** se envía, **Then** no se reintenta y se aplica el escenario 1.
5. **Given** el circuito del envío abierto (rechazo sin intentar el envío), **When** llega un mensaje, **Then** no se registra como fallo definitivo por sí solo (F006 FR-016a): el mensaje no se confirma y el consumidor espera la apertura antes de volver a consumir.

---

### User Story 4 - Distinguir el fallo al guardar Notificado después de enviar (Priority: P2)

Si el correo se envió pero no puede registrarse Notificado (p. ej. la base no responde), el sistema no debe presentar la carga como notificada sin evidencia ni ocultar que el correo ya salió. [Decisión: distinguir este caso del fallo de envío]

**Why this priority**: Es el único punto donde puede repetirse un correo de forma conocida; se acepta y se documenta (Q2).

**Independent Test**: Provocar un fallo de base después del envío (base temporal) y revisar estado, incidencias, mensaje y correos recibidos.

**Acceptance Scenarios**:

1. **Given** el correo enviado y el registro de Notificado fallido tras sus reintentos, **When** termina el intento, **Then** el mensaje no se confirma, la carga sigue Finalizado y no se registra `ErrorNotificacion` ni Fallido (Q2).
2. **Given** la reentrega de ese mensaje, **When** se consume, **Then** se consulta primero el estado: si ya está Notificado, se confirma sin reenviar; si sigue Finalizado, el correo puede volver a enviarse (posible duplicado documentado) (Q2).
3. **Given** ese caso, **When** se revisan los logs, **Then** consta que el correo se envió y que no pudo registrarse Notificado, con `idCarga`.

---

### Edge Cases

- Mensaje con `usuario` distinto del correo actual del usuario en la base: se usa el del mensaje (contrato de la 006); no se consulta `Usuario`.
- Carga Finalizado cuyo mensaje nunca llegó (publicación sin confirmación en la 006): no se notifica; limitación aceptada, no se reabre.
- Dos instancias de ms-notificaciones: la constitución no exige exclusividad para este consumidor; con una sola instancia en Compose y el descarte por estado, el riesgo de doble envío queda en la ventana entre enviar y registrar Notificado (aceptado en Q2). No se añade cerrojo.
- Caída del proceso después de enviar y antes de confirmar: el mensaje se reentrega; si Notificado ya quedó registrado se descarta; si sigue Finalizado, puede reenviarse (Q2).
- Correo duplicado: **no se garantiza** entrega exactamente una vez ni ausencia absoluta de duplicados; solo que no se reenvía deliberadamente a una carga Notificado.
- Configuración SMTP ausente o inválida al arrancar: el servicio no arranca (sin enviar a ciegas); la cola conserva los mensajes.

## Requirements *(mandatory)*

### Functional Requirements

**Consumo**

- **FR-001**: ms-notificaciones MUST consumir la cola `notificaciones` con un `BackgroundService` en `Acity.Api` (sin proyectos Worker), declarando la misma topología que el publicador, de a un mensaje y con confirmación manual. [README §2] [Constitución §3, §9] [F006 contrato]
- **FR-002**: Un mensaje inválido, de una carga inexistente o de una carga que no está Finalizado MUST confirmarse y descartarse sin enviar correo ni cambiar nada, con log. [F006 contrato "obligación del consumidor"] [F001 FR-022]
- **FR-003**: Para comprobar el estado antes de enviar, ms-notificaciones MUST incorporar la entidad `CargaArchivo` con solo las columnas que lee (`Id`, `Estado`) por el procedimiento de ingeniería inversa de F004. Justificación: el contrato de la 006 obliga a enviar solo si la carga está Finalizado, y eso exige leer su estado; el diferido de F004 ("`CargaArchivo` no se mapea en Notificaciones") se levanta por este caso de uso. No se mapea `Usuario`. [F004] [F006 contrato]

**Envío**

- **FR-004**: El correo MUST enviarse con MailKit a la dirección `usuario` del mensaje, con servidor, puerto, seguridad, remitente y credenciales tomados de variables de entorno; sin valores en el repositorio. [README §2, §4] [Constitución §6, §10]
- **FR-005**: El correo MUST tener asunto "Carga {idCarga} finalizada" y cuerpo en texto plano con `idCarga` y `fechaFin`, dirigido solo a `usuario` del mensaje; MUST NOT incluir resúmenes, consultas adicionales, adjuntos ni plantillas, ni presentar "finalizada" como "sin errores". [README §2, §3 paso 4] [Decisión Q1] [Constitución §8 v3.3.0]
- **FR-006**: El envío MUST protegerse con reintentos limitados y Circuit Breaker de Polly (parámetros del plan); los errores permanentes del servidor (destinatario o credenciales rechazados) MUST NOT reintentarse; un rechazo por circuito abierto MUST NOT tratarse por sí solo como fallo definitivo (F006 FR-016a). [Constitución §9] [F006 FR-016a]

**Estados e incidencias**

- **FR-007**: Solo tras un envío exitoso, la carga MUST pasar Finalizado → Notificado con `usp_CambiarEstadoCarga` y origen esperado Finalizado (historial incluido; repetición sin efecto). [Constitución §8] [F001 FR-037, FR-040] [F005 V003]
- **FR-008**: Si el envío falla de forma definitiva, la carga MUST conservar Finalizado, MUST registrarse una incidencia `ErrorNotificacion` vinculada a la carga (motivo sin credenciales) y MUST NOT reprocesarse el archivo ni marcarse Fallido. Los reintentos limitados de FR-006 satisfacen "la notificación se reintenta": al agotarse, el mensaje MUST confirmarse y MUST NOT haber recuperación automática posterior. Si la incidencia no puede persistirse, el mensaje MUST NOT confirmarse como si se hubiera registrado: se aplica el manejo de fallos existente (espera antes de volver a consumir), sin reentrega inmediata en bucle. [Decisión Q3] [Constitución §8 v3.3.0] [F001 FR-037]
- **FR-009**: Si el correo se envió y el registro de Notificado falla tras sus reintentos, MUST NOT registrarse `ErrorNotificacion` ni Fallido y el mensaje MUST NOT confirmarse. En la reentrega MUST consultarse primero el estado: Notificado → confirmar sin reenviar; Finalizado → puede reenviarse. El posible correo duplicado MUST documentarse; MUST NOT añadirse mecanismos nuevos para evitarlo. [Decisión Q2] [Constitución §8 v3.3.0]
- **FR-010**: El sistema MUST NOT prometer entrega exactamente una vez: MUST NOT reenviar deliberadamente a una carga Notificado (FR-002), y los casos que pueden repetir un correo (Q2, caída tras enviar, duplicado del publicador antes de registrar Notificado) MUST documentarse como limitaciones. [Decisión]

**Límites de alcance**

- **FR-011**: Esta feature MUST NOT incluir Autenticación, Gateway, consultas de seguimiento, endpoints de negocio, colas adicionales, frameworks de mensajería, Outbox, servicios de barrido, plantillas de correo ni pruebas automatizadas; MUST NOT reabrir la recuperación de mensajes no publicados de la 006. [Decisión] [Constitución §9, §11, §12]
- **FR-012**: RabbitMQ y la configuración SMTP MUST llegar a ms-notificaciones por variables de entorno en Compose. Para pruebas locales y demostración, Compose MUST incluir Mailpit como servidor SMTP que captura los correos sin entregarlos a buzones externos; MailKit sigue siendo la biblioteca de envío y el servidor se cambia solo por variables de entorno. Ningún otro servicio cambia. [Constitución §11 v3.4.0] [Decisión del usuario, no del README]

### Key Entities *(include if feature involves data)*

Sin tablas ni columnas nuevas.

- **Carga del archivo** (nueva en ms-notificaciones, tabla existente): `Id` y `Estado` para comprobar Finalizado; el cambio a Notificado lo hace el procedimiento.
- **Incidencia**: `ErrorNotificacion` vinculada a la carga.
- **Historial de estados**: lo escribe `usp_CambiarEstadoCarga`.
- **Mensaje de notificación**: `idCarga`, `usuario`, `fechaFin` (contrato de la 006).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Para el 100 % de las cargas Finalizado cuyo mensaje llega y cuyo envío termina con éxito, el usuario recibe un correo con el contenido aprobado y la carga queda Notificado con una entrada de historial.
- **SC-002**: 0 correos enviados por mensajes de cargas que ya estaban Notificado, Fallido o en otro estado distinto de Finalizado, y por mensajes inválidos.
- **SC-003**: Ante un fallo definitivo de envío, el 100 % de las cargas afectadas conserva Finalizado con una incidencia `ErrorNotificacion` y ningún dato de la carga se reprocesa.
- **SC-004**: Ante el fallo al guardar Notificado tras enviar, el resultado coincide con Q2 y nunca produce Fallido ni `ErrorNotificacion`.
- **SC-005**: 0 apariciones de credenciales SMTP, contraseñas o cadenas de conexión en los logs.

## Assumptions

- La verificación usa Mailpit en Compose (decisión del usuario, 2026-11-03): el envío por SMTP es real hasta Mailpit, que captura el correo; no hay entrega a buzones externos. La entrega externa no se verifica en esta feature. [Constitución §11 v3.4.0]
- Los parámetros de Polly (reintentos, tiempos, circuito) los fija el plan dentro de lo aprobado. [Constitución §9]
- Una sola instancia de ms-notificaciones en Compose. [Observado]
- Validado solo en el equipo de desarrollo; el equipo de evaluación queda sin validar.

## Asuntos pendientes (no son requisitos aprobados)

Ninguno que requiera decisión del usuario. Resueltos el 2026-11-03: **Q1** contenido del correo (FR-005), **Q2** fallo al guardar Notificado después de enviar (FR-009), **Q3** tras agotar los reintentos del envío (FR-008).

**Limitaciones aceptadas**: (1) si el correo salió y Notificado no pudo persistirse, la reentrega puede enviar un segundo correo (Q2); (2) una carga cuya notificación agotó los reintentos queda Finalizado con `ErrorNotificacion` y no se notifica después de forma automática (Q3); (3) no se garantiza entrega exactamente una vez.

Para el plan: parámetros de Polly del envío y del registro de Notificado, clasificación de errores SMTP permanentes frente a transitorios, nombres de las variables SMTP y formato exacto de `fechaFin` en el cuerpo.
