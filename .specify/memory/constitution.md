<!--
Sync Impact Report
- Versión: 3.5.0 → 3.6.0 (MINOR: fundación del frontend opcional por decisión del usuario,
  2026-11-03, con autorización expresa de enmienda.) Principios modificados: §11 (el cliente web
  React del README, opcional, se implementa con React, TypeScript y Vite; React Router, Axios con
  interceptores y Tailwind CSS; responsabilidades en pages, components, models, api, hooks y
  contexto de autenticación; consume solo el Gateway; sesión y renovación según el contrato de
  Autenticación; Postman se conserva), Pendientes (se retira "React"; se añaden la ubicación del
  frontend en el monorepo, su despliegue y el acceso del navegador al Gateway). Distinción: el
  README deja el frontend como opcional valorado (React 16+, componentes; en su defecto, colecciones
  Postman); TypeScript, Vite, React Router, Axios, Tailwind CSS y la separación de
  responsabilidades son decisiones del usuario. Sin cambios en §2 ni §9: sus efectos quedan como
  pendientes. Documentos afectados: ninguno de las features 001–010 (el frontend consume los
  contratos ya aprobados de las features 008, 009 y 010); plantillas sin cambios. Resto sin cambios;
  la fecha de ratificación sigue pendiente.
- Historial 3.5.0 (referencia): Versión 3.4.0 → 3.5.0 (MINOR: decisiones del usuario de la Feature 008, autenticación,
  2026-11-03, con autorización expresa de enmienda.) Principios modificados: §9 (Autenticación:
  refresh tokens incluidos con `POST /auth/refresh`; token de acceso con vigencia de 60 minutos
  predeterminada y configurable, HS256 con JWT_CLAVE/JWT_EMISOR/JWT_AUDIENCIA; credenciales
  inválidas → 401 genérico con Response<T> sin revelar si el usuario existe; refresh token con
  vigencia de 1 hora configurable, persistido solo como hash en la base mediante script versionado
  y rotado en cada renovación), Pendientes (se retira "Refresh tokens ... no está decidido"). Distinción: el README exige `/auth/login` con JWT
  Bearer y deja `/auth/refresh` como opcional valorado; lo demás son decisiones del usuario.
  Plantillas: sin cambios. Resto sin cambios; la fecha de ratificación sigue pendiente.
- Historial 3.4.0 (referencia): Versión 3.3.0 → 3.4.0 (MINOR: se añade a §11 Mailpit como servidor SMTP local de pruebas y
  demostración en Compose, por decisión del usuario en la Feature 007, 2026-11-03; no es requisito del
  README. Captura correos sin entregarlos a buzones externos; MailKit y la configuración SMTP por
  variables de entorno se mantienen.) Resto sin cambios; la fecha de ratificación sigue pendiente.
- Historial 3.3.0 (referencia): Versión 3.2.1 → 3.3.0 (MINOR: decisiones del usuario de la Feature 007, notificación por correo,
  2026-11-03.) Principios modificados: §8 (contenido del correo; reintentos limitados del envío y su
  agotamiento; correo enviado sin Notificado persistido, con posible duplicado documentado),
  Pendientes (se retira "contenido exacto del correo"). Distinción: el README exige enviar el correo
  al usuario indicando que la carga finalizó y marcar Notificado; lo demás son precisiones
  operativas del usuario. Resto sin cambios; la fecha de ratificación sigue pendiente.
- Historial 3.2.1 (referencia): Versión 3.2.0 → 3.2.1 (PATCH: aclaración de §7 sobre la lectura del precio, decisiones H1 y H2 del
  usuario en el cierre de la Feature 006, 2026-11-03: celdas numéricas por su valor con la
  representación decimal más corta y comprobación exacta en DECIMAL(18,2); celdas de texto con punto
  decimal y sin separadores de miles. Sustituye la interpretación propuesta en F001 R-08.) Resto sin
  cambios.
- Historial 3.2.0 (referencia): Versión 3.1.0 → 3.2.0 (MINOR: precisiones de la Feature 006, procesamiento en ms-carga-masiva,
  autorizadas por el usuario el 2026-11-03.)
- Principios modificados en 3.2.0: §7 (archivo inválido → incidencia ArchivoInvalido, sin productos
  ni reintentos, recorrido normal hasta Finalizado y notificación; archivo inexistente = fallo
  técnico de recuperación), §8 (notificación no confirmada → Finalizado + ErrorTecnico, sin
  recuperación automática; limitación aceptada), Pendientes (se retiran: tratamiento operativo de
  archivos ilegibles y recuperación de mensajes no publicados en `notificaciones`). El nuevo tipo
  de incidencia se añade al esquema con un script versionado mínimo (§6, sin cambio de la regla).
  Resto sin cambios. Fecha de ratificación: sigue pendiente.
- Historial 3.1.0 (referencia): Versión 3.0.0 → 3.1.0 (MINOR: se resuelven pendientes y se añaden precisiones para la Feature 005,
  recepción y registro de cargas en ms-control, por decisión del usuario del 2026-11-02.)
- Principios modificados en 3.1.0: §5 (Data sin resultado = null incluido en el JSON; camelCase),
  §9 (Control valida también el JWT; contrato mínimo de claims `sub` + `role`; tamaño máximo de
  archivo predeterminado de 10 MiB configurable; Rate Limiting en Control para el endpoint de carga;
  fallo definitivo de publicación en `carga_masiva` → Fallido), Pendientes (se retiran: Data sin
  resultado y serialización; límites de tamaño de archivo; ubicación del Rate Limiting; recuperación
  de mensajes no publicados en `carga_masiva`). Resto sin cambios.
- Plantillas: sin cambios necesarios. Fecha de ratificación: sigue pendiente.
- Historial 3.0.0 (referencia): Versión 2.1.0 → 3.0.0 (MAJOR: se invierte la regla vigente de 2.0.0/2.1.0 de §6 que asignaba
  las EF Core Migrations a ms-control con aplicación automática al iniciar. Pasa a regir una única
  base SQL Server compartida cuyo esquema y procedimientos se gestionan con scripts SQL versionados
  en `database/`, aplicados por un runner ejecutado desde Docker Compose; los microservicios usan
  EF Core + LINQ solo para acceso a datos, sin generar ni aplicar migraciones. Los modelos se
  obtienen por ingeniería inversa de las tablas que cada servicio necesita.)
- Principios modificados en 3.0.0: §2 (`database/` vuelve a ser componente activo), §6 (gestión del
  esquema, procedimientos, modelos por servicio), §11 (orden de arranque en Compose), Pendientes
  (se sustituye el pendiente de arranque de Control con migraciones), Governance (versión del pie).
  Resto sin cambios; la fecha de ratificación sigue pendiente. Corrige la discrepancia previa
  (reporte 2.1.0 frente a pie 2.0.0).
- Historial (ya no vigente): las entradas de 2.1.0 y 2.0.0 que siguen describen decisiones previas;
  la regla de migraciones EF Core de ms-control de 2.0.0 queda superada por 3.0.0.
- Versión 2.1.0 (referencia): (MINOR: se resuelve el pendiente Api/Worker: los cuatro microservicios usan
  el host `Acity.Api` (ASP.NET Core Web API con controladores) y los consumidores RabbitMQ corren
  como `BackgroundService` dentro de ese host; no existen proyectos `Acity.Worker`)
- Principios modificados en 2.1.0: §3 (host), §4 (ejecutables), Pendientes (se retira Api/Worker).
  Resto sin cambios; la fecha de ratificación sigue pendiente.
- Cambios 2.0.0 (referencia): (MAJOR: se invierte la regla de §6 que prohibía EF Core Migrations y
  exigía scripts SQL versionados con runner; el esquema pasa a migraciones EF Core administradas
  desde ms-control, con propietario único y aplicación automática al iniciar)
- Principios modificados en 2.0.0: §2 (descripción de `database/`), §6 (gestión del esquema),
  Pendientes (arranque por migraciones pendiente de la feature de ms-control). Resto sin cambios;
  la fecha de ratificación sigue pendiente.
- Cambios 1.3.0 (referencia): tratamiento de precisión y longitudes, fallo terminal de un
  periodo, duplicados con ganador revertido, Trim de CodigoProducto, hash en autenticación.
- Cambios 1.2.0 (referencia): normalización de CodigoProducto, matriz de transiciones,
  consumidor único con recuperación, responsabilidades de Polly y de usuarios iniciales.
- Principios modificados en 1.3.0: §6 (hash con PasswordHasher de ASP.NET Core Identity), §7
  (omisión por precisión/longitud, fallo terminal, duplicados), Pendientes (resueltos retirados).
- Principios modificados en 1.2.0: §6 (usuarios iniciales: responsable y repetibilidad), §7
  (CodigoProducto sin distinguir mayúsculas y sin espacios externos; precisión del precio y
  longitudes como pendientes de aprobación), §8 (matriz de transiciones, recuperación de
  Cargado → Finalizado), §9 (consumidor único, procesamiento secuencial, recuperación antes de
  consumir; parámetros de Polly por feature)
- Cambios de versiones anteriores (referencia): 1.1.1 nombres de directorios; 1.1.0 estado
  Fallido, atomicidad por periodo, identidad y roles, valores inválidos.
- Secciones añadidas: ninguna; "Pendientes y no autorizado" actualizada (resueltos retirados)
- Secciones eliminadas: ninguna
- Diferidos: fecha de ratificación (pendiente de aprobación del usuario).
- Procedencia: [README §n] = sección del README del examen (referencias/README.md);
  [Decisión] = decisión explícita del prompt de constitución del usuario.
-->
# carga-masiva Constitution

## Principios rectores del proyecto

### 1. Fuentes y control del alcance
- El README establece los requisitos del examen; las decisiones del usuario aclaran sus
  ambigüedades y delimitan la solución. [README] [Decisión]
- Todo artefacto MUST distinguir: requisito del README, decisión del usuario y asunto pendiente.
- No se añaden funcionalidades, paquetes, patrones, entidades, validaciones ni endpoints por
  costumbre. Una propuesta no equivale a aprobación. [Decisión]
- Los repositorios de referencia (Clean Architecture y API Gateway) solo orientan la estructura;
  no se copian íntegramente. [Decisión]
- Ante un conflicto no resuelto, MUST documentarse y consultarse al usuario antes de desarrollar
  contenido dependiente. Los pendientes permanecen pendientes. [Decisión]

### 2. Organización del proyecto
- Monorepo bajo `carga-masiva/`, con un único Git y un único Spec Kit en la raíz. [Decisión]
- Una constitución común; especificaciones, planes y tareas acotados por componente. [Decisión]
- Directorios contenedores en la raíz: `api-gateway/` (API Gateway con Ocelot), `database/`
  (scripts SQL versionados y runner de evolución del esquema; componente activo), `ms-autenticacion/` (autenticación y emisión de JWT),
  `ms-control/` (recepción de archivos y seguimiento de cargas), `ms-carga-masiva/`
  (procesamiento masivo del Excel) y `ms-notificaciones/` (envío de correos). [Decisión]
- Estos nombres identifican los directorios contenedores; los proyectos internos mantienen los
  nombres del principio 4. Las capas se crean solo cuando un plan lo requiera. [Decisión]
- La base de datos participa en el mismo SDD y se modela inicialmente de forma integral.
  [Decisión]
- MUST NOT inicializarse Git ni Spec Kit dentro de cada componente. [Decisión]

### 3. Arquitectura y plataforma
- .NET 10 para los cuatro microservicios y el Gateway; esta decisión resuelve las menciones a
  .NET 8/9 del README (§4) y del diagrama. [README §2 y §4] [Decisión]
- Clean Architecture, SOLID, separación de responsabilidades y CQRS con MediatR. [README §4]
  [Decisión]
- MediatR fijado en 12.5.0 (Apache-2.0); MUST NOT actualizarse automáticamente a una versión con
  otro esquema de licencia. [Decisión]
- Repository y Unit of Work según la referencia, implementando solo las operaciones necesarias.
  [Decisión]
- Entradas HTTP con ASP.NET Core MVC y controladores. No se crean endpoints sin un requisito.
  [Decisión]
- Los cuatro microservicios usan el host `Acity.Api`, ASP.NET Core Web API con controladores,
  siguiendo la arquitectura de referencia. Los consumidores RabbitMQ se ejecutan mediante
  `BackgroundService` dentro de ese host; MUST NOT crearse proyectos `Acity.Worker`. [Decisión]
- Los ejecutables de cada microservicio son independientes. [README §4] [Decisión]

### 4. Capas y convenciones
- Proyectos por microservicio: Acity.Api (host de los cuatro servicios), Acity.Application,
  Acity.Domain, Acity.Infrastructure y Acity.Common. MUST NOT usarse Servicio.Api, Servicio.Domain ni
  equivalentes. [Decisión]
- No se crean capas ni carpetas vacías para aparentar arquitectura completa. [Decisión]
- Application: casos de uso, Commands/Queries, handlers, DTOs, validadores, interfaces y mapeos.
  Domain: entidades y reglas de dominio. Infrastructure: persistencia e integraciones externas.
  [Decisión]
- DTOs de solicitud y respuesta como `record`; entidades e implementaciones como clases; contratos
  como interfaces cuando correspondan. [Decisión]
- Mapeos manuales mínimos: sin AutoMapper ni motor genérico de mapeo. [Decisión]
- FluentValidation para las validaciones autorizadas. MUST NOT inferirse límites, campos
  obligatorios ni reglas funcionales a partir de capacidades de una biblioteca. [Decisión]

### 5. Contrato de respuesta y errores
- `Response<T>` como `record` genérico en `Acity.Common/Helpers`, con los campos Data, IsSuccess,
  Message, Code y Errors (colección de mensajes opcional). [Decisión]
- `Code` representa el estado HTTP y MUST coincidir con el estado HTTP real. [Decisión]
- Serialización JSON en camelCase con los cinco campos siempre presentes; cuando no hay
  resultado, Data se serializa como `null`. [Decisión]
- Manejo global de excepciones, sin Reec. [README §4] [Decisión]
- MUST NOT existir dos formatos de error incompatibles ni campos añadidos al contrato sin
  autorización. [Decisión]

### 6. Persistencia
- SQL Server; EF Core y LINQ en Infrastructure; sin Dapper. [README §4 y §5] [Decisión]
- Una única base SQL Server compartida. El esquema y los procedimientos se gestionan con scripts
  SQL versionados en `database/`, aplicados por un runner ejecutado desde Docker Compose, que
  aplica las versiones pendientes y registra las aplicadas correctamente. [README Base de datos:
  migraciones automáticas y scripts de base de datos] [Decisión]
- Ningún microservicio crea ni modifica el esquema al arrancar, ni genera o aplica EF Core
  Migrations. [Decisión]
- EF Core y LINQ se usan para acceso a datos y mapeo. Los modelos se obtienen por ingeniería
  inversa de las tablas que cada microservicio necesita, respetando las capas; es una operación de
  desarrollo, no de arranque. [Decisión]
- Cada servicio incorpora únicamente las entidades y mapeos que necesita funcionalmente; no se
  genera CRUD ni lógica adicional, y no se incorpora el modelo completo en Control por razones de
  despliegue. El código generado se revisa e integra respetando las capas, sin sobrescribir
  personalizaciones indiscriminadamente. [Decisión]
- Modelado inicial integral del esquema en los scripts; los scripts de `referencias/` sirven de
  referencia. [Decisión]
- Procedimientos almacenados conforme al README; sus responsabilidades se delimitan en el plan.
  Se conservan y se despliegan mediante los scripts versionados. [README §4] [Decisión]
- Identidad propia persistida en SQL Server; sin proveedores externos de identidad. Se persisten
  usuarios, roles y su asociación, sin motor genérico de permisos. [Decisión]
- Roles: Administrador (carga archivos; consulta todas las cargas y sus incidencias) y Operador
  (carga archivos; consulta solo sus propias cargas e incidencias). [Decisión]
- Las contraseñas se almacenan con hash apropiado para contraseñas, nunca en texto plano ni con
  cifrado reversible. El hash lo genera ms-autenticacion con `PasswordHasher` de ASP.NET Core
  Identity, sin implementar PBKDF2 manualmente; la base solo define su almacenamiento. [Decisión]
- Un usuario inicial por rol para la demostración, con credenciales suministradas por
  configuración externa; MUST NOT haber contraseñas en scripts ni migraciones versionados. Su creación al
  arrancar corresponde a ms-autenticacion: repetible sin duplicar usuarios y sin sobrescribir
  contraseñas existentes. [Decisión]
- En la etapa de constitución MUST NOT crearse tablas, columnas, índices ni procedimientos
  definitivos. [Decisión]

### 7. Reglas funcionales de carga
- Los campos del Excel son Periodo, CodigoProducto, NombreProducto y Precio. [Decisión]
- Un archivo puede contener varios periodos; sus filas se agrupan internamente por periodo.
  [Decisión]
- Periodo con carga previa Cargado, Finalizado o Notificado: se rechaza el grupo. Con carga previa
  Pendiente o En proceso: se bloquea el grupo. [README §3] [Decisión]
- El rechazo o bloqueo de un grupo no detiene los demás periodos del archivo. La carga actual
  MUST distinguirse de las previas para no rechazarse contra sí misma. [Decisión]
- Tras validar el periodo se evalúa duplicidad global por CodigoProducto: si el código ya existe,
  incluso en otro periodo, la fila se omite y se reporta como Existente. [README §3] [Decisión]
- MUST NOT usarse Periodo + CodigoProducto como criterio de duplicidad ni llave única compuesta.
  [Decisión]
- CodigoProducto se compara sin distinguir mayúsculas de minúsculas (p0117 = P0117), tras eliminar
  los espacios al inicio y al final (`Trim()` de .NET); un código vacío tras esa limpieza se trata
  como ausente. La normalización precede a la comprobación de longitud y de unicidad. No se
  eliminan espacios internos ni se aplican otras transformaciones (tampoco equivalencias de
  acentos). El mismo criterio rige en
  las validaciones de aplicación y en la restricción de la base de datos. [Decisión]
- Entre duplicados elegibles del archivo se conserva la primera fila aceptada en el orden
  original; las posteriores no actualizan el precio. [Decisión]
- Filas completamente vacías se omiten. [README §3]
- Falta Periodo o CodigoProducto: se omite la fila y se registra el motivo (excepción autorizada
  a los valores por defecto). Falta NombreProducto: “Producto ” + CodigoProducto. Falta Precio: 0.
  [README §3] [Decisión]
- Ausente ≠ inválido: Precio no numérico o Periodo inválido omiten la fila y registran el motivo;
  no se aplican valores por defecto para ocultar datos inválidos. Periodo válido: AAAA-MM (año de
  cuatro dígitos, mes 01–12). No se inventan restricciones sobre precios negativos ni patrones de
  CodigoProducto. Precio se almacena como DECIMAL(18,2) [Decisión]. Si el precio no puede
  representarse exactamente en DECIMAL(18,2), o un texto supera la longitud definida en el
  modelo, la fila se omite y se registra el motivo; nunca se redondea ni se trunca en silencio.
  [Decisión]
- Lectura del precio [Decisión, Feature 006, 2026-11-03]: una celda numérica se evalúa por su valor
  numérico leído, no por su formato visual; se convierte a decimal con la representación decimal más
  corta que preserva ese valor y después se comprueba su representación exacta en DECIMAL(18,2), sin
  redondear ni truncar para hacerla encajar. Una celda de texto solo es numérica con punto como
  separador decimal y sin separadores de miles ("1,5" se omite con su incidencia; nunca es 15). Esta
  precisión no autoriza redondeos generales ni añade otras restricciones.
- Ante una validación de periodo fallida termina el procesamiento de ese grupo, no el de los
  demás. [README §3] [Decisión]
- Ante un fallo técnico terminal de un periodo se detiene el procesamiento del archivo y la carga
  queda Fallido: se conservan los periodos completados, los pendientes se registran como “No
  procesado por interrupción de la carga” (sin afirmar que fallaron) y se liberan sus reservas.
  Esto no altera el rechazo de negocio, que no detiene los demás periodos. [Decisión]
- Archivo inválido (corrupto, sin hojas o sin los encabezados requeridos): se registra la
  incidencia `ArchivoInvalido`, sin productos ni reintentos técnicos; la carga sigue las
  transiciones aprobadas En proceso → Cargado → Finalizado (finalización sin productos autorizada
  para este caso) y se notifica. Un archivo inexistente en el almacenamiento no es contenido
  inválido: es un fallo técnico de recuperación del archivo (`ErrorTecnico`, Fallido); su ausencia
  confirmada no se reintenta, a diferencia de los errores transitorios de acceso. [Decisión,
  Feature 006]
- Existente requiere un producto confirmado: no se registra si la fila original no llegó a
  persistirse. Si se revierte un periodo, sus resultados provisionales se revierten; las filas no
  procesadas se reportan como no procesadas y en una nueva carga se evalúan de nuevo contra los
  datos confirmados. [Decisión]
- Cada grupo de periodo se persiste de forma transaccional: productos aceptados e incidencias de
  negocio se confirman juntos; un fallo técnico antes de confirmar revierte el grupo y después se
  registra de forma persistente la incidencia técnica. Los periodos completados se conservan
  aunque otro falle. El plan define la implementación y el mínimo mecanismo de concurrencia, sin
  infraestructura adicional innecesaria. [Decisión]
- La disponibilidad de un periodo MUST NOT deducirse solo del estado general del archivo: el
  resultado de cada periodo se distingue del estado de la carga. Un periodo completado impide
  nuevas cargas aunque el archivo termine Fallido; un periodo revertido puede procesarse en una
  nueva carga al terminar la ejecución anterior; grupos rechazados o bloqueados no reservan el
  periodo; un fallo no bloquea indefinidamente un periodo no completado. [Decisión]
- MUST NOT deducirse límites de volumen ni validaciones nuevas del archivo de ejemplo.
- Detalle y criterios de aceptación corresponden a las especificaciones.

### 8. Seguimiento y notificaciones
- `idCarga` identifica la carga del archivo; se conserva la asociación de los periodos aceptados
  con su carga. [README §3] [Decisión]
- Se registra quién subió el archivo y cuándo. Los fallidos e incidencias se almacenan de forma
  persistente, vinculados a la carga; el plan define las tablas necesarias para auditoría y
  trazabilidad. [README §3] [Decisión]
- Las incidencias por fila o periodo MUST diferenciarse de los estados del archivo. Estados del
  archivo: Pendiente, En proceso, Cargado, Finalizado, Notificado [README §2] y Fallido
  [Decisión]; no se inventan otros. Recorrido satisfactorio: Pendiente → En proceso → Cargado →
  Finalizado → Notificado. [Decisión]
- Transiciones permitidas: Pendiente → En proceso; En proceso → Cargado; Cargado → Finalizado;
  Finalizado → Notificado; Pendiente o En proceso → Fallido ante un fallo técnico terminal.
  Notificado y Fallido son terminales; Fallido no se origina desde Cargado ni Finalizado. Cada
  transición aplicada se conserva en el historial. Un error transitorio recuperable no cambia
  el estado; no se marca Fallido por cualquier caída del proceso ni por un reinicio. [Decisión]
- Si falla el paso Cargado → Finalizado, la carga conserva Cargado y se recupera la finalización
  sin reprocesar productos confirmados. [Decisión]
- Significado: Pendiente = registrado, esperando procesamiento; En proceso = lectura, validación
  y procesamiento; Cargado = productos aceptados e incidencias persistidos, resueltos todos los
  grupos; Finalizado = procesamiento completo y resultado preparado para notificar; Notificado =
  correo enviado satisfactoriamente; Fallido = procesamiento interrumpido por error técnico tras
  agotar los reintentos permitidos. Una omisión o rechazo de negocio no implica Fallido.
  [Decisión]
- Si falla solo el envío del correo, la carga conserva Finalizado y se registra la incidencia; la
  notificación se reintenta sin reprocesar el archivo. [Decisión]
- La notificación se publica al finalizar el procesamiento del archivo, aunque existan omisiones
  de negocio. [README §3] [Decisión]
- Si la publicación en `notificaciones` no obtiene confirmación tras reintentos limitados, la carga
  conserva Finalizado y se registra una incidencia `ErrorTecnico` que describe la falta de
  confirmación, sin afirmar que el mensaje no se publicó. No hay recuperación automática, columnas
  nuevas ni uso de incidencias como mecanismo de control, y no se republican las cargas Finalizado
  al arrancar: no se garantiza la notificación posterior (limitación aceptada). [Decisión,
  Feature 006]
- Notificaciones envía el correo con MailKit y marca Notificado tras el envío exitoso.
  [README §2 y §3] [Decisión]
- Contenido del correo [Decisión, Feature 007, 2026-11-03]: asunto "Carga {idCarga} finalizada" y
  cuerpo en texto plano con idCarga y fechaFin, dirigido al usuario del mensaje; sin resúmenes,
  consultas adicionales, adjuntos ni plantillas; "finalizada" no se presenta como "sin errores".
- Reintentos del correo [Decisión, Feature 007]: los reintentos limitados del envío satisfacen "la
  notificación se reintenta"; agotados, se registra ErrorNotificacion, la carga conserva Finalizado y
  el mensaje se confirma, sin recuperación automática posterior. Si la incidencia no puede
  persistirse, el mensaje no se confirma y se aplica el manejo de fallos existente, sin reentrega
  inmediata en bucle.
- Correo enviado sin Notificado persistido [Decisión, Feature 007]: si tras enviar no puede
  persistirse Notificado después de los reintentos, el mensaje no se confirma; en la reentrega se
  consulta primero el estado (Notificado → confirmar sin reenviar; Finalizado → puede reenviarse).
  El posible correo duplicado se documenta; no se promete entrega exactamente una vez ni se añaden
  mecanismos para garantizarla.
- El número de tablas no está fijado: el plan justifica el modelo mínimo. [README §5] [Decisión]

### 9. Gateway, mensajería e integraciones
- Gateway mínimo con Ocelot en .NET 10: reenvío de solicitudes y validación del JWT emitido por
  Authentication. [README §2] [Decisión]
- Control valida el permiso para ejecutar cargas masivas. [README §3]
- Control valida también la firma y vigencia del JWT, además del Gateway, sin cabeceras propias ni
  modos que omitan la seguridad. Contrato mínimo de claims que Autenticación MUST emitir: `sub`
  (identificador numérico de Usuario) y `role` (Administrador u Operador); el correo no se exige
  como claim y se obtiene del usuario persistido. [Decisión]
- Autenticación expone `POST /auth/login` [README §2] y, por decisión del usuario (Feature 008;
  opcional en el README), refresh tokens con `POST /auth/refresh`. El token de acceso se firma con
  HS256 usando `JWT_CLAVE`, `JWT_EMISOR` y `JWT_AUDIENCIA`, con vigencia de 60 minutos
  predeterminada y configurable. Credenciales inválidas → 401 genérico con `Response<T>`, sin
  revelar si el usuario existe. Refresh token: vigencia de 1 hora predeterminada y configurable;
  se persiste solo su hash en la base (tabla creada por script versionado del runner); cada
  renovación lo invalida y entrega uno nuevo, y reusar uno invalidado → 401. Sin registro ni administración de usuarios, recuperación de
  contraseña, MFA, proveedores externos, logout ni administración de sesiones. [Decisión]
- Tamaño máximo del archivo recibido por Control: 10 MiB (10 485 760 bytes) predeterminado,
  configurable por variable de entorno. [README §2, §3] [Decisión]
- Rate Limiting obligatorio; se aplica en Control al endpoint de carga y no se duplica por costumbre
  en el Gateway. [README §4] [Decisión]
- Control publica en `carga_masiva` tras subir el archivo y registrar la carga; ante un fallo
  definitivo de publicación registra Fallido (solo desde Pendiente), su historial y la incidencia
  técnica, sin Outbox ni servicios de recuperación. Una falta de confirmación no prueba que el
  mensaje no llegó; no se promete atomicidad entre almacenamiento, base y cola. [Decisión]
- Polly para reintentos y Circuit Breaker; los parámetros concretos (cantidad, intervalos, errores
  reintentables, operaciones protegidas) se definen en las features de Control, Carga Masiva y
  Notificaciones, evitando duplicar efectos de escritura. Los reintentos son limitados; reintentar o
  reentregar un mensaje no duplica productos, no reprocesa periodos completados ni reporta como
  Existente un producto ya confirmado por la misma carga. Sin interfaz ni endpoint de reintentos.
  [README §4] [Decisión]
- RabbitMQ con las colas `carga_masiva` y `notificaciones`. [README §2 y §4] [Decisión]
- Para este examen: una sola instancia activa del consumidor de carga masiva, con procesamiento
  secuencial de mensajes y de grupos de periodo; las ejecuciones interrumpidas se recuperan antes
  de consumir nuevos mensajes y los periodos completados se conservan. Configurar una réplica no
  es por sí misma una garantía de exclusividad: se exige un mecanismo que la asegure con la
  infraestructura existente (sin servicios de barrido, Cronos ni Redis). [Decisión]
- SeaweedFS almacena los Excel. [README §2] [Decisión]
- MailKit configurable por variables de entorno. [README §4]
- MUST NOT trasladarse del Gateway de referencia: Happy, Code/Header/IdentityPool, Reec, AWS,
  SignalR, WebSockets ni agregación de Swagger. No se incluyen Emissary ni Kubernetes. [Decisión]

### 10. Logging y recursos
- Logging estructurado con Serilog y archivos `.log` vía Serilog.Sinks.File. [README §4]
  [Decisión]
- Rotación y retención se definen en el plan. Archivos separados por servicio, conservados
  mediante volúmenes de Docker. [Decisión]
- Se registran propiedades estructuradas relevantes y excepciones; MUST NOT registrarse
  contraseñas, tokens ni el contenido completo del Excel. [Decisión]
- Los logs técnicos no sustituyen la auditoría ni la trazabilidad funcional. [Decisión]
- `using`/`await using` para recursos desechables creados y poseídos por el código; no se liberan
  manualmente servicios cuyo ciclo de vida administra la inyección de dependencias. No se afirma
  que `using` libere toda la memoria ni garantice ausencia de fugas. [Decisión]

### 11. Docker, cliente y pruebas
- Solución funcional y dockerizada; un Dockerfile por ejecutable propio (no por biblioteca de
  capas). [README §1 y §4] [Decisión]
- Un Docker Compose en la raíz levanta la solución y sus dependencias. RabbitMQ y SeaweedFS pueden
  declararse directo en Compose; solo se crean directorios si requieren archivos de
  configuración. [README §4] [Decisión]
- Mailpit se declara en Compose como servidor SMTP local de pruebas y demostración: captura los
  correos y los muestra en su interfaz web, sin entregarlos a buzones externos. MailKit sigue siendo
  la biblioteca de envío y el servidor SMTP se elige por variables de entorno (Mailpit es solo el
  valor local; otro servidor se configura sin cambiar código). [Decisión del usuario, Feature 007;
  no es requisito del README]
- Orden de arranque en Compose: SQL Server disponible → runner de `database/` completado
  correctamente → microservicios. Si el runner falla, Compose no arranca los microservicios
  dependientes. No se añade otro microservicio ni un ejecutable .NET para migraciones. [Decisión]
- Postman se conserva como cliente y entregable: colecciones con cada petición. [README §2]
  [Decisión]
- Cliente web: el README lo deja como opcional valorado (React 16+, uso de componentes). [README
  §2, §4 Frontend] Por decisión del usuario (no es exigencia del README) se implementa con React,
  TypeScript y Vite; React Router para la navegación, Axios para HTTP y sus interceptores, y
  Tailwind CSS para los estilos. [Decisión del usuario]
- Responsabilidades separadas en pages, components, models, api, hooks y un contexto de
  autenticación. Los models representan los contratos de la API; los hooks se crean por
  funcionalidad cuando sean necesarios. No se crean carpetas ni capas vacías. [Decisión del
  usuario]
- El cliente web consume únicamente el Gateway, nunca los microservicios directamente. La gestión
  de sesión y la renovación del token respetan el contrato aprobado de Autenticación (token de
  acceso, refresh token con rotación, 401 genérico) y los errores siguen `Response<T>`. Sin
  dependencias, patrones ni funcionalidades adicionales a las autorizadas. [Decisión del usuario]
  [§5] [§9]
- Por decisión del usuario NO se crean proyectos ni suites de tests automatizados; esto no elimina
  la revisión documental ni la comprobación manual del flujo funcional. [Decisión]

### 12. Dependencias y simplicidad
- ClosedXML es la única biblioteca elegida para Excel. [Decisión]
- Excluidos: AutoMapper, EPPlus, Reec, Cronos, CloudWatch. BulkExtensions no está aprobado.
  [Decisión]
- No se añaden caché, CI/CD, frameworks de mensajería adicionales ni patrones no autorizados.
  No añadir plataformas ni servicios adicionales de observabilidad; se mantiene el logging
  aprobado con Serilog. [Decisión]
- Para paquetes sin versión fijada, el plan verifica compatibilidad y licencia; no se copian
  versiones antiguas de los repositorios. [Decisión]

## Pendientes y no autorizado
Permanecen pendientes (no decididos): rotación y retención de logs; parámetros de
Polly (se fijan en las features de Control, Carga Masiva y Notificaciones); verificación
operativa de Docker y SQL Server en el equipo de ejecución; la adaptación y validación del
runner de `database/` y la limpieza de ms-control (migraciones EF Core, modelo completo y
arranque con migraciones) quedan pendientes de una nueva feature. Del cliente web: su directorio
contenedor en el monorepo (§2 enumera los directorios de la raíz y no lo incluye), su despliegue
(Dockerfile y servicio en Compose, §11) y el acceso del navegador al Gateway desde otro origen
(CORS u origen común; §9 define un Gateway mínimo).
No se adoptan las convenciones de idioma, commits ni versionado de endpoints del ejemplo Bistro.

## Governance
Esta constitución prevalece sobre otras prácticas del proyecto. Enmiendas: documentadas y
aprobadas por el usuario antes de aplicarse. Versionado semántico: MAJOR por redefiniciones o
eliminaciones incompatibles; MINOR por principios o secciones nuevas; PATCH por aclaraciones.
Toda especificación, plan y tarea MUST verificar cumplimiento de esta constitución, indicar la
procedencia de cada obligación y justificar cualquier complejidad añadida.

**Version**: 3.6.0 | **Ratified**: TODO(RATIFICATION_DATE): pendiente de aprobación del usuario | **Last Amended**: 2026-11-03
