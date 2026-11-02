# Feature Specification: Recepción y registro de cargas en ms-control

**Feature Branch**: `005-recepcion-registro-cargas` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-02

**Status**: Draft (v3: aclaraciones Q1–Q3, pendientes del plan y mecanismo de FR-023 resueltos por el usuario)

**Input**: Recepción del Excel por un controlador HTTP de ms-control, identificación del usuario y validación del permiso, validaciones del archivo definidas en las fuentes, almacenamiento en SeaweedFS, registro de la carga en estado Pendiente y publicación del mensaje en la cola `carga_masiva`, respetando `Response<T>` y el manejo de errores aprobado. Sin consumidores, procesamiento de filas, consultas de seguimiento, correos, Autenticación ni Gateway. Esta etapa genera solo la especificación.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v3.1.0; `[Decisión]` = decisión explícita del usuario (prompt de la feature y aclaraciones del 2026-11-02); `[F001]` = `specs/001-persistencia-integridad-carga/` (reglas, modelo, contratos de procedimientos); `[F002]` = fundación (`Response<T>`, manejo global de errores); `[F004]` = entidades por servicio; `[Observado]` = comprobado leyendo el repositorio en esta etapa; `[Referencia]` = repositorio de referencia. **Pendiente** = asunto no decidido; no es requisito.

## Contexto

**Estado observado** [Observado]:
- ms-control tiene solo `GET /health`, el manejador global (`ValidationException` → 400 con `Errors`; otra excepción → 500 genérico; 404/405 por páginas de estado), `Response<T>` (`Data`, `IsSuccess`, `Message`, `Code`, `Errors`) y las entidades `CargaArchivo`, `CargaEstadoHistorial` e `Incidencia` mapeadas sobre `CargaMasivaApp` (F004). No tiene autenticación ni paquetes de RabbitMQ, SeaweedFS o Polly.
- `compose.yaml` declara SQL Server, el runner de `database/` y los cuatro servicios; **no** declara RabbitMQ ni SeaweedFS (F002 los dejó para la feature que los use). El puerto de Control (8082) está publicado.
- ms-autenticacion no emite todavía ningún JWT y `api-gateway/` está vacío: hasta esta feature **no existía contrato de claims** en ningún documento aprobado.
- Esquema (`database/scripts/V001`): `CargaArchivo` exige `NombreArchivo` (NVARCHAR(260)), `UsuarioId` (clave hacia `Usuario`), `FechaRecepcion`, `RutaArchivo` (NVARCHAR(500), obligatoria) y `Estado`; `CargaEstadoHistorial` guarda cada estado; `Incidencia` admite el tipo `ErrorTecnico`; `Usuario` guarda `Email` y `RolId`. `usp_CambiarEstadoCarga` (V002) valida el origen contra la matriz y permite tanto Pendiente → Fallido como En proceso → Fallido.

**Referencias GitHub** (`referencias/repositorios.md`) [Referencia]: acceso web parcial. Clean Architecture: estructura `Todo.Api/Application/Common/Domain/Infrastructure` y `TodoController` (`ControllerBase`, `[ApiController]`, delega con `IMediator.Send`, sin `[Authorize]` ni carga de archivos). API Gateway: `Program.cs` sin `AddJwtBearer` ni reenvío de claims (usa una validación por cabecera propia, excluida por la constitución §9); `configuration.json` vacío. No se revisaron los demás archivos; ninguna referencia muestra recepción de archivos, almacenamiento, publicación en colas ni lectura de claims.

**Decisiones aprobadas que esta feature aplica**:
- Roles Administrador y Operador; **ambos pueden cargar archivos** [Constitución §6] [F001 FR-002]. Control valida el permiso [README §3] [Constitución §9].
- Control crea la carga y el historial inicial Pendiente en la misma transacción y publica el mensaje [F001 data-model "Transiciones", research R-03].
- Mensaje de la cola `carga_masiva`: `idCarga`, `rutaArchivo`, `usuario` (correo) [README §3 paso 2].
- Se conserva la referencia al archivo y **nunca** su contenido binario en la base [F001 FR-007].
- Estados y transiciones: solo los seis aprobados; Pendiente → Fallido solo ante un fallo técnico terminal; un error transitorio recuperable no cambia el estado [Constitución §8].
- Un mensaje reentregado de una carga que ya no está Pendiente se confirma y descarta sin reprocesar (responsabilidad del consumidor de Carga Masiva) [F001 research R-03 punto 6, FR-022].
- Aclaraciones del 2026-11-02 [Decisión]: Q1 = Control valida el JWT además del Gateway, con identificador numérico y rol en el token y el correo tomado del usuario persistido; Q2 = máximo predeterminado de 10 MiB configurable; Q3 = subir → registrar → publicar, con Fallido ante un fallo definitivo de publicación; más los pendientes resueltos de 0 bytes, varios archivos, nombre largo, HTTP 202, `Data` nulo, Rate Limiting en Control y criterios de Polly.
- Mecanismo de FR-023 [Decisión, opción A]: un script versionado nuevo en `database/` incorpora a `usp_CambiarEstadoCarga` un estado de origen esperado **opcional**; comprobación y cambio son atómicos; las llamadas existentes conservan su comportamiento.

## Contrato de identidad del token (para Autenticación) [Decisión Q1]

Control consume este contrato; la feature de Autenticación MUST emitir tokens que lo cumplan.

| Dato | Claim | Valor | Uso en Control |
|---|---|---|---|
| Identificador del usuario | `sub` (claim registrado de JWT) | `Usuario.Id` en decimal (texto de un entero positivo) | `CargaArchivo.UsuarioId`; búsqueda del usuario persistido |
| Rol | `role` | Exactamente `Administrador` u `Operador` (`Rol.Nombre`) | Permiso de carga |

- **No** se exige el correo como claim. Si viene, Control no lo usa: el `usuario` del mensaje se toma de `Usuario.Email` persistido.
- Firma, algoritmo, emisor, audiencia, vigencia y el origen de la clave por configuración externa los fija el plan; deben ser los mismos para Autenticación, Gateway y Control. Sin clave en archivos versionados. [Constitución §6, §10]
- Un token sin `sub` numérico válido o sin `role` reconocible no autentica ni autoriza; no hay valores por defecto.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Subir un Excel válido y obtener la carga registrada (Priority: P1)

Un usuario autenticado con rol Administrador u Operador envía un único archivo `.xlsx` no vacío, dentro del tamaño permitido. El sistema guarda el archivo en el almacenamiento de archivos, registra la carga en estado Pendiente con quién la subió y cuándo, deja constancia en el historial, publica el aviso para su procesamiento y responde con el identificador de la carga. [README §2 Microservicio 1, §3 paso 2] [F001 US1] [Decisión]

**Why this priority**: Es la entrada de todo el flujo; sin ella no hay procesamiento, notificación ni trazabilidad.

**Independent Test**: Con un token válido de cada rol, enviar el Excel de ejemplo y comprobar la respuesta, la fila de la carga y su historial en la base, el archivo en el almacenamiento y el mensaje en la cola `carga_masiva` (sin consumidor).

**Acceptance Scenarios**:

1. **Given** un usuario autenticado con rol Operador, **When** envía un `.xlsx` válido, **Then** la respuesta es HTTP 202 con `Response<T>` (`isSuccess` verdadero, `code` 202) y `data` contiene el `idCarga` y el estado Pendiente.
2. **Given** la misma solicitud con rol Administrador, **When** se envía, **Then** el resultado es el mismo.
3. **Given** una carga recién aceptada, **When** se revisa la base, **Then** existe una carga con ese `idCarga`, el nombre original del archivo, el `Usuario.Id` del token, la fecha de recepción, la referencia al archivo almacenado, estado Pendiente y exactamente una entrada de historial Pendiente; no hay contenido binario del Excel.
4. **Given** una carga recién aceptada, **When** se revisa el almacenamiento de archivos, **Then** el archivo está en la ubicación registrada en la carga y su contenido es idéntico al enviado.
5. **Given** una carga recién aceptada, **When** se revisa la cola `carga_masiva`, **Then** hay un mensaje para ella con `idCarga`, `rutaArchivo` igual a la referencia registrada y `usuario` igual al `Usuario.Email` persistido del responsable.
6. **Given** dos envíos del mismo archivo, **When** ambos se aceptan, **Then** existen dos cargas distintas con su propio `idCarga`, archivo almacenado y mensaje (las fuentes no definen detección de archivos repetidos; los periodos los evalúa Carga Masiva).
7. **Given** un `.xlsx` de exactamente 10 485 760 bytes con el máximo predeterminado, **When** se envía, **Then** se acepta (el contenido adicional del multipart no lo impide).

---

### User Story 2 - Rechazar solicitudes sin identidad o sin permiso (Priority: P1)

Una solicitud sin token válido, o cuyo usuario no tiene un rol con permiso de carga, se rechaza antes de almacenar, registrar o publicar nada. Control valida el token por sí mismo, aunque la solicitud no pase por el Gateway. [README §2 Gateway, §3 paso 2] [Constitución §6, §9] [Decisión Q1]

**Why this priority**: El permiso es un requisito de negocio del README y la carga debe identificar siempre a su responsable.

**Independent Test**: Enviar el Excel directamente a Control sin token, con un token de firma inválida, caducado, sin `sub`, sin `role` y con un rol distinto; comprobar en cada caso el estado HTTP, el cuerpo `Response<T>` y que no se creó carga, archivo ni mensaje.

**Acceptance Scenarios**:

1. **Given** una solicitud sin token, **When** llega a Control, **Then** responde 401 con `Response<T>` (`isSuccess` falso, `code` 401, `data` null) y no se crea carga, archivo ni mensaje.
2. **Given** un token de firma inválida, caducado, de otro emisor o audiencia, o sin `sub` numérico, **When** llega a Control, **Then** responde igual que sin token.
3. **Given** un token válido cuyo `role` no es Administrador ni Operador o no existe, **When** envía un archivo, **Then** responde 403 con `Response<T>`, sin efectos.
4. **Given** un token válido cuyo `sub` no corresponde a un usuario persistido, **When** envía un archivo, **Then** no se almacena el archivo, no se registra la carga ni se publica mensaje; la respuesta no es satisfactoria (el plan fija el código dentro de 401/403).
5. **Given** cualquier rechazo de esta historia, **When** se revisan los logs, **Then** no contienen el token.

---

### User Story 3 - Rechazar archivos que no cumplen las validaciones (Priority: P1)

Antes de almacenar o registrar nada, Control comprueba que la solicitud trae exactamente un archivo, no vacío, con extensión `.xlsx`, con un nombre que cabe en el esquema y que no supera el tamaño máximo configurado. Un incumplimiento se responde como error con su motivo y sin efectos. [README §2 Microservicio 1, §3 paso 2] [Constitución §5, §9] [Decisión]

**Why this priority**: Son las validaciones de archivo que el README asigna a Control, precisadas por el usuario.

**Independent Test**: Enviar una solicitud sin archivo, con dos archivos, un `.xlsx` de 0 bytes, un `.xls`, un `.csv`, un archivo sin extensión, un nombre de 261 caracteres y un `.xlsx` mayor que el máximo; comprobar la respuesta y la ausencia de efectos.

**Acceptance Scenarios**:

1. **Given** una solicitud autenticada sin archivo, **When** se envía, **Then** la respuesta es 400 con `errors` indicando el motivo y no hay efectos.
2. **Given** una solicitud con dos o más archivos, **When** se envía, **Then** se rechaza completa con 400 (no se toma ninguno) y no hay efectos.
3. **Given** un `.xlsx` de 0 bytes, **When** se envía, **Then** se rechaza con 400 sin efectos.
4. **Given** un archivo con extensión distinta de `.xlsx` (p. ej. `.xls`, `.csv`, sin extensión), **When** se envía, **Then** se rechaza con 400 sin efectos.
5. **Given** un archivo `.XLSX`, **When** se envía dentro del tamaño, **Then** se acepta como `.xlsx`.
6. **Given** un nombre de archivo de más de 260 caracteres, **When** se envía, **Then** se rechaza con 400 indicando la longitud máxima; el nombre no se trunca.
7. **Given** un `.xlsx` de 10 485 761 bytes con el máximo predeterminado, **When** se envía, **Then** se rechaza indicando que excede el tamaño permitido, con `Response<T>` y `code` igual al estado HTTP, sin efectos.
8. **Given** el máximo configurado por variable de entorno con otro valor, **When** se envían archivos justo por debajo, igual y por encima de ese valor, **Then** se aceptan los dos primeros y se rechaza el tercero.
9. **Given** un `.xlsx` con contenido que no es un Excel legible, **When** se envía cumpliendo lo anterior, **Then** Control lo acepta y lo registra: la legibilidad la trata la feature de procesamiento [F001 Edge Cases "Archivo ilegible"].

---

### User Story 4 - Limitar la frecuencia de cargas (Priority: P2)

El endpoint de carga de Control aplica Rate Limiting; las solicitudes que exceden el límite se rechazan sin efectos. [README §4 "Implementar Patrón Rate Limiting"] [Constitución §9] [Decisión]

**Why this priority**: Requisito técnico obligatorio del README; no altera el flujo funcional.

**Independent Test**: Enviar solicitudes por encima del límite configurado y comprobar la respuesta y la ausencia de efectos de las rechazadas.

**Acceptance Scenarios**:

1. **Given** el límite configurado alcanzado, **When** llega otra solicitud de carga, **Then** responde 429 con `Response<T>` (`code` 429) y no se crea carga, archivo ni mensaje.
2. **Given** el límite, **When** se consulta `GET /health`, **Then** no está sujeto a él.

---

### User Story 5 - Responder con claridad ante fallos técnicos del flujo (Priority: P2)

El flujo es: subir el archivo → registrar carga Pendiente e historial en una transacción → publicar el mensaje. Cada paso solo se ejecuta si el anterior terminó bien; los fallos técnicos se reintentan de forma limitada y, si persisten, se responden sin detalles internos y dejan la carga en un estado coherente con las transiciones aprobadas. No se promete atomicidad entre almacenamiento, base y cola. [Constitución §8, §9] [F001 FR-031, FR-032] [Decisión Q3]

**Why this priority**: Los fallos parciales afectan la trazabilidad, pero no son el flujo principal.

**Independent Test**: Detener por separado SeaweedFS, SQL Server y RabbitMQ durante un envío y revisar la respuesta, la base, el almacenamiento, la cola y los logs.

**Acceptance Scenarios**:

1. **Given** SeaweedFS no disponible, **When** se envía un archivo válido y se agotan los reintentos, **Then** no se crea la carga ni se publica mensaje, y la respuesta es de error técnico (`Response<T>`, `data` null, sin trazas ni secretos).
2. **Given** el archivo ya subido y la base no disponible para registrar la carga, **When** se agotan los reintentos, **Then** no se publica mensaje, la respuesta es de error técnico y el archivo puede quedar almacenado sin carga (limitación conocida, registrada en el log con su ubicación).
3. **Given** la carga registrada y la publicación fallida de forma definitiva (rechazada por el intermediario o sin confirmación tras los reintentos), **When** la carga sigue Pendiente, **Then** pasa a Fallido con su entrada de historial y una incidencia `ErrorTecnico`, y la respuesta es de error técnico.
4. **Given** una publicación sin confirmación (p. ej. por tiempo de espera), **When** Control la trata como fallo definitivo, **Then** no afirma que el mensaje no llegó: si el mensaje llegó, el consumidor encuentra la carga en un estado distinto de Pendiente y lo descarta sin reprocesar [F001 R-03].
5. **Given** que el mensaje llegó y el consumidor ya pasó la carga a En proceso antes de que Control registre el fallo, **When** Control solicita Fallido con origen esperado Pendiente, **Then** el cambio no se aplica: el estado actual no se sobrescribe, no se añade historial Fallido ni incidencia `ErrorTecnico`, y Control responde 202 porque la salida de Pendiente evidencia que el mensaje se recibió.
6. **Given** un reintento de publicación tras una confirmación perdida, **When** el intermediario recibe dos mensajes para la misma carga, **Then** el procesamiento ocurre como máximo una vez (el segundo encuentra la carga fuera de Pendiente) [F001 FR-022].
7. **Given** un fallo transitorio que se resuelve dentro de los reintentos, **When** se envía un archivo válido, **Then** la respuesta es 202, hay una sola carga y un solo archivo almacenado, y el estado no cambió por el error transitorio.
8. **Given** que tampoco puede registrarse Fallido (p. ej. la base cae tras la publicación fallida), **When** se agotan los reintentos, **Then** la carga queda Pendiente, el log registra el `idCarga` y la causa, y la respuesta es de error técnico (limitación conocida).
9. **Given** cualquier fallo técnico, **When** se revisan los logs, **Then** registran la excepción con el contexto de la carga (si existe) sin contraseñas, tokens, cadenas de conexión ni contenido del Excel.

---

### Edge Cases

- Tamaño máximo no configurado: se usa el predeterminado de 10 MiB. Un valor configurado no válido (no numérico, cero o negativo) impide el arranque de Control con un error claro (no se aceptan archivos sin límite).
- Máximo configurado por encima de los límites HTTP predeterminados del servidor: los límites de cuerpo de la solicitud y del multipart MUST ajustarse al máximo configurado más el margen del contenido adicional, de modo que el rechazo por tamaño sea el de esta feature y no un corte del servidor sin `Response<T>`.
- El mismo usuario sube varias veces seguidas: cada envío es una carga independiente, sujeta al Rate Limiting (US1-6, US4).
- El archivo contiene periodos ya cargados: Control lo acepta; rechazar o bloquear periodos corresponde a Carga Masiva [README §3 paso 3].
- Mensaje llegado para una carga que Control marcó Fallido: Fallido es terminal; el consumidor lo descarta [Constitución §8] [F001 R-03].
- Archivo almacenado sin carga por un fallo del registro: queda en SeaweedFS; no se crea un proceso de limpieza (sin servicios de recuperación). El plan puede intentar eliminarlo una vez si es seguro, sin garantizarlo.
- Solicitud directa a Control sin pasar por el Gateway: exige la misma identidad y permiso (Q1).

## Requirements *(mandatory)*

### Functional Requirements

**Recepción y respuesta**

- **FR-001**: Control MUST exponer una única operación HTTP de recepción de un archivo Excel por solicitud, mediante un controlador de `Acity.Api` que delega en un caso de uso de la capa de aplicación. [README §2 Microservicio 1] [Constitución §3, §4] [Decisión]
- **FR-002**: MUST NOT añadirse otros endpoints de negocio (consultas de historial, detalle, contenido del archivo, reintentos). [Decisión] [Constitución §1, §9]
- **FR-003**: Una carga aceptada MUST responder HTTP 202 con `Response<T>` cuyo `data` incluye el `idCarga` y el estado Pendiente; `code` = 202. [Decisión] [Constitución §5]
- **FR-004**: Toda respuesta de esta operación, satisfactoria o de error, MUST serializar los cinco campos de `Response<T>` con nombres JSON en camelCase (`data`, `isSuccess`, `message`, `code`, `errors`); cuando no hay resultado, `data` MUST aparecer como `null`. `code` MUST coincidir con el estado HTTP real. [Decisión] [Constitución §5] [F002 contrato]

**Identidad y permiso** [Decisión Q1]

- **FR-005**: Control MUST validar por sí mismo la firma y la vigencia del JWT (además del Gateway) con la misma configuración de firma, emisor y audiencia que Autenticación; MUST NOT confiar en una cabecera propia, en datos del cuerpo o la consulta, en usuarios fijos ni en modos que omitan la seguridad. [README §2] [Constitución §9] [Decisión]
- **FR-006**: Control MUST obtener del token únicamente el identificador numérico del usuario (`sub`) y su rol (`role`), según el "Contrato de identidad del token"; MUST NOT exigir el correo como claim. Este contrato MUST documentarse para que la feature de Autenticación lo respete. [Decisión]
- **FR-007**: Sin token válido (ausente, firma inválida, caducado, emisor o audiencia distintos, `sub` no numérico) Control MUST responder 401; con token válido y rol distinto de Administrador u Operador, 403. Ambos con `Response<T>` y sin efectos. [README §3 paso 2] [Constitución §5, §6, §9]
- **FR-008**: Control MUST obtener el usuario persistido a partir de `sub` y tomar de él el correo para el mensaje. Si no existe, MUST NOT almacenar el archivo, registrar la carga ni publicar mensaje. [Decisión Q1] [F001 US1-3] [Esquema `FK_CargaArchivo_Usuario`]
- **FR-009**: Para FR-008, Control MUST incorporar la entidad `Usuario` (solo las columnas que necesita leer, sin `PasswordHash` si el mapeo lo permite sin redefinir el esquema; el plan lo resuelve) con el procedimiento de F004; queda justificado el diferido de F004 ("`Usuario` en Control depende de los claims"). [Constitución §6] [F004]

**Validaciones del archivo**

- **FR-010**: Control MUST admitir exactamente un archivo por solicitud; sin archivo o con más de uno, MUST rechazar la solicitud completa con 400. [README §3 paso 2] [Decisión]
- **FR-011**: Control MUST rechazar con 400 un archivo de 0 bytes. [Decisión]
- **FR-012**: Control MUST aceptar solo la extensión `.xlsx`, sin distinguir mayúsculas de minúsculas; cualquier otra MUST rechazarse con 400. [README §2 Cliente "(.xlsx)", §3 paso 2 "extensión correcta"]
- **FR-013**: Control MUST rechazar con 400 un nombre de archivo que supere la longitud admitida por el esquema (260 caracteres, `CargaArchivo.NombreArchivo`); MUST NOT truncarlo. [Decisión] [Esquema V001]
- **FR-014**: Control MUST rechazar un archivo que exceda el tamaño máximo, con `Response<T>`, `code` igual al estado HTTP y `errors` con el motivo. El máximo predeterminado MUST ser 10 MiB (10 485 760 bytes), configurable por variable de entorno; un archivo de tamaño igual al máximo MUST aceptarse. [README §2 Microservicio 1 "tamaño máximo configurado"] [Decisión Q2]
- **FR-015**: Los límites HTTP del servidor (cuerpo de la solicitud y multipart) MUST permitir recibir un archivo del tamaño máximo configurado junto con el contenido adicional del multipart; el plan comprueba los valores predeterminados del servidor y los ajusta si el máximo configurado lo requiere. [Decisión Q2]
- **FR-016**: Control MUST NOT leer ni validar el contenido del Excel (columnas, filas, periodos, productos) ni añadir otras validaciones de archivo. [Constitución §1, §7] [F001 "archivo ilegible: feature de procesamiento"]
- **FR-017**: Rate Limiting (FR-027), autenticación, permiso y validaciones del archivo MUST resolverse antes de almacenar el archivo, registrar la carga o publicar el mensaje. [Decisión]

**Almacenamiento, registro y publicación** [Decisión Q3]

- **FR-018**: El orden MUST ser: (1) subir el archivo a SeaweedFS; (2) registrar la carga Pendiente y su entrada de historial Pendiente en **una** transacción; (3) publicar el mensaje. Cada paso solo se ejecuta si el anterior terminó bien. [README §2, §3 paso 2] [F001 FR-006, FR-035] [Decisión]
- **FR-019**: La carga MUST registrar el nombre original del archivo, el `Usuario.Id` del token, la fecha de recepción, la referencia al archivo subido y el estado Pendiente; MUST NOT guardar el contenido binario. [README §3 paso 2 "auditoría de quién y cuándo"] [F001 FR-006, FR-007]
- **FR-020**: Si falla definitivamente la subida, MUST NOT crearse la carga ni publicarse mensaje. Si falla definitivamente el registro, MUST NOT publicarse mensaje. [Decisión]
- **FR-021**: El mensaje MUST publicarse en la cola `carga_masiva` con `idCarga`, `rutaArchivo` (la referencia registrada) y `usuario` (`Usuario.Email` persistido), en el formato JSON del README, sin campos adicionales. [README §2 Cola 1, §3 paso 2] [Constitución §9] [Decisión Q1]
- **FR-022**: Control MUST considerar publicado el mensaje solo cuando el intermediario confirme su recepción; la ausencia de confirmación (incluido un tiempo de espera) MUST NOT interpretarse como prueba de que el mensaje no llegó. El plan define la confirmación y el tratamiento de duplicados con los mecanismos mínimos de RabbitMQ y las reglas aprobadas; MUST NOT añadirse Outbox, servicios de recuperación, tablas, estados nuevos ni frameworks. [Decisión] [Constitución §9, §12]
- **FR-023**: Ante un fallo definitivo de publicación, Control MUST solicitar el paso a Fallido mediante `usp_CambiarEstadoCarga` con estado de origen esperado Pendiente. Si el procedimiento lo aplica, queda la entrada de historial Fallido y Control registra a continuación la incidencia `ErrorTecnico` vinculada a la carga. Si la carga ya no está Pendiente, MUST NOT sobrescribirse su estado ni registrarse historial Fallido ni incidencia `ErrorTecnico` de una transición que no ocurrió. [Decisión Q3, opción A] [Constitución §8, §9] [F001 FR-031, FR-033, contrato "No hace: registrar incidencias técnicas"]
- **FR-024**: Una respuesta 202 MUST implicar que el archivo está almacenado, la carga y su historial Pendiente confirmados, y la publicación confirmada por el intermediario o evidenciada porque la carga ya salió de Pendiente (FR-023, US5-5). [Decisión]
- **FR-025**: Un error transitorio que se resuelve dentro de los reintentos MUST NOT cambiar el estado ni dejar más de un archivo o carga por solicitud. [Constitución §8, §9]

**Fallos, reintentos, Rate Limiting y errores**

- **FR-026**: Las operaciones contra SeaweedFS, la base y RabbitMQ MUST protegerse con reintentos limitados y Circuit Breaker de Polly, con parámetros definidos en el plan **por operación** según su riesgo de duplicación; MUST NOT reintentarse errores de validación, autenticación o autorización. [Constitución §9] [Decisión]
- **FR-027**: Control MUST aplicar Rate Limiting al endpoint de carga, con parámetros definidos en el plan y respuesta 429 con `Response<T>` sin efectos; MUST NOT aplicarse a `/health` ni duplicarse por costumbre en el Gateway. [README §4] [Constitución §9] [Decisión]
- **FR-028**: Un fallo técnico definitivo MUST responderse con `Response<T>` de error (`data` null), sin trazas, cadenas de conexión, rutas internas ni secretos, mediante el manejo global aprobado; MUST NOT existir un segundo formato de error. [Constitución §5] [F002]
- **FR-029**: Los logs MUST registrar la recepción, el resultado y los fallos con propiedades estructuradas (`idCarga`, nombre de archivo, `Usuario.Id`, ubicación del archivo ante un archivo sin carga) y MUST NOT registrar tokens, contraseñas, cadenas de conexión ni el contenido del archivo. [Constitución §10]

**Límites de alcance**

- **FR-030**: Esta feature MUST NOT incluir consumidores de RabbitMQ, lectura o procesamiento del Excel, consultas de seguimiento, envío de correos, la cola `notificaciones`, la emisión de tokens en Autenticación, el Gateway, React ni pruebas automatizadas. [Decisión] [Constitución §11]
- **FR-032**: Un script versionado nuevo en `database/scripts/` (aplicado por el runner existente, sin modificar V001 ni V002) MUST incorporar a `usp_CambiarEstadoCarga` un parámetro **opcional** de estado de origen esperado: (a) si no se envía, el procedimiento se comporta exactamente como hoy (compatibilidad con las llamadas existentes y futuras de Carga Masiva y Notificaciones); (b) si se envía y el estado actual no coincide, MUST devolver `Cambiado` = 0 sin actualizar la carga, sin insertar historial ni tocar periodos; (c) la lectura del estado actual, la comparación y el cambio MUST ocurrir en la misma transacción con el bloqueo de fila que el procedimiento ya usa; (d) se conservan la validación de la matriz, los terminales, la idempotencia y la regla de no invocarlo dentro de una transacción abierta. Sin procedimientos, tablas ni estados nuevos. [Decisión opción A] [Constitución §6] [F001 contrato `usp_CambiarEstadoCarga`]
- **FR-031**: RabbitMQ y SeaweedFS MUST declararse en el Compose raíz como dependencias de Control, sin crear directorios salvo que requieran archivos de configuración; la configuración de firma del JWT MUST llegar a Control por variables de entorno. [Constitución §11] [F002 research]

### Key Entities *(include if feature involves data)*

Sin tablas nuevas; se usan las existentes [F001] [F004].

- **Carga del archivo**: `idCarga`; nombre original, usuario responsable, fecha de recepción, referencia al archivo en SeaweedFS y estado (Pendiente al crearse; Fallido solo ante fallo definitivo de publicación).
- **Historial de estados**: entrada Pendiente creada con la carga; entrada Fallido si aplica FR-023.
- **Incidencia**: `ErrorTecnico` vinculada a la carga cuando aplica FR-023.
- **Usuario** (nueva entidad en Control, tabla existente): identificador y correo para el mensaje.
- **Mensaje de carga**: `idCarga`, `rutaArchivo`, `usuario`; formato del README.
- **Identidad del solicitante**: `sub` y `role` del token (contrato documentado arriba).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En la comprobación manual, el 100 % de los envíos válidos (uno por rol) responde 202 y produce exactamente una carga Pendiente con una entrada de historial, un archivo almacenado idéntico al enviado y un mensaje en `carga_masiva` cuyos tres campos coinciden con lo registrado y con el correo persistido.
- **SC-002**: El 100 % de los envíos rechazados (sin token, token inválido, sin permiso, usuario inexistente, sin archivo, varios archivos, 0 bytes, extensión, nombre largo, tamaño excedido, límite de frecuencia) deja 0 cargas, 0 archivos y 0 mensajes nuevos.
- **SC-003**: El 100 % de las respuestas comprobadas usa `Response<T>` con los cinco campos en camelCase, `data` null cuando no hay resultado, `code` igual al estado HTTP y sin trazas, rutas internas ni secretos.
- **SC-004**: Los rechazos por autenticación (401), permiso (403), validación (400) y frecuencia (429) son distinguibles en el 100 % de los casos comprobados.
- **SC-005**: Ante cada fallo técnico simulado, el resultado coincide con US5: sin carga si falla la subida; sin mensaje si falla el registro; Fallido con historial e incidencia si falla definitivamente la publicación y la carga seguía Pendiente; ninguna carga En proceso o posterior pasa a Fallido por Control ni recibe historial Fallido o incidencia `ErrorTecnico` por ello; ningún estado fuera de los seis aprobados.
- **SC-006**: Con el máximo predeterminado, un archivo de 10 485 760 bytes se acepta y uno de 10 485 761 se rechaza con `Response<T>`.
- **SC-008**: Tras aplicar el script de FR-032 sobre una base temporal: una llamada sin estado esperado produce el mismo resultado que antes para cada transición de la matriz; una llamada con origen esperado Pendiente sobre una carga En proceso devuelve `Cambiado` = 0 y deja carga, historial y periodos sin cambios; sobre una carga Pendiente aplica Fallido con su historial.
- **SC-007**: 0 coincidencias de tokens, contraseñas, cadenas de conexión o contenido del Excel en los logs de Control tras ejecutar todos los escenarios.

## Limitaciones conocidas (no se prometen garantías entre sistemas) [Decisión Q3]

- No hay atomicidad entre SeaweedFS, SQL Server y RabbitMQ.
- Un fallo del registro tras la subida puede dejar un archivo almacenado sin carga; se registra en el log y no hay limpieza automática.
- Una publicación sin confirmación puede haber llegado: la carga queda Fallido y el consumidor descarta el mensaje porque ya no está Pendiente.
- Un reintento tras una confirmación perdida puede duplicar el mensaje; el procesamiento ocurre como máximo una vez porque el consumidor descarta las cargas fuera de Pendiente (depende de la feature de Carga Masiva, que debe cumplir F001 R-03).
- Si tampoco puede registrarse Fallido, la carga queda Pendiente sin mensaje confirmado; el log conserva el `idCarga` y la causa, y no hay proceso de recuperación.
- El paso a Fallido es atómico con su historial (FR-032), pero la incidencia `ErrorTecnico` la inserta Control después, según el contrato de F001; si esa inserción falla, la carga queda Fallido sin incidencia y el log conserva la causa.

## Assumptions

- La feature de Autenticación emitirá tokens conforme al "Contrato de identidad del token"; mientras no exista, la verificación manual usa tokens firmados con la misma configuración que se dará a Autenticación, **nunca** un mecanismo que omita la validación. Cómo generarlos para la verificación lo propone el plan, fuera del código de producción. [Dependencia]
- Los envíos llegarán normalmente a través del Gateway; esta feature no lo implementa ni define sus rutas. [README §2]
- La verificación es manual (Postman y revisión de base, almacenamiento, cola y logs); sin pruebas automatizadas. [Constitución §11]
- Las versiones de paquetes (RabbitMQ, cliente de SeaweedFS o su API HTTP, Polly, validación JWT) se verifican en el plan (compatibilidad y licencia). [Constitución §12]
- El tipo de intercambio (directo o topic) y la durabilidad de la cola y del mensaje los fija el plan dentro de lo que permite el README. [README §4 Mensajería]
- Validado solo en el equipo de desarrollo (SQL Server amd64 emulado); el equipo de evaluación queda sin validar. [Constitución Pendientes]

## Asuntos pendientes (no son requisitos aprobados)

- Ninguno que requiera decisión del usuario. (El mecanismo de FR-023 quedó resuelto: opción A, FR-032.)
- Para el plan, sin aprobación por detalle local: parámetros de Polly por operación, parámetros del Rate Limiting, configuración de firma JWT, intercambio y durabilidad de RabbitMQ, forma de la referencia `rutaArchivo`, código exacto de US2-4, nombre de la variable de entorno del tamaño máximo, y número y nombre del script de FR-032 y de su parámetro.
