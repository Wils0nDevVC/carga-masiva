# Feature Specification: Persistencia y reglas de integridad para la carga masiva

**Feature Branch**: `001-persistencia-integridad-carga` (no se creó rama; se trabaja en `main`)

**Created**: 2026-11-02

**Status**: Draft (v4: decisiones de precisión, fallo terminal, duplicados, espacios y hash incorporadas)

**Input**: User description: "Feature: persistencia y reglas de integridad para la carga masiva" (prompt `/speckit-specify` del usuario) y decisiones aprobadas posteriores sobre estados, fallos, reintentos, identidad y valores inválidos.

**Procedencia**: `[README §n]` = sección de `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v1.3.0; `[Decisión]` = decisión explícita del usuario (no proviene del README). Lo no resuelto figura como pendiente y no como requisito.

## Contexto

Examen técnico de carga masiva con cuatro microservicios (Autenticación, Control, Carga Masiva y Notificaciones) y un API Gateway. Esta primera feature define, de forma conjunta y mínima, **qué información debe conservarse y qué reglas de integridad debe garantizar** la persistencia para los cuatro microservicios. Es solo especificación: no define modelo físico, SQL, APIs, consumidores ni código.

Archivo de entrada: Excel con las columnas Periodo, CodigoProducto, NombreProducto y Precio. Un mismo archivo puede incluir varios periodos (el Excel de ejemplo contiene varios). [Constitución §7]

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar la carga y su responsable (Priority: P1)

Cuando un usuario presenta un archivo, el sistema conserva una carga identificada por `idCarga`, con el archivo recibido, el usuario responsable y la fecha de recepción, en estado inicial Pendiente, y la referencia necesaria para localizar el archivo en el almacenamiento de archivos (nunca su contenido binario). [README §2, §3] [Decisión]

**Why this priority**: Es la base de la trazabilidad; sin carga no hay seguimiento, periodos, productos ni incidencias.

**Independent Test**: Revisar manualmente que, tras registrar una carga, es posible consultar quién subió el archivo, cuándo, su estado Pendiente y la referencia al archivo, y que no se almacena el contenido del Excel.

**Acceptance Scenarios**:

1. **Given** un usuario identificado, **When** se registra la recepción de un archivo, **Then** existe una carga con `idCarga`, nombre del archivo, usuario, fecha de recepción, referencia de ubicación del archivo y estado Pendiente.
2. **Given** una carga registrada, **When** se consulta su información, **Then** no existe contenido binario del Excel entre los datos conservados.
3. **Given** una carga sin usuario responsable, **When** se intenta registrar, **Then** se rechaza el registro.

---

### User Story 2 - Admitir, rechazar o bloquear periodos por grupo (Priority: P1)

Las filas del archivo se agrupan por Periodo. Para cada grupo se decide su admisión según el resultado de las cargas previas de ese periodo; el resultado de un grupo no detiene a los demás. Ante una validación de periodo fallida termina el procesamiento de **ese grupo**, no el de los demás. [README §3] [Constitución §7] [Decisión]

**Why this priority**: Es la primera regla de negocio y condiciona qué productos pueden persistirse.

**Independent Test**: Con datos de cargas previas preparados manualmente, evaluar un archivo con varios periodos y verificar el resultado por grupo.

**Acceptance Scenarios**:

1. **Given** un archivo con los periodos 2025-01, 2025-02 y 2025-03, el primero con carga previa Finalizado, el segundo con carga previa En proceso y el tercero sin cargas previas, **When** se evalúan los grupos, **Then** 2025-01 queda rechazado, 2025-02 bloqueado y 2025-03 admitido y procesado (rechazo parcial por grupo).
2. **Given** un periodo con carga previa Cargado, Finalizado o Notificado, **When** se evalúa, **Then** todo el grupo se rechaza.
3. **Given** un periodo con carga previa Pendiente o En proceso, **When** se evalúa, **Then** todo el grupo se bloquea.
4. **Given** la carga actual ya asociada a un periodo, **When** se consultan las cargas previas de ese periodo, **Then** la propia carga no se considera previa y no se rechaza ni bloquea a sí misma.
5. **Given** un grupo rechazado o bloqueado, **When** finaliza la evaluación, **Then** ese periodo no queda reservado ni figura como admitido para esa carga, y su resultado queda conservado como resultado del periodo (no como nuevo estado de la carga).
6. **Given** dos cargas simultáneas con el mismo periodo, **When** ambas se evalúan a la vez, **Then** como máximo una queda admitida para ese periodo; la otra se bloquea o rechaza según la regla.

---

### User Story 3 - Persistir productos sin duplicar CodigoProducto (Priority: P1)

Tras superar la validación de periodo, cada fila elegible se persiste como producto aceptado si su CodigoProducto no existe; la unicidad es **global**. [README §3] [Constitución §7]

**Why this priority**: Es la segunda regla de negocio y el resultado principal de la carga.

**Independent Test**: Preparar un catálogo previo y un archivo con códigos nuevos, existentes y repetidos; verificar qué filas se aceptan y cuáles se reportan.

**Acceptance Scenarios**:

1. **Given** un CodigoProducto ya registrado en otro periodo, **When** aparece en una fila de un grupo admitido, **Then** esa fila se omite y se reporta como Existente; las demás filas continúan.
2. **Given** el mismo CodigoProducto en varias filas elegibles del archivo, **When** se procesa, **Then** se acepta la primera según el orden original del Excel y las posteriores no actualizan el precio.
3. **Given** el mismo CodigoProducto en periodos distintos del mismo archivo, **When** se procesa, **Then** rige la unicidad global: solo se conserva la primera fila elegible.
4. **Given** un producto existente, **When** llega una fila con el mismo código y otro precio, **Then** el precio del producto existente no cambia.
5. **Given** dos procesamientos concurrentes con el mismo CodigoProducto, **When** terminan, **Then** existe un único producto con ese código y la fila perdedora se reporta como Existente; una colisión de una fila nunca rechaza el grupo completo.
6. **Given** el código X en la fila 3 del periodo B y en la fila 5 del periodo A, con filas del periodo A anteriores a la 3 en el archivo, **When** se procesa, **Then** gana la fila 3 (primera en el orden original del Excel) aunque su periodo no sea el primero en procesarse.
7. **Given** los códigos "p0117" y " P0117 " (con espacios externos), **When** se comparan, **Then** representan el mismo código; el segundo se reporta Existente. Los espacios internos no se eliminan.
8. **Given** una colisión de unicidad que se repite tras los reintentos limitados, **When** se agotan, **Then** el periodo se registra como fallo técnico con su incidencia y libera su reserva (ver US5).
9. **Given** el código X ganador en la fila 3 del periodo B y duplicado en la fila 5 del periodo A, **When** el periodo B se revierte o no llega a procesarse, **Then** la fila 5 no se registra como Existente (no hay producto confirmado): se reporta como no procesada y una nueva carga la evalúa de nuevo contra los datos confirmados.
10. **Given** un precio que no cabe exactamente en DECIMAL(18,2) o un texto que supera la longitud definida, **When** se procesa la fila, **Then** se omite con su motivo, sin redondear ni truncar.

---

### User Story 4 - Distinguir valores ausentes de inválidos (Priority: P2)

Se omiten las filas totalmente vacías; se aplican valores por defecto solo a datos **ausentes** de NombreProducto y Precio; las filas con datos obligatorios ausentes o con datos **inválidos** se omiten registrando el motivo. [README §3] [Constitución §7] [Decisión]

**Why this priority**: Afecta la calidad de los datos persistidos; depende de las historias anteriores.

**Independent Test**: Evaluar filas con combinaciones de campos ausentes e inválidos y comparar con los resultados esperados.

**Acceptance Scenarios**:

1. **Given** una fila completamente vacía, **When** se procesa, **Then** no se genera producto.
2. **Given** una fila sin Periodo o sin CodigoProducto, **When** se procesa, **Then** se omite y se registra el motivo.
3. **Given** una fila sin NombreProducto con CodigoProducto "X", **When** se persiste, **Then** el nombre es "Producto X".
4. **Given** una fila sin Precio, **When** se persiste, **Then** el precio es 0.
5. **Given** una fila con Precio no numérico, **When** se procesa, **Then** se omite y se registra el motivo; no se le aplica el valor 0.
6. **Given** una fila con Periodo que no cumple AAAA-MM (p. ej. "2025-13" o "25-01"), **When** se procesa, **Then** se omite y se registra el motivo.
7. **Given** una fila con Periodo "2025-12", **When** se procesa, **Then** el periodo es válido.
8. **Given** una fila cuyo CodigoProducto contiene solo espacios, **When** se procesa, **Then** se trata como ausente: la fila se omite y se registra el motivo.
9. **Given** una fila con CodigoProducto " ab c ", **When** se persiste, **Then** el código es "ab c" (espacios externos eliminados, interno conservado).

---

### User Story 5 - Persistir cada periodo de forma atómica y tolerar fallos técnicos (Priority: P1)

Cada grupo de periodo se persiste de forma transaccional: sus productos aceptados e incidencias de negocio se confirman juntos; un fallo técnico antes de confirmar revierte ese grupo y después se registra de forma persistente la incidencia técnica. Los periodos completados se conservan aunque otro falle. [Decisión]

**Why this priority**: Evita estados parciales inconsistentes y define la disponibilidad de los periodos tras un fallo.

**Independent Test**: Simular manualmente un fallo técnico en el segundo de tres periodos y revisar qué queda confirmado.

**Acceptance Scenarios**:

1. **Given** un archivo con los periodos P1, P2 y P3 donde P1 se completó y P2 sufre un fallo técnico antes de confirmar, **When** se agotan los reintentos permitidos, **Then** P1 permanece confirmado, P2 no deja productos ni incidencias de negocio confirmados, queda una incidencia técnica persistente, P3 se registra como “No procesado por interrupción de la carga” con su reserva liberada y la carga termina Fallido con su motivo.
2. **Given** P1 completado en un archivo que terminó Fallido, **When** otra carga intenta P1, **Then** P1 se rechaza porque un periodo completado sigue impidiendo nuevas cargas.
3. **Given** P2 fallido y revertido, sin productos confirmados, y finalizada la ejecución anterior, **When** una nueva carga incluye P2, **Then** P2 puede procesarse.
4. **Given** un periodo no completado tras un fallo terminal, **When** la carga queda Fallido, **Then** su reserva se libera y el periodo no queda bloqueado indefinidamente.
5. **Given** un mensaje de procesamiento reentregado o reintentado, **When** se procesa de nuevo, **Then** no se duplican productos, no se reprocesan periodos completados y no se reporta como Existente un producto ya confirmado por esa misma carga.
6. **Given** una carga cuyo procesamiento falla por error técnico tras agotar los reintentos, **When** se consulta, **Then** queda Fallido con su motivo; una omisión o rechazo de negocio, en cambio, nunca produce Fallido.
7. **Given** cargas Pendiente esperando su mensaje, **When** el servicio de carga masiva se reinicia, **Then** siguen Pendiente y no se marcan Fallido; el reinicio no demuestra abandono.
8. **Given** una carga En proceso interrumpida por la caída del proceso, con P1 completado y P2 sin confirmar, **When** el servicio se recupera antes de consumir nuevos mensajes, **Then** conserva P1, reanuda solo los periodos no completados sin duplicar productos ni incidencias, y la carga continúa su recorrido.
9. **Given** una recuperación interrumpida por otra caída, **When** se repite, **Then** el resultado final es el mismo, sin duplicados; las caídas repetidas no producen Fallido por sí solas hasta agotar los intentos permitidos para procesar la carga.
10. **Given** dos instancias que intentan procesar a la vez, **When** ambas arrancan, **Then** solo una procesa y recupera cargas; la otra no procesa ni recupera.
11. **Given** el mensaje de una carga que ya se recuperó, **When** RabbitMQ lo reentrega, **Then** se descarta sin reprocesar ni cambiar su estado.
12. **Given** un periodo rechazado por validación de negocio con otros periodos pendientes, **When** se procesa el archivo, **Then** los demás periodos continúan; solo un fallo técnico terminal detiene el archivo.

---

### User Story 6 - Conservar incidencias e historial de estados (Priority: P2)

Se conservan de forma persistente los fallidos, existentes y demás incidencias vinculados a la carga, con su motivo, y el historial de cambios de estado. [README §3] [Constitución §8]

**Why this priority**: Es la auditoría y trazabilidad exigida; no sustituye a los logs técnicos.

**Independent Test**: Tras una carga con rechazos, bloqueos, existentes y omisiones, consultar incidencias e historial y verificar que permiten reconstruir lo ocurrido.

**Acceptance Scenarios**:

1. **Given** una fila omitida o existente, **When** finaliza el procesamiento, **Then** existe una incidencia con motivo, carga, periodo y fila o producto afectado (según la información disponible).
2. **Given** un grupo rechazado o bloqueado, **When** finaliza la evaluación, **Then** existe una incidencia de grupo, distinguible de las incidencias de fila y técnicas.
3. **Given** una carga que cambia de estado, **When** se consulta su historial, **Then** cada cambio figura con estado y momento, en orden.
4. **Given** los archivos de log técnico, **When** se necesita auditar una carga, **Then** la información persistente es suficiente sin recurrir a ellos.

---

### User Story 7 - Registrar finalización y notificación (Priority: P2)

Se conserva evidencia de la finalización del procesamiento y de la notificación completada, a nivel de carga y no de fila. Si falla solo el envío del correo, la carga conserva Finalizado y se registra la incidencia; la notificación se reintenta sin reprocesar el archivo. [README §2, §3] [Decisión]

**Why this priority**: Cierra el ciclo Finalizado → Notificado.

**Independent Test**: Revisar que una carga con omisiones de negocio queda Finalizado, que un fallo de correo no la cambia, y que solo un envío exitoso la deja Notificado.

**Acceptance Scenarios**:

1. **Given** un archivo con todos sus periodos resueltos, incluso con omisiones de negocio, **When** termina el procesamiento, **Then** recorre Pendiente → En proceso → Cargado → Finalizado y conserva su fecha de fin.
2. **Given** una carga Finalizado, **When** el envío del correo termina con éxito, **Then** queda Notificado con la evidencia de la notificación.
3. **Given** una carga Finalizado, **When** falla únicamente el envío del correo, **Then** conserva Finalizado, se registra la incidencia y la notificación puede reintentarse sin reprocesar el archivo.
4. **Given** una notificación no completada, **When** se consulta la carga, **Then** no figura como Notificado.
5. **Given** una carga en Cargado cuyo paso a Finalizado falla, **When** se recupera, **Then** conserva Cargado y se completa la finalización sin reprocesar productos confirmados.
6. **Given** una carga Notificado o Fallido, **When** se intenta cambiar su estado, **Then** no cambia (estados terminales).

---

### User Story 8 - Identidad propia y roles Administrador/Operador (Priority: P2)

La identidad es propia y persistida; existen los roles Administrador y Operador, un usuario inicial por rol para la demostración y contraseñas protegidas con hash. [Decisión]

**Why this priority**: Identifica al responsable de cada carga y fija las diferencias de acceso a la consulta.

**Independent Test**: Revisar que existen un usuario por rol, que las cargas de distintos usuarios se distinguen y que lo que cada rol puede consultar coincide con lo definido.

**Acceptance Scenarios**:

1. **Given** un Administrador, **When** consulta cargas e incidencias, **Then** accede a las de todos los usuarios y puede cargar archivos.
2. **Given** un Operador, **When** consulta cargas e incidencias, **Then** accede solo a las propias y puede cargar archivos.
3. **Given** un Operador y la carga de otro usuario, **When** intenta consultarla, **Then** no tiene acceso a ella.
4. **Given** los datos persistidos de identidad, **When** se revisan, **Then** ninguna contraseña está en texto plano ni con cifrado reversible.
5. **Given** los scripts versionados, **When** se revisan, **Then** no contienen contraseñas; las credenciales iniciales provienen de configuración externa.

---

### Edge Cases

- Archivo en el que todos los periodos son rechazados o bloqueados: la carga termina sin productos aceptados y con incidencias de grupo; no es Fallido.
- Periodo con varias cargas previas en estados distintos: un periodo completado impide la nueva carga; los grupos rechazados, bloqueados o revertidos no reservan el periodo.
- Código existente solo en una fila de un grupo posteriormente rechazado: no se persiste, el grupo no es elegible.
- Primera aparición de un código omitida por dato inválido: la primera fila *aceptada* es la que cuenta.
- Reentrega de un mensaje ya completado: no duplica productos, incidencias ni historial.
- Fallo técnico del procesamiento tras la confirmación de algunos periodos: la carga queda Fallido, pero los periodos completados se conservan.
- Fallo de correo: la carga permanece Finalizado; Fallido no aplica a la notificación.
- Precio que no puede representarse exactamente en DECIMAL(18,2), o texto que supera la longitud definida en el modelo: la fila se omite y se registra el motivo; no se redondea ni trunca en silencio. [Decisión]
- Fallo terminal de un periodo con otros periodos aún sin procesar: se detiene el archivo, la carga queda Fallido, se conservan los periodos completados y los pendientes se registran como “No procesado por interrupción de la carga” con sus reservas liberadas. Un periodo rechazado por validación de negocio no detiene los demás. [Decisión]
- Duplicado cuya fila ganadora no llegó a persistirse (periodo revertido o no procesado): no se registra Existente; la fila se reporta como no procesada y una nueva carga la evalúa de nuevo contra los datos confirmados. [Decisión]
- Archivo ilegible por formato o contenido inválido: incidencia de entrada, sin reintentos técnicos; una falla de descarga o almacenamiento es técnica. Su tratamiento operativo se define en la feature de procesamiento. [Decisión]
- Nuevo archivo con P2 mientras la ejecución anterior que lo revirtió sigue en curso: el periodo sigue ocupado hasta terminar esa ejecución (el mecanismo lo define el plan).

## Requirements *(mandatory)*

### Functional Requirements

**Identidad, usuarios y roles**

- **FR-001**: El sistema MUST conservar usuarios, roles y su asociación en una identidad propia persistida, sin proveedores externos de identidad. [Decisión]
- **FR-002**: Los roles MUST ser Administrador y Operador. El Administrador carga archivos y consulta todas las cargas y sus incidencias; el Operador carga archivos y consulta solo sus propias cargas e incidencias. [Decisión]
- **FR-003**: El sistema MUST NOT incorporar un motor genérico de permisos. [Decisión]
- **FR-004**: Las contraseñas MUST almacenarse con un hash apropiado para contraseñas, nunca en texto plano ni con cifrado reversible. El hash lo genera ms-autenticacion con `PasswordHasher` de ASP.NET Core Identity, sin implementar PBKDF2 manualmente; esta feature solo define su almacenamiento. [Decisión]
- **FR-005**: MUST existir un usuario inicial por rol para la demostración, con credenciales suministradas por configuración externa y sin contraseñas en scripts versionados. [Decisión]

**Carga y archivo**

- **FR-006**: El sistema MUST registrar cada carga con `idCarga`, nombre del archivo, usuario responsable, fecha de recepción y estado actual. [README §3, §5] [Decisión]
- **FR-007**: El sistema MUST conservar la referencia necesaria para localizar el archivo en el almacenamiento y MUST NOT guardar el contenido binario del Excel. [Decisión]
- **FR-008**: El sistema MUST asociar a la carga los periodos del archivo, conservando por cada uno su resultado (admitido/completado, rechazado, bloqueado, fallido o no procesado). Ese resultado MUST distinguirse del estado de la carga y MUST NOT convertirse automáticamente en estado de la carga. [Decisión] [Constitución §8]

**Reglas por periodo**

- **FR-009**: El sistema MUST permitir consultar las cargas previas de un periodo, excluyendo la carga actual. [Decisión]
- **FR-010**: Si el periodo tiene una carga previa Cargado, Finalizado o Notificado, el sistema MUST rechazar todo el grupo. [README §3]
- **FR-011**: Si el periodo tiene una carga previa Pendiente o En proceso, el sistema MUST bloquear todo el grupo. [README §3]
- **FR-012**: Rechazar o bloquear un grupo termina el procesamiento de ese grupo y MUST NOT detener los demás. [Constitución §7] [Decisión]
- **FR-013**: Los grupos rechazados o bloqueados MUST NOT reservar el periodo ni registrarse como carga admitida de él. [Decisión]
- **FR-014**: La disponibilidad de un periodo MUST NOT determinarse únicamente por el estado general del archivo, sino por el resultado de cada periodo. Un periodo completado MUST seguir impidiendo nuevas cargas aunque el archivo termine Fallido; un periodo fallido y revertido MAY procesarse en una nueva carga una vez terminada la ejecución anterior; un fallo MUST NOT bloquear indefinidamente un periodo no completado. [Decisión]
- **FR-015**: La regla de periodo MUST mantenerse ante solicitudes concurrentes: dos cargas simultáneas del mismo periodo no pueden quedar ambas admitidas. El plan define el mecanismo mínimo. [Decisión]

**Persistencia atómica**

- **FR-016**: Cada grupo de periodo MUST persistirse de forma transaccional: productos aceptados e incidencias de negocio se confirman juntos. [Decisión]
- **FR-017**: Un fallo técnico antes de confirmar MUST revertir la transacción del grupo y, tras revertir, MUST registrarse de forma persistente la incidencia técnica. [Decisión]
- **FR-018**: Los periodos completados MUST conservarse aunque otro periodo falle. [Decisión]

**Reglas por producto**

- **FR-019**: La validación por CodigoProducto MUST ejecutarse solo después de admitir el periodo. [Constitución §7]
- **FR-020**: La unicidad de CodigoProducto MUST ser global; si el código existe, aun en otro periodo, la fila se omite y se reporta como Existente. MUST NOT usarse unicidad compuesta Periodo + CodigoProducto. [README §3] [Constitución §7]
- **FR-021**: Entre filas elegibles con el mismo código se MUST conservar la primera aceptada según el orden original; MUST NOT actualizarse el precio ni sobrescribirse un producto existente. [Constitución §7]
- **FR-022**: La unicidad MUST mantenerse ante procesamiento concurrente y ante reintento o reentrega. Repetir una operación ya confirmada de la misma carga MUST NOT duplicar productos, reprocesar periodos completados ni registrar como Existente un producto confirmado por esa misma carga. [Decisión]
- **FR-023**: Cada producto aceptado MUST conservar su asociación con la carga y el periodo con que se aceptó. [Constitución §8]

**Filas y valores**

- **FR-024**: Las filas completamente vacías MUST omitirse sin generar producto. [README §3]
- **FR-025**: Si falta Periodo o CodigoProducto, la fila MUST omitirse y registrarse el motivo. [Constitución §7]
- **FR-026**: Si falta NombreProducto se MUST usar "Producto " + CodigoProducto (ya limpio de espacios externos); si falta Precio, 0. [README §3] [Constitución §7]
- **FR-027**: Un Precio no numérico o un Periodo inválido MUST omitir la fila y registrar el motivo. Un Periodo válido tiene formato AAAA-MM, con año de cuatro dígitos y mes entre 01 y 12. No se aplican valores por defecto a datos inválidos. [Decisión]
- **FR-028**: No se imponen restricciones sobre precios negativos ni patrones de CodigoProducto, y ningún límite se deduce del Excel de ejemplo. Las longitudes físicas de los textos son propuestas del plan sujetas a aprobación (ver FR-047). [Decisión] [Constitución §7]

**Estados, auditoría y notificación**

- **FR-029**: La carga MUST usar únicamente los estados Pendiente, En proceso, Cargado, Finalizado, Notificado y Fallido (este último es decisión del usuario, no del README), con el recorrido satisfactorio Pendiente → En proceso → Cargado → Finalizado → Notificado. Transiciones permitidas: ese recorrido y, ante un fallo técnico terminal, Pendiente → Fallido y En proceso → Fallido. Notificado y Fallido son terminales. [README §2] [Decisión]
- **FR-030**: Significado de los estados: Pendiente = archivo registrado, esperando procesamiento; En proceso = lectura, validación y procesamiento del contenido; Cargado = productos aceptados e incidencias persistidos, resueltos todos los grupos; Finalizado = procesamiento completo y resultado preparado para notificar; Notificado = correo enviado satisfactoriamente; Fallido = procesamiento interrumpido por error técnico tras agotar los reintentos permitidos. [Decisión]
- **FR-031**: Una omisión o rechazo por reglas de negocio MUST NOT producir Fallido. Un error transitorio recuperable MUST NOT cambiar el estado; un reinicio o caída del proceso MUST NOT marcar Fallido por sí solo. Al agotarse los reintentos de procesamiento, el sistema MUST registrar Fallido y su motivo. [Decisión]
- **FR-032**: Los reintentos técnicos MUST ser limitados; cantidad, intervalos, errores reintentables y operaciones protegidas se definen en las features de Control, Carga Masiva y Notificaciones. No se añade interfaz ni endpoint de reintentos. [Decisión]
- **FR-033**: Los fallidos, existentes y demás incidencias MUST persistirse vinculados a la carga, con motivo e información disponible de periodo y fila o producto. [README §3] [Constitución §8]
- **FR-034**: Debe poder distinguirse una incidencia de grupo, una de fila y una técnica entre sí y respecto del estado de la carga. [Decisión]
- **FR-035**: El sistema MUST conservar el historial de cambios de estado de la carga. [Constitución §8]
- **FR-036**: El sistema MUST conservar la finalización del procesamiento de la carga (con su fecha) aunque existan omisiones de negocio. [README §3]
- **FR-037**: Notificado MUST registrarse solo tras el envío exitoso, y la notificación corresponde a la carga, no a cada fila. Si falla únicamente el correo, la carga conserva Finalizado, se registra la incidencia y la notificación se reintenta sin reprocesar el archivo. [README §3] [Decisión]
- **FR-038**: La información de auditoría MUST ser persistente y no depender de archivos de log. [Constitución §10]
- **FR-039**: La especificación MUST NOT imponer una o varias tablas para auditoría y trazabilidad; el plan decide el modelo mínimo. [Constitución §8]
- **FR-040**: Cada transición de estado aplicada MUST conservarse en el historial; repetir una transición ya aplicada MUST NOT duplicarlo. [Decisión]
- **FR-041**: Si falla el paso Cargado → Finalizado, la carga MUST conservar Cargado y la finalización MUST recuperarse sin reprocesar productos confirmados. [Decisión]
- **FR-042**: Debe existir un único procesador activo de cargas, con exclusividad garantizada por un mecanismo (no solo por configurar una réplica); procesa mensajes y grupos de periodo de forma secuencial. [Decisión]
- **FR-043**: Las ejecuciones interrumpidas MUST recuperarse antes de consumir nuevos mensajes, conservando los periodos completados; una carga Pendiente que espera su mensaje no se considera interrumpida. [Decisión]
- **FR-044**: CodigoProducto se compara sin distinguir mayúsculas, tras eliminar los blancos iniciales y finales con `Trim()` de .NET (se conservan los internos) y antes de comprobar longitud y unicidad, sin introducir otras equivalencias (p. ej. acentos); un código vacío tras la limpieza se trata como ausente; no se eliminan espacios internos ni se aplican otras transformaciones. El criterio MUST ser el mismo en la validación de aplicación y en la restricción de la base de datos. [Decisión]
- **FR-045**: El orden original del Excel MUST determinar qué fila gana entre duplicados elegibles aunque estén en periodos distintos e intercalados, sin incumplir la atomicidad por periodo. [Decisión]
- **FR-046**: Una colisión de unicidad de CodigoProducto MUST resolverse como fila Existente tras reevaluar, sin rechazar el grupo; si persiste tras los reintentos limitados, MUST registrarse como fallo técnico del periodo con su incidencia y liberar su reserva. [Decisión]
- **FR-047**: Precio se almacena como DECIMAL(18,2). Si el precio no puede representarse exactamente en DECIMAL(18,2), o un texto supera la longitud definida en el modelo, la fila MUST omitirse y registrarse el motivo; no se aceptan redondeos ni truncamientos silenciosos. [Decisión]
- **FR-048**: Ante un fallo técnico terminal de un periodo, el sistema MUST detener el procesamiento del archivo y dejar la carga Fallido, conservar los periodos completados, registrar los periodos pendientes como “No procesado por interrupción de la carga” (sin afirmar que fallaron) y liberar sus reservas. [Decisión]
- **FR-049**: Una fila MUST registrarse como Existente solo si el producto que la origina está confirmado; si el periodo de la fila ganadora se revierte o no llega a procesarse, sus resultados provisionales se revierten y las filas afectadas se reportan como no procesadas. Una nueva carga las evalúa de nuevo contra los datos confirmados. [Decisión]

### Key Entities *(include if feature involves data)*

Descripción conceptual; no implica una tabla por concepto ni fija columnas, tipos o cardinalidades.

- **Usuario**: identidad propia; puede ser responsable de cargas; guarda credencial protegida con hash.
- **Rol**: Administrador u Operador; se asocia con usuarios.
- **Carga del archivo**: recepción de un Excel; identificada por `idCarga`; conoce al responsable, fecha de recepción, referencia del archivo, estado actual y fecha de fin.
- **Periodo de la carga**: periodo contenido en el archivo, con su propio resultado, distinto del estado de la carga.
- **Producto aceptado**: producto persistido con CodigoProducto único globalmente, nombre y precio; conserva su carga y periodo de origen.
- **Incidencia de procesamiento**: hecho de grupo, de fila o técnico (rechazo, bloqueo, existente, omisión, fallo, fallo de correo) con motivo, vinculado a la carga y, si aplica, a periodo y fila o producto.
- **Historial de estados**: registro ordenado de cambios de estado de la carga, incluida la evidencia de finalización y notificación.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Para el 100% de los escenarios de aceptación de las historias 2, 3 y 4, el resultado por grupo y por fila coincide con el esperado al revisarse manualmente.
- **SC-002**: Tras cualquier carga, el 100% de las filas omitidas, existentes y de los grupos rechazados, bloqueados o fallidos tiene una incidencia persistente con motivo, consultable sin usar logs.
- **SC-003**: En una prueba manual con cargas concurrentes o mensajes reentregados, existe como máximo un producto por CodigoProducto y como máximo una carga admitida por periodo, sin productos ni incidencias duplicados.
- **SC-004**: Tras un fallo técnico en un periodo, el 100% de los periodos completados se conserva y el periodo revertido no deja datos confirmados.
- **SC-005**: El historial de una carga permite reconstruir cada cambio de estado con su momento, sin estados fuera de los seis aprobados.
- **SC-006**: El 100% de las cargas identifica a su usuario responsable y la fecha de recepción; un Operador solo accede a sus propias cargas.
- **SC-007**: Ningún dato conservado contiene el contenido binario completo del Excel ni contraseñas reversibles.

## Assumptions

- Las reglas de negocio provienen del README y de la constitución vigente; las precisiones de estados, fallos, identidad y valores inválidos son decisiones del usuario y no del README.
- Se mantiene el modelo mínimo; no se crean estructuras preventivas para funcionalidades no autorizadas.
- Las restricciones técnicas acordadas (SQL Server, EF Core y LINQ, scripts SQL versionados, Polly y RabbitMQ) se aplican en el plan, no aquí. [Constitución §6, §9]
- Los escenarios se verifican manualmente; no se crean pruebas automatizadas. [Constitución §11]
- El Excel de ejemplo solo ilustra el formato y la presencia de varios periodos; no define límites ni validaciones.
- Los parámetros de reintento (cantidad, intervalos) se fijan en las features de Control, Carga Masiva y Notificaciones; esta feature solo define límites transaccionales, operaciones repetibles y resultados persistentes.
- Fuera de alcance: login, JWT, endpoints, administración de usuarios, consumidores de colas, lectura del Excel, SeaweedFS, envío de correo y React.

## Asuntos pendientes (no son requisitos aprobados)

- Tratamiento operativo de un archivo ilegible (formato o contenido inválido: incidencia de entrada sin reintentos técnicos; descarga o almacenamiento: técnica): se define en la feature de procesamiento.
- Recuperación de notificaciones no publicadas: se resuelve en la feature del servicio que publica el mensaje; no se implementa en `database/`.
- Verificación operativa de Docker y SQL Server en el equipo donde se ejecutará (ver `research.md` R-11); no bloquea la creación de la base.
- Modelo físico, índices, procedimientos y scripts: ver `plan.md` y `data-model.md`.
