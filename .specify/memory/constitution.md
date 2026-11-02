<!--
Sync Impact Report
- Versión: 1.3.0 → 2.0.0 (MAJOR: se invierte la regla de §6 que prohibía EF Core Migrations y
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
  (implementación anterior de scripts SQL; fuera del flujo activo), `ms-autenticacion/` (autenticación y emisión de JWT),
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
- El host (Api o Worker) de los consumidores de colas queda pendiente del plan; los proyectos
  HTTP usan `Acity.Api`. [Decisión]
- Los ejecutables de cada microservicio son independientes. [README §4] [Decisión]

### 4. Capas y convenciones
- Proyectos por microservicio: Acity.Api (proyectos con entradas HTTP), Acity.Application,
  Acity.Domain, Acity.Infrastructure y Acity.Common. El nombre del ejecutable de los consumidores
  se define al resolver Api/Worker en el plan. MUST NOT usarse Servicio.Api, Servicio.Domain ni
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
- El tratamiento de Data sin resultado y la serialización se definen en el plan. [Decisión]
- Manejo global de excepciones, sin Reec. [README §4] [Decisión]
- MUST NOT existir dos formatos de error incompatibles ni campos añadidos al contrato sin
  autorización. [Decisión]

### 6. Persistencia
- SQL Server; EF Core y LINQ en Infrastructure; sin Dapper. [README §4 y §5] [Decisión]
- Esquema gestionado con EF Core Migrations. `ms-control/Acity.Infrastructure` es el único
  propietario de las migraciones de toda la base SQL Server compartida; los demás microservicios
  usan EF Core sin generar ni aplicar migraciones. [Decisión]
- Modelado inicial integral y una migración inicial que materialice el esquema aprobado; los
  scripts de `referencias/` sirven de referencia para los mapeos y esa migración. [Decisión]
- Las migraciones se aplican automáticamente al iniciar ms-control, antes de atender solicitudes
  (migraciones automáticas). [README §4] [Decisión]
- Un único responsable aplica los cambios de esquema: ms-control. [Decisión]
- Procedimientos almacenados conforme al README; sus responsabilidades se delimitan en el plan.
  Se conservan y se incorporan mediante SQL dentro de las migraciones. [README §4] [Decisión]
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
- Ante una validación de periodo fallida termina el procesamiento de ese grupo, no el de los
  demás. [README §3] [Decisión]
- Ante un fallo técnico terminal de un periodo se detiene el procesamiento del archivo y la carga
  queda Fallido: se conservan los periodos completados, los pendientes se registran como “No
  procesado por interrupción de la carga” (sin afirmar que fallaron) y se liberan sus reservas.
  Esto no altera el rechazo de negocio, que no detiene los demás periodos. [Decisión]
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
- Notificaciones envía el correo con MailKit y marca Notificado tras el envío exitoso.
  [README §2 y §3] [Decisión]
- El número de tablas no está fijado: el plan justifica el modelo mínimo. [README §5] [Decisión]

### 9. Gateway, mensajería e integraciones
- Gateway mínimo con Ocelot en .NET 10: reenvío de solicitudes y validación del JWT emitido por
  Authentication. [README §2] [Decisión]
- Control valida el permiso para ejecutar cargas masivas. [README §3]
- Rate Limiting obligatorio. [README §4]
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
- Postman es el cliente y entregable actual. React queda pendiente de confirmación: no se
  implementa ni se añaden dependencias anticipadas. [README §2 y §6] [Decisión]
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
Permanecen pendientes (no decididos): host Api/Worker de los consumidores; tratamiento de Data
sin resultado y serialización de `Response<T>`; rotación y retención de logs; parámetros de
Polly (se fijan en las features de Control, Carga Masiva y Notificaciones); tratamiento operativo
de archivos ilegibles y recuperación de notificaciones no publicadas (features del servicio
que corresponda); límites de tamaño de archivo; contenido exacto del correo; React; verificación
operativa de Docker y SQL Server en el equipo de ejecución; el arranque de ms-control con
migraciones queda pendiente de su feature. Refresh tokens es opcional en el
README y no está decidido.
No se adoptan las convenciones de idioma, commits ni versionado de endpoints del ejemplo Bistro.

## Governance
Esta constitución prevalece sobre otras prácticas del proyecto. Enmiendas: documentadas y
aprobadas por el usuario antes de aplicarse. Versionado semántico: MAJOR por redefiniciones o
eliminaciones incompatibles; MINOR por principios o secciones nuevas; PATCH por aclaraciones.
Toda especificación, plan y tarea MUST verificar cumplimiento de esta constitución, indicar la
procedencia de cada obligación y justificar cualquier complejidad añadida.

**Version**: 2.0.0 | **Ratified**: TODO(RATIFICATION_DATE): pendiente de aprobación del usuario | **Last Amended**: 2026-11-02
