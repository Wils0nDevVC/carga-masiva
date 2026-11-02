# Feature Specification: Fundación común de los cuatro microservicios

**Feature Branch**: `002-fundacion-microservicios` (no se creó rama; se trabaja en `main`)

**Created**: 2026-11-02

**Status**: Draft

**Input**: Fundación común de `ms-autenticacion`, `ms-control`, `ms-carga-masiva` y `ms-notificaciones`: estructura de capas, convenciones, configuración, logs, salud, migraciones EF Core y Docker. Una sola feature; esta etapa genera solo la especificación.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v2.1.0; `[Decisión]` = decisión explícita del usuario (incluye el prompt de esta feature); `[Referencia]` = observado en el código del repositorio de referencia (ver "Contexto"); `[Feature 001]` = decisiones y modelo de `specs/001-persistencia-integridad-carga/`. Un requisito sin etiqueta propia no es una decisión aprobada.

## Contexto

Monorepo con una constitución y un flujo Spec Kit en la raíz. Hoy existen los directorios `ms-autenticacion/`, `ms-control/`, `ms-carga-masiva/`, `ms-notificaciones/` y `api-gateway/`, todos vacíos, además de `database/` (implementación anterior por runner de scripts, fuera del flujo activo), un `compose.yaml` con solo el servicio SQL Server verificado y `referencias/V001__esquema_inicial.sql` y `V002__procedimientos.sql` como referencia del esquema. [Decisión]

**Revisión del proyecto de referencia** (`Wils0nDevVC/Clean_architecture_base_net7`, carpeta `src`): se pudo acceder al repositorio y se leyeron el listado de archivos, los cinco `.csproj`, los dos `DependencyInjection.cs`, `Program.cs`, `BaseDbContext.cs`, `BaseEntity.cs`, `IUnitOfWork.cs` e `IGenericRepository.cs`. **No** se leyeron los controladores, los handlers ni los demás archivos de `Application`/`Common`. Lo observado y usado como guía de organización:

- Cinco proyectos: `Api`, `Application`, `Domain`, `Infrastructure` y `Common`. Dependencias: `Api → Infrastructure → Application → {Common, Domain}`; `Domain` no referencia nada. [Referencia]
- Registro de dependencias por capa con métodos de extensión (`AddApplication`, `AddInfrastructure`) invocados desde `Program.cs`; un `DbContext` base con configuraciones de entidad aplicadas desde el ensamblado; repositorio genérico y `IUnitOfWork` registrados como `Scoped`. [Referencia]
- La referencia usa .NET 7, MediatR 10, AutoMapper, Dapper, BulkExtensions, Cronos, EPPlus, Reec, CloudWatch y Swashbuckle, y un `IUnitOfWork` con operaciones de carga masiva y truncado. **Nada de eso se traslada**: las decisiones del proyecto (.NET 10, MediatR 12.5.0, sin esos paquetes) prevalecen. [Decisión] [Constitución §3, §12]

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Estructura común y compilable por servicio (Priority: P1)

Cada uno de los cuatro microservicios tiene la misma organización de capas, compila y se ejecuta de forma independiente, sin referencias de código a otro servicio ni a una biblioteca compartida adicional. [Decisión]

**Why this priority**: Es la base sobre la que se desarrollan todas las features funcionales; sin ella nada compila ni arranca.

**Independent Test**: Compilar y ejecutar cada servicio usando solo las rutas de su directorio, y revisar que ningún proyecto referencia a otro servicio.

**Acceptance Scenarios**:

1. **Given** el repositorio con la fundación implementada, **When** se compila y ejecuta cada servicio desde su propio directorio, **Then** los cuatro compilan y arrancan sin depender de que otro servicio esté compilado.
2. **Given** los proyectos de un servicio, **When** se revisan sus referencias, **Then** `Acity.Domain` no depende de ninguna otra capa, `Acity.Application` depende de Domain y Common, `Acity.Infrastructure` depende de Application y `Acity.Api` compone las capas y expone el host.
3. **Given** los cuatro servicios, **When** se revisan los nombres y referencias, **Then** los nombres repetidos (`Acity.*`) pertenecen a proyectos independientes y ninguna referencia cruza de un servicio a otro.
4. **Given** una capa sin contenido necesario, **When** se revisa la estructura, **Then** no existen carpetas, repositorios, abstracciones ni comportamientos sin una necesidad concreta de esta fundación, ni CRUD de ejemplo.
5. **Given** los `Acity.Api` de los cuatro servicios, **When** se arrancan, **Then** todos son hosts ASP.NET Core Web API con controladores; los consumidores RabbitMQ futuros se ejecutarán mediante `BackgroundService` dentro de ese host y no existen proyectos `Acity.Worker`.

---

### User Story 2 - Control inicializa el esquema con migraciones EF Core (Priority: P1)

Al iniciar, `ms-control` aplica automáticamente las migraciones EF Core sobre la base SQL Server compartida, antes de declararse listo. La migración inicial materializa el esquema aprobado y los procedimientos. [Decisión]

**Why this priority**: Todo servicio dependiente del esquema necesita que la base exista y esté en el estado aprobado; Control es su único propietario.

**Independent Test**: Iniciar `ms-control` contra una base nueva y vacía y revisar tablas, índices, restricciones, datos base y procedimientos; reiniciarlo y comprobar que no se reaplica nada.

**Acceptance Scenarios**:

1. **Given** una base nueva y vacía y SQL Server disponible, **When** `ms-control` inicia, **Then** se crean las siete tablas del modelo aprobado (sin el historial del runner anterior como tabla de negocio), sus relaciones, índices, restricciones y los datos base (roles Administrador y Operador), más los procedimientos `usp_ReservarPeriodo` y `usp_CambiarEstadoCarga` incorporados mediante SQL dentro de las migraciones, y solo entonces `ms-control` se declara listo.
2. **Given** el esquema ya creado por migraciones, **When** `ms-control` se reinicia, **Then** no vuelve a crear objetos ni reaplica migraciones ya ejecutadas.
3. **Given** una migración que falla, **When** `ms-control` arranca, **Then** no se declara listo, el fallo queda registrado sin exponer secretos y los servicios dependientes no arrancan.
4. **Given** SQL Server aún no disponible, **When** `ms-control` inicia, **Then** espera la disponibilidad hasta un límite definido en el plan y, si no se alcanza, no se declara listo.
5. **Given** una base creada con los scripts anteriores (objetos existentes sin historial de migraciones de EF), **When** se arranca la fundación, **Then** no se aplica encima la migración inicial ni se inventa un historial, y la base existente se conserva; la validación se realiza sobre una base nueva y vacía.
6. **Given** los servicios `ms-autenticacion`, `ms-carga-masiva` y `ms-notificaciones`, **When** arrancan, **Then** no generan ni ejecutan migraciones ni modifican el esquema.

---

### User Story 3 - Docker Compose levanta la fundación respetando dependencias (Priority: P1)

Un Docker Compose en la raíz levanta SQL Server y los cuatro servicios con un Dockerfile por microservicio ejecutable, en el orden que exige el esquema. [Decisión]

**Why this priority**: La entrega debe ser funcional y dockerizada; el arranque ordenado evita que un servicio use un esquema que aún no existe.

**Independent Test**: Levantar Compose sobre una base y un volumen nuevos y comprobar el orden de arranque y el estado de salud de cada servicio.

**Acceptance Scenarios**:

1. **Given** el Compose de la raíz, **When** se levanta, **Then** SQL Server arranca primero, Control espera su disponibilidad antes de migrar, y los demás servicios arrancan solo después de que Control termine correctamente la inicialización.
2. **Given** que la migración de Control falla, **When** Compose evalúa las dependencias, **Then** Control no se declara listo y los servicios dependientes no arrancan.
3. **Given** el Compose existente con SQL Server, **When** se amplía, **Then** reutiliza la configuración verificada de SQL Server y conserva sus volúmenes y los recursos ajenos al proyecto.
4. **Given** el arranque real, **When** no se ha comprobado, **Then** Docker no se declara funcional (ver Asuntos pendientes).

---

### User Story 4 - Contrato común de respuesta y errores (Priority: P2)

Los servicios responden con `Response<T>` y un manejo global de excepciones compatible, con `Code` igual al estado HTTP real. [Constitución §5] [Decisión]

**Why this priority**: Es el contrato que usarán todos los endpoints futuros; conviene fijarlo antes de las features funcionales.

**Independent Test**: Provocar manualmente una excepción no controlada y una validación fallida en un host y comparar el cuerpo con el estado HTTP.

**Acceptance Scenarios**:

1. **Given** una excepción no controlada en cualquier servicio, **When** se responde, **Then** el cuerpo es un `Response<T>` con `IsSuccess` falso y `Code` igual al estado HTTP real.
2. **Given** una validación fallida, **When** se responde, **Then** `Errors` contiene los mensajes de validación y `Code` coincide con el estado HTTP.
3. **Given** `Errors` sin mensajes, **When** se serializa, **Then** la colección admite ausencia (nullable).
4. **Given** cualquier error, **When** se responde, **Then** no se filtran trazas de pila, cadenas de conexión ni secretos en el cuerpo.

---

### User Story 5 - Configuración, logs y salud por servicio (Priority: P2)

Cada servicio se configura por entorno, usa variables de entorno para secretos, escribe sus propios logs estructurados en archivos `.log` conservados en volúmenes, y expone una comprobación mínima de salud. [Constitución §10] [Decisión]

**Why this priority**: Permite operar y verificar el arranque de cada servicio sin plataformas adicionales.

**Independent Test**: Arrancar cada servicio, consultar su salud y revisar su archivo de log y que no contiene secretos.

**Acceptance Scenarios**:

1. **Given** un servicio arrancado, **When** se consulta su comprobación de salud, **Then** indica que el host arrancó; en Control, además, solo indica listo tras completar la inicialización del esquema.
2. **Given** los cuatro servicios, **When** escriben logs, **Then** cada uno produce sus propios archivos `.log` estructurados, separados de los de otro servicio, y persisten en un volumen de Docker.
3. **Given** cualquier log, **When** se revisa, **Then** no contiene contraseñas, tokens ni secretos.
4. **Given** una variable de entorno de un secreto ausente, **When** el servicio arranca, **Then** falla de forma clara sin imprimir el secreto ni valores por defecto inseguros.

---

### Edge Cases

- SQL Server tarda o no responde al arrancar: Control no migra ni se declara listo; no hay reintento infinito.
- Falla a mitad de la migración inicial: no debe quedar un estado que se declare listo; el plan define cómo evitar un esquema parcial.
- Dos arranques simultáneos de Control: se asume una sola instancia de Control; el plan debe indicar si hace falta protección adicional.
- Base existente creada por el runner anterior (volumen y base ya presentes en este equipo): se conserva; la validación usa una base nueva y vacía.
- El motor SQL Server amd64 corre emulado en Apple silicon, sin soporte oficial de Microsoft; la verificación manual vale solo para el equipo donde se hizo.
- Un servicio dependiente arranca antes de que Control esté listo: Compose lo impide.
- Una capa que no necesita un repositorio o abstracción: no se crea.

## Requirements *(mandatory)*

### Functional Requirements

**Estructura y dependencias**

- **FR-001**: Cada uno de `ms-autenticacion/`, `ms-control/`, `ms-carga-masiva/` y `ms-notificaciones/` MUST tener los proyectos .NET 10 `src/Acity.Api`, `src/Acity.Application`, `src/Acity.Domain`, `src/Acity.Infrastructure` y `src/Acity.Common`. [Decisión] [Constitución §4]
- **FR-002**: `Acity.Api` MUST ser el host ASP.NET Core Web API con controladores en los cuatro servicios, siguiendo la arquitectura de referencia; los consumidores RabbitMQ se ejecutarán mediante `BackgroundService` dentro de ese host cuando se implementen sus funciones, y MUST NOT crearse proyectos `Acity.Worker`. [Decisión] [Constitución §3]
- **FR-003**: Las referencias entre capas MUST seguir Clean Architecture: Domain sin dependencias de otras capas; Application depende de Domain y Common; Infrastructure depende de Application; Api compone las capas y registra las dependencias. El dominio MUST ser independiente de la infraestructura. [Referencia] [Decisión]
- **FR-004**: MUST NOT existir referencias de código entre proyectos de distintos microservicios ni una biblioteca compartida adicional; los nombres repetidos son proyectos independientes. [Decisión]
- **FR-005**: Cada servicio MUST poder compilarse y ejecutarse de forma independiente y sus comandos de compilación y ejecución MUST ser inequívocos mediante las rutas de ese servicio. [Decisión]
- **FR-006**: El registro de dependencias MUST hacerse por capa (métodos de extensión por capa, invocados desde el host). [Referencia] [Decisión]
- **FR-007**: Application MUST usar CQRS con MediatR 12.5.0 y FluentValidation. [Constitución §3, §4] [Decisión]
- **FR-008**: MUST existir un Repository y un Unit of Work mínimos, conforme a la referencia, con solo las operaciones necesarias; MUST NOT incluir operaciones masivas, de truncado ni repositorios por entidad, y MUST NOT crearse repositorios, abstracciones ni comportamientos sin una necesidad concreta de esta fundación. El conjunto mínimo exacto lo fija el plan. [Referencia] [Decisión] [Constitución §3]
- **FR-009**: MUST NOT generarse CRUD ni endpoints de ejemplo. Los únicos endpoints son la comprobación de salud. [Decisión] [Constitución §3]

**Convenciones**

- **FR-010**: Los DTO MUST ser `record`; las entidades e implementaciones, clases; las abstracciones, interfaces. [Constitución §4] [Decisión]
- **FR-011**: Los mapeos MUST ser manuales y mínimos; MUST NOT usarse AutoMapper ni un motor genérico de mapeo. [Constitución §4] [Decisión]
- **FR-012**: Los recursos desechables creados y poseídos por el código MUST liberarse con `using` o `await using`; MUST NOT disponerse manualmente servicios administrados por la inyección de dependencias. [Constitución §10] [Decisión]

**Contrato de respuesta y errores**

- **FR-013**: `Response<T>` MUST ser un `record` genérico en `Acity.Common/Helpers` con `Data`, `IsSuccess`, `Message`, `Code` y `Errors`. [Constitución §5] [Decisión]
- **FR-014**: `Code` MUST representar el estado HTTP real y coincidir con él en toda respuesta. [Constitución §5] [Decisión]
- **FR-015**: `Errors` MUST admitir una colección nullable de mensajes. [Decisión]
- **FR-016**: MUST existir un manejo global de excepciones compatible con `Response<T>`, sin usar Reec ni otro formato de error incompatible. [Constitución §5] [README §4]
- **FR-017**: Los errores MUST NOT exponer trazas, cadenas de conexión ni secretos en la respuesta. (Valor por defecto de seguridad; ver Assumptions.)

**Configuración, observabilidad y salud**

- **FR-018**: La configuración MUST variar por entorno y los secretos MUST proveerse por variables de entorno, nunca en archivos versionados. [Decisión] [Constitución §10]
- **FR-019**: Cada servicio MUST usar Serilog con logging estructurado y escribir sus propios archivos `.log`, separados por servicio. [Constitución §10] [Decisión]
- **FR-020**: Los logs MUST persistir mediante volúmenes de Docker. [Constitución §10] [Decisión]
- **FR-021**: MUST NOT registrarse contraseñas, tokens ni secretos. [Constitución §10] [Decisión]
- **FR-022**: Cada servicio MUST exponer una comprobación mínima de salud que valide su arranque; en `ms-control` MUST indicar listo solo después de completar la inicialización del esquema. [Decisión]
- **FR-023**: MUST NOT agregarse plataformas adicionales de observabilidad. [Constitución §12] [Decisión]

**Persistencia y migraciones**

- **FR-024**: La persistencia MUST ser SQL Server con EF Core y LINQ. [Constitución §6] [Decisión]
- **FR-025**: `ms-control/Acity.Infrastructure` MUST ser el único propietario del modelo completo necesario para las migraciones de la base compartida. [Constitución §6] [Decisión]
- **FR-026**: La migración inicial MUST generarse tomando como referencia `referencias/V001__esquema_inicial.sql` y MUST preservar sus tablas, relaciones, índices, restricciones y datos base aprobados. [Decisión] [Feature 001]
- **FR-027**: Los procedimientos de `referencias/V002__procedimientos.sql` MUST incorporarse mediante SQL dentro de las migraciones. [Decisión]
- **FR-028**: El historial del runner anterior (`SchemaMigraciones`) MUST NOT trasladarse como parte del modelo de negocio. [Decisión]
- **FR-029**: Las migraciones MUST aplicarse automáticamente al iniciar `ms-control`, antes de que el servicio esté listo para atender solicitudes. [Constitución §6] [Decisión]
- **FR-030**: Reiniciar `ms-control` MUST NOT volver a crear objetos ni reaplicar migraciones ya ejecutadas. [Decisión]
- **FR-031**: Los demás servicios MUST NOT generar ni ejecutar migraciones ni modificar el esquema al arrancar. [Constitución §6] [Decisión]
- **FR-032**: Los modelos de acceso de los demás servicios MUST limitarse a lo que necesiten; MUST NOT duplicarse todo el esquema en cada servicio por uniformidad. [Decisión]
- **FR-033**: MUST NOT crearse un ejecutable de migraciones ni reactivarse el runner de `database/`. [Decisión]
- **FR-034**: Si existe una base creada con los scripts anteriores, MUST NOT aplicarse encima una migración inicial ni inventarse un historial de migraciones; la base existente se conserva y la validación se prevé sobre una base nueva y vacía. [Decisión]
- **FR-035**: La lógica funcional de autenticación, procesamiento y notificación MUST quedar fuera de esta feature, aunque sus tablas se creen aquí. [Decisión]

**Docker**

- **FR-036**: MUST existir un Dockerfile por microservicio ejecutable. [Constitución §11] [Decisión]
- **FR-037**: MUST existir un Docker Compose en la raíz que levante esta fundación y reutilice la configuración SQL Server verificada. [Constitución §11] [Decisión]
- **FR-038**: Control MUST esperar la disponibilidad de SQL Server antes de aplicar migraciones. [Decisión]
- **FR-039**: Los servicios dependientes del esquema MUST arrancar después de que Control termine correctamente la inicialización; si la migración falla, Control MUST NOT declararse listo. [Decisión]
- **FR-040**: Compose MUST conservar los volúmenes y recursos ajenos al proyecto. [Decisión]
- **FR-041**: Docker MUST NOT declararse funcional hasta comprobar el arranque real. [Decisión]

**Límites**

- **FR-042**: Esta feature MUST NOT incluir login, JWT, refresh tokens ni usuarios de demostración; recepción, lectura o validación de Excel; publicación o consumo de RabbitMQ; integración funcional con SeaweedFS; procesamiento de periodos o productos; envío de correos; recuperación de cargas ni políticas concretas de Polly; el API Gateway; React; ni pruebas automatizadas, proyectos de tests o dependencias de testing. [Decisión]
- **FR-043**: RabbitMQ, SeaweedFS, MailKit, ClosedXML y Polly conservan su lugar en la arquitectura aprobada, pero MUST NOT agregarse implementaciones vacías ni paquetes que esta fundación no utilice. [Decisión] [Constitución §9, §12]
- **FR-044**: MUST NOT incorporarse Reec, AutoMapper, EPPlus, Cronos, CloudWatch, Dapper, BulkExtensions ni otras dependencias no autorizadas. [Constitución §12] [Decisión]

### Key Entities

- **Microservicio**: unidad ejecutable independiente con sus cinco proyectos, su host, su Dockerfile, su configuración y sus logs; no referencia a otro microservicio.
- **Response<T>**: contrato de respuesta común (Data, IsSuccess, Message, Code, Errors).
- **Esquema compartido**: base SQL Server única; sus tablas, índices, restricciones, datos base y procedimientos son los de la Feature 001 y los crea y versiona solo Control.
- **Historial de migraciones**: registro que lleva EF Core de las migraciones aplicadas; no es parte del modelo de negocio.
- **Comprobación de salud**: indicador mínimo de que un servicio arrancó (y, en Control, de que el esquema está inicializado).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Los 4 servicios se compilan y ejecutan de forma independiente usando solo las rutas de su directorio.
- **SC-002**: 0 referencias de código entre microservicios y 0 bibliotecas compartidas adicionales.
- **SC-003**: Los 4 hosts arrancan y responden su comprobación de salud.
- **SC-004**: El 100 % de las respuestas de error comprobadas manualmente usa `Response<T>` con `Code` igual al estado HTTP.
- **SC-005**: Cada servicio produce sus propios archivos `.log` y 0 contienen contraseñas, tokens o secretos.
- **SC-006**: Sobre una base nueva y vacía, Control crea las 7 tablas del modelo aprobado, sus índices y restricciones, los 2 roles base y los 2 procedimientos.
- **SC-007**: Un segundo arranque de Control no crea objetos nuevos ni reaplica ninguna migración.
- **SC-008**: 0 servicios distintos de Control modifican el esquema al arrancar.
- **SC-009**: Compose levanta la fundación respetando el orden SQL Server → Control → demás servicios, y con la migración fallida los dependientes no arrancan.
- **SC-010**: 0 paquetes no autorizados y 0 proyectos o dependencias de pruebas.

## Assumptions

- Las reglas de negocio, el modelo y los contratos de procedimientos son los de la Feature 001; aquí solo se materializan en migraciones. El modelo de referencia usa `Usuario.RolId` (un rol por usuario), cuya confirmación sigue pendiente en la Feature 001.
- Una sola instancia de Control durante el examen; el plan decide si basta o si hace falta protección adicional ante arranques simultáneos.
- Los errores no exponen detalles internos al cliente (valor por defecto de seguridad coherente con el no registrar secretos).
- Las versiones de los paquetes, los puertos, los parámetros de rotación y retención de logs, el formato exacto de la comprobación de salud, el tratamiento de `Data` sin resultado y la serialización de `Response<T>` se resuelven en el plan con soluciones mínimas. [Constitución §5, §10, §12]
- La verificación es manual; no se crean suites de pruebas. [Constitución §11]
- No se implementa el API Gateway: los servicios se alcanzan directamente en esta fundación.
- Fuera de alcance: todo lo listado en FR-042 a FR-044.

## Asuntos pendientes (no son requisitos aprobados)

- Base y volumen existentes en este equipo creados por el runner anterior: el plan debe usar una base nueva y vacía (nombre o volumen distintos) y conservar los existentes.
- Verificación operativa de Docker y SQL Server: la imagen es amd64 y se ejecuta emulada en Apple silicon, sin soporte oficial de Microsoft; solo se comprobó en este equipo con el runner anterior. No se declara Docker funcional.
- Compatibilidad y licencia de EF Core 10 y de los demás paquetes (sin fijar versiones): se verifican en el plan.
- Detalles que el plan debe fijar: conjunto mínimo de Repository/Unit of Work, aplicación de FluentValidation a las solicitudes, forma de la comprobación de salud y de la espera de SQL Server, manejo de un fallo parcial de migración y estructura de solución por servicio.
