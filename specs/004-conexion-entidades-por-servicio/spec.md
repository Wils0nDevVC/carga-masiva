# Feature Specification: Conexión a CargaMasivaApp y entidades por microservicio

**Feature Branch**: `004-conexion-entidades-por-servicio` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-02

**Status**: Draft

**Input**: Conexión de los cuatro microservicios a la base `CargaMasivaApp` y entidades y mapeos necesarios en cada uno, obtenidos mediante ingeniería inversa de las tablas existentes con EF Core + LINQ. El esquema sigue gestionado en `database/`. Una sola feature; esta etapa genera solo la especificación (sin scaffolding, sin cambios de código ni de bases).

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v3.0.0; `[Decisión]` = decisión explícita del usuario (incluye el prompt de esta feature); `[Feature 001]` = `specs/001-persistencia-integridad-carga/` (reglas, modelo y contratos de procedimientos); `[Feature 002]` = fundación; `[Feature 003]` = runner y limpieza de Control; `[Referencia]` = observado en el repositorio de referencia (ver "Contexto"); `[Observado]` = comprobado leyendo el repositorio en esta etapa. Un requisito sin etiqueta propia no es una decisión aprobada.

## Contexto

**Estado observado hoy** [Observado]: los cuatro servicios ya reciben la cadena de conexión por la variable de entorno `ConnectionStrings__Default` (Compose la arma con la base `CargaMasivaApp` y las credenciales de `.env`) y registran un `AppDbContext` con SQL Server en `Acity.Infrastructure/DependencyInjection.cs`. Los cuatro `AppDbContext` **no tienen entidades** (Feature 003 retiró las siete de Control). Ningún servicio abre hoy una conexión: `GET /health` solo comprueba que el host arrancó y no hay endpoints de negocio. El esquema (7 tablas y 2 procedimientos) lo crea el runner de `database/`; `CargaMasivaApp` es idéntica a la referencia `V001`/`V002`.

**Revisión del proyecto de referencia** (`Wils0nDevVC/Clean_architecture_base_net7`, carpeta `src`, indicado en `referencias/repositorios.md`): **se pudo acceder** al repositorio público (listado completo de archivos mediante la API de GitHub y lectura del contenido crudo de los archivos de persistencia). Se leyeron: `Todo.Infrastructure/DependencyInjection.cs`, `Persistence/BaseDbContext.cs`, `Persistence/Configurations/TodoConfiguration.cs`, `Todo.Domain/Entities/TodoEntity.cs`, `BaseEntity.cs`, `IGenerateEntity.cs`, el `.csproj` de Infrastructure, las líneas de `Program.cs` que registran capas y conexión, las claves de `appsettings.Development.json` (sin valores secretos) y el comienzo de `IUnitOfWork.cs`. **No** se leyeron los controladores, los handlers ni el resto de `Application`/`Api`. Lo observado y usado como guía de organización [Referencia]:

- Las entidades son clases en `Domain/Entities`; su mapeo es una clase `IEntityTypeConfiguration<T>` por entidad en `Infrastructure/Persistence/Configurations` (`ToTable`, `HasKey`), que el `DbContext` aplica con `ApplyConfigurationsFromAssembly`.
- La conexión se registra en `AddInfrastructure` con `AddDbContext<…>(UseSqlServer(GetConnectionString("…")))`; el repositorio genérico y el Unit of Work se registran como `Scoped`.
- **No se traslada** [Decisión] [Constitución §12]: .NET 7, EF Core 7 y sus paquetes, `BaseEntity`/`IGenerateEntity`, `IUnitOfWork` con un repositorio por entidad y operaciones masivas, AutoMapper, BulkExtensions, Reec, Newtonsoft, Polly en Infrastructure, `Models/Database` ni la cadena de conexión en archivos de configuración. Las decisiones del proyecto (.NET 10, EF Core 10.0.14, Repository/UoW mínimos, secretos por entorno) prevalecen. [Feature 002]

**Decisiones anteriores que esta feature precisa** (el esquema, las reglas de negocio y el runner no cambian):

| Decisión anterior | Dónde está | Pasa a ser |
|---|---|---|
| Los cuatro `AppDbContext` sin entidades; "las entidades se incorporan cuando una feature funcional las necesite" | Feature 002 `data-model.md`; Feature 003 `data-model.md` y `research.md` D-04 | Esta feature incorpora las entidades de la tabla de distribución, justificadas por las funciones descritas en el README y las responsabilidades ya documentadas; no se agregan otras |
| Ingeniería inversa solo "cuando su feature funcional las necesite" para los tres servicios sin entidades | Feature 003 FR-028 | Se aplica ahora a los tres servicios y a Control, con el conjunto aprobado en esta spec [Decisión: prompt de esta feature] |
| Control retiró sus siete entidades por falta de uso actual | Feature 003 | Control recibe solo tres (`CargaArchivo`, `CargaEstadoHistorial`, `Incidencia`), justificadas por su función de recepción y seguimiento; no vuelve al modelo completo |

## Distribución de entidades por microservicio

**Regla**: cada servicio mapea solo las tablas que sus funciones usan; no se concentra el modelo completo en Control ni se duplica en todos por uniformidad. [Decisión] [Constitución §6]

| Tabla | ms-autenticacion | ms-control | ms-carga-masiva | ms-notificaciones |
|---|---|---|---|---|
| Rol | **Sí** | — | — | — |
| Usuario | **Sí** | — | — | — |
| CargaArchivo | — | **Sí** | **Sí** | — |
| CargaEstadoHistorial | — | **Sí** | — | — |
| CargaPeriodo | — | — | **Sí** | — |
| Producto | — | — | **Sí** | — |
| Incidencia | — | **Sí** | **Sí** | **Sí** |

Resultado: 2 + 3 + 4 + 1 = 10 mapeos de 7 tablas; ningún servicio mapea las siete; cada tabla la mapea al menos un servicio que la usa.

**Justificación y procedencia por servicio**

| Servicio | Tablas | Por qué (función) | Procedencia |
|---|---|---|---|
| ms-autenticacion | `Usuario`, `Rol` | Valida credenciales contra la base y emite el JWT con claims del usuario; crea los usuarios iniciales (uno por rol) al arrancar y por eso resuelve el rol por nombre; guarda el hash. Es el servicio que escribe `Usuario` | [README §2 Microservicio 0, §3 paso 0]; [Constitución §6] (identidad propia, roles, usuarios iniciales por ms-autenticacion, `PasswordHasher`); [Feature 001] contrato "Almacenamiento de identidad (contrato para ms-autenticacion)"; [Feature 002] `data-model.md` ("p. ej. `Usuario`, `Rol`") |
| ms-control | `CargaArchivo`, `CargaEstadoHistorial`, `Incidencia` | Registra la carga con estado inicial Pendiente y la auditoría de quién y cuándo (`CargaArchivo`); crea el historial inicial (`CargaEstadoHistorial`); y su función de seguimiento de cargas expone el historial y el estado, y, según el rol, las incidencias propias o de todos (`Incidencia`) | [README §2 Microservicio 1, §3 paso 2]; [Feature 001] `research.md` R-09 ("Control crea la carga y el historial inicial"); [Constitución §2] (Control = recepción de archivos y seguimiento de cargas), [Constitución §6] (Administrador consulta todas las cargas y sus incidencias; Operador solo las propias) |
| ms-carga-masiva | `CargaArchivo`, `CargaPeriodo`, `Producto`, `Incidencia` | Lee la carga y actualiza lo que el procedimiento no cubre (p. ej. `IntentosProceso`); pasa cada periodo a Completado e inserta productos e incidencias de negocio y técnicas dentro de la transacción de cada grupo; comprueba la unicidad global de `CodigoProducto` | [README §2 Microservicio 2, §3 paso 3] (duplicidad por periodo y por código; fallidos en tabla de auditoría); [Feature 001] contrato de procedimientos ("la transacción EF por grupo de periodo contiene solo inserciones de Producto e Incidencia de negocio y el paso del periodo a Completado"; los procedimientos no insertan productos ni cierran periodos; la aplicación cuenta intentos) |
| ms-notificaciones | `Incidencia` | Si falla el envío del correo, registra la incidencia de notificación; la carga conserva Finalizado y la notificación se reintenta. El paso a Notificado lo aplica el procedimiento `usp_CambiarEstadoCarga` (no requiere entidad) y los datos del mensaje (`idCarga`, `usuario`) llegan en él | [README §2 Microservicio 3, §3 paso 4]; [Constitución §8]; [Feature 001] spec (incidencia `ErrorNotificacion`) y contrato de procedimientos |

**Excluidas con justificación**: `CargaEstadoHistorial` en ms-carga-masiva (el historial lo escribe `usp_CambiarEstadoCarga`); `Usuario` en ms-carga-masiva y ms-notificaciones (el correo del usuario viaja en el mensaje de la cola [README §3]); `CargaPeriodo` y `Producto` en Control (sin función de Control aprobada que las use; ver Asuntos pendientes). Los procedimientos almacenados no son entidades.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cada servicio se conecta a CargaMasivaApp (Priority: P1)

Los cuatro microservicios se conectan a la base `CargaMasivaApp` con la cadena de conexión provista por variable de entorno y pueden leer sus tablas mapeadas con EF Core y LINQ; los secretos no aparecen en archivos versionados ni en logs ni en respuestas. [Decisión] [Constitución §6, §10] [Feature 002]

**Why this priority**: Sin conexión verificada, ningún servicio puede usar sus entidades.

**Independent Test**: Con la base creada por el runner y los servicios levantados con Compose, comprobar (con el mecanismo manual que defina el plan) que cada servicio abre una conexión a `CargaMasivaApp` y consulta una tabla de su conjunto.

**Acceptance Scenarios**:

1. **Given** Compose levantado sobre `CargaMasivaApp`, **When** se comprueba cada servicio, **Then** abre una conexión a `CargaMasivaApp` (no a `CargaMasiva` ni a `CargaMasivaDb`) y consulta al menos una de sus tablas mapeadas mediante EF Core + LINQ sin error.
2. **Given** la configuración de cada servicio, **When** se revisa, **Then** la cadena de conexión llega solo por variable de entorno (`ConnectionStrings__Default`), sin valores en archivos versionados.
3. **Given** una operación de datos que no puede conectar, **When** el servicio responde, **Then** lo hace con el contrato `Response<T>` de error genérico, sin exponer cadena de conexión, contraseña ni traza (manejo global de la Feature 002); esto se verifica de forma manual cuando exista un punto de ejecución adecuado.
4. **Given** el arranque de los servicios, **When** se revisa el esquema antes y después, **Then** ninguno crea ni modifica el esquema (sin migraciones; sin `__EFMigrationsHistory`).

---

### User Story 2 - Cada servicio tiene solo las entidades que necesita, con selección justificada (Priority: P1)

Cada servicio incorpora únicamente las entidades de la tabla "Distribución de entidades por microservicio", con su justificación y procedencia; no hay entidades fuera de esa tabla. [Decisión] [Constitución §6]

**Why this priority**: Es el alcance aprobado; evita volver al modelo completo en Control o a duplicarlo.

**Independent Test**: Comparar las entidades de cada servicio con la tabla de la spec y revisar que cada una tiene su justificación y procedencia.

**Acceptance Scenarios**:

1. **Given** cada servicio, **When** se listan sus entidades, **Then** coinciden exactamente con la columna de la tabla de distribución (ms-autenticacion: Usuario y Rol; ms-control: CargaArchivo, CargaEstadoHistorial e Incidencia; ms-carga-masiva: CargaArchivo, CargaPeriodo, Producto e Incidencia; ms-notificaciones: Incidencia).
2. **Given** los cuatro servicios, **When** se comparan, **Then** ninguno mapea las siete tablas y ninguna entidad existe en un servicio que no figura en la tabla.
3. **Given** cada entidad incorporada, **When** se revisa su justificación, **Then** cita la función del servicio y la procedencia ([README], [Constitución], [Feature 001/002] o [Decisión]); ninguna se justifica por una necesidad futura supuesta.
4. **Given** las exclusiones (CargaPeriodo y Producto en Control, Usuario en Carga Masiva y Notificaciones, CargaArchivo en Notificaciones, CargaEstadoHistorial en Carga Masiva), **When** se revisa el código, **Then** no existen esas entidades en esos servicios.

---

### User Story 3 - El mapeo corresponde al esquema existente (Priority: P1)

Las entidades y mapeos de cada servicio se obtienen por ingeniería inversa de las tablas existentes de `CargaMasivaApp` y corresponden a ellas; el modelo no redefine el esquema. [Decisión] [Constitución §6] [Feature 003]

**Why this priority**: Un mapeo distinto del esquema produce errores en tiempo de ejecución y viola la regla de que el esquema lo gestiona `database/`.

**Independent Test**: Para cada entidad de cada servicio, comparar su mapeo con el catálogo de `CargaMasivaApp` (columnas, tipos, longitudes, nulabilidad, claves, relaciones, generación de valores).

**Acceptance Scenarios**:

1. **Given** una entidad incorporada, **When** se compara con su tabla, **Then** el nombre de tabla y de columnas, los tipos y longitudes, la nulabilidad, la clave primaria y la generación de valores (identidad o no) coinciden con `V001`.
2. **Given** una relación entre dos entidades del mismo servicio, **When** se revisa, **Then** corresponde a una clave foránea real; **Given** una clave foránea hacia una tabla no mapeada en ese servicio (p. ej. `CargaArchivo.UsuarioId` en Control), **When** se revisa, **Then** se conserva como columna de clave sin introducir una entidad adicional.
3. **Given** `Producto.CodigoProducto`, **When** se consulta con LINQ, **Then** la comparación respeta la collation de la columna (sin distinguir mayúsculas de minúsculas y sin otras transformaciones) y el modelo no la sobrescribe. [Constitución §7]
4. **Given** el modelo de un servicio, **When** se revisa, **Then** no contiene datos base sembrados, creación de índices o restricciones, ni anotaciones para generar el esquema: solo describe el acceso a datos.
5. **Given** un servicio con sus entidades, **When** se compila, **Then** compila sin advertencias y el mapeo de cada tabla se verifica contra el catálogo creado por el runner.

---

### User Story 4 - Organización conforme a la referencia, sin sobreingeniería (Priority: P2)

Las entidades y los mapeos siguen la organización del repositorio de referencia con las adaptaciones aprobadas, sin añadir capas, abstracciones, dependencias, endpoints ni lógica. [Decisión] [Referencia] [Feature 002]

**Why this priority**: Mantiene la coherencia arquitectónica y evita ampliar el alcance.

**Independent Test**: Revisar la ubicación de entidades y mapeos, las dependencias de proyecto y el diff de la feature.

**Acceptance Scenarios**:

1. **Given** una entidad incorporada, **When** se revisa su ubicación, **Then** la entidad está en `Acity.Domain` y su mapeo en `Acity.Infrastructure/Persistence`, registrado en el `AppDbContext` del servicio, respetando `Api → Infrastructure → Application → {Common, Domain}`.
2. **Given** el diff de la feature, **When** se revisa, **Then** no hay controladores, comandos, consultas, handlers, endpoints, repositorios por entidad, `BaseEntity`, ni mapeos entre DTO; la fundación (`Response<T>`, errores, logs, `/health`) no cambia.
3. **Given** los `.csproj`, **When** se comparan con los de la Feature 003, **Then** no se añaden paquetes nuevos ni referencias entre servicios.
4. **Given** el código generado por ingeniería inversa, **When** se integra, **Then** no se conservan elementos que el servicio no usa ni se sobrescriben personalizaciones existentes de forma indiscriminada.

---

### Edge Cases

- Un servicio no puede conectar (SQL Server caído o credenciales inválidas): error genérico sin secretos; no se crea ni modifica el esquema; no hay reintento infinito propio.
- La base destino apunta por error a `CargaMasiva` o `CargaMasivaDb`: el esquema podría diferir (la primera es compatible; la segunda tiene el historial de EF): la conexión debe dirigirse a `CargaMasivaApp` (por `DB_NAME`).
- Una columna cambia en un script futuro de `database/`: el modelo del servicio debe ajustarse; esta feature solo cubre el esquema actual `V001`.
- Una tabla mapeada por varios servicios (`CargaArchivo`, `Incidencia`): cada servicio la mapea de forma independiente y solo con lo que usa; no se comparten clases entre servicios.
- `Rol.Id` no es identidad y los demás `Id` sí: el mapeo debe respetarlo.
- Los procedimientos `usp_ReservarPeriodo` y `usp_CambiarEstadoCarga` no son entidades; su invocación pertenece a las features funcionales.
- El motor SQL Server amd64 corre emulado en Apple silicon, sin soporte oficial de Microsoft; la verificación manual vale solo para el equipo donde se hizo.

## Requirements *(mandatory)*

### Functional Requirements

**Conexión**

- **FR-001**: Cada uno de los cuatro microservicios MUST conectarse a la base `CargaMasivaApp` mediante EF Core y SQL Server, con el contexto de datos registrado en `Acity.Infrastructure`. [Decisión] [Constitución §6] [Feature 002]
- **FR-002**: La cadena de conexión MUST proveerse por variable de entorno (`ConnectionStrings__Default`) y MUST NOT aparecer en archivos versionados, logs ni respuestas de error. [Constitución §6, §10] [Feature 002]
- **FR-003**: La base destino de los servicios MUST ser `CargaMasivaApp` (por `DB_NAME`); `CargaMasiva` y `CargaMasivaDb` se conservan y no se usan como destino. [Feature 003] [Decisión]
- **FR-004**: Ningún servicio MUST crear, modificar ni migrar el esquema al arrancar ni al usar sus entidades; el esquema sigue gestionado en `database/`. [Constitución §6] [Feature 003]
- **FR-005**: La conexión MUST poder comprobarse manualmente en cada servicio sin añadir endpoints, casos de negocio ni cambiar `GET /health`; el mecanismo concreto lo define el plan. [Decisión]
- **FR-006**: Ante un fallo de conexión, la respuesta MUST usar el contrato de error común sin exponer secretos. [Constitución §5, §10] [Feature 002]

**Selección de entidades**

- **FR-007**: Cada servicio MUST incorporar únicamente las entidades indicadas en la tabla "Distribución de entidades por microservicio" de esta spec. [Decisión]
- **FR-008**: MUST NOT concentrarse el modelo completo en Control ni duplicarse las siete entidades en todos los servicios por uniformidad. [Decisión] [Constitución §6]
- **FR-009**: Cada entidad incorporada MUST tener una justificación con la función del servicio y su procedencia; la selección MUST NOT apoyarse en necesidades futuras supuestas. [Decisión]
- **FR-010**: Una tabla usada por varios servicios MUST mapearse de forma independiente en cada uno (sin clases compartidas entre servicios); cada entidad mapea todas las columnas de su tabla y solo las relaciones hacia entidades que ese servicio también mapea. [Feature 002 FR-004] [Decisión] *(Precisado en el plan, D-05: sustituye "solo con las columnas que ese servicio usa".)*
- **FR-011**: MUST NOT incorporarse entidades para los procedimientos almacenados, ni para `SchemaMigraciones`. [Decisión] [Feature 003]

**Correspondencia con el esquema**

- **FR-012**: Las entidades y mapeos MUST obtenerse por ingeniería inversa de las tablas existentes de `CargaMasivaApp` y MUST coincidir con ellas en nombre de tabla y de columna, tipo, longitud, nulabilidad, clave primaria, generación de valores y relaciones. [Decisión] [Constitución §6]
- **FR-013**: Una clave foránea hacia una tabla no mapeada en el servicio MUST conservarse como columna de clave, sin añadir la entidad relacionada. [Decisión: no concentrar el modelo]
- **FR-014**: El modelo MUST NOT redefinir el esquema: sin datos base sembrados, sin definición de índices o restricciones para generar DDL y sin anotaciones solo para replicarlo. [Constitución §6] [Feature 003]
- **FR-015**: El mapeo de `Producto.CodigoProducto` MUST respetar la collation de la columna para que la comparación no distinga mayúsculas de minúsculas ni aplique otras transformaciones. [Constitución §7] [Feature 001]
- **FR-016**: Cualquier diferencia entre un mapeo y el esquema MUST registrarse y corregirse en el mapeo, no en el esquema. El esquema solo cambia con un script versionado de `database/`. [Constitución §6]

**Organización y límites**

- **FR-017**: Las entidades MUST ubicarse en `Acity.Domain` y sus mapeos y el contexto en `Acity.Infrastructure/Persistence`, siguiendo la organización de la referencia (entidad como clase, configuración por entidad aplicada por el contexto) con las adaptaciones aprobadas. [Referencia] [Constitución §4] [Decisión]
- **FR-018**: La ingeniería inversa MUST ser una operación de desarrollo: el código generado se revisa e integra respetando las capas, sin sobrescribir personalizaciones de forma indiscriminada y sin ejecutarse al arrancar ni en las imágenes. [Constitución §6] [Feature 003]
- **FR-019**: MUST NOT añadirse endpoints, controladores, comandos, consultas, handlers, repositorios por entidad, `BaseEntity`, abstracciones, mapeos entre DTO, lógica de negocio ni CRUD. [Decisión] [Constitución §4]
- **FR-020**: MUST NOT añadirse paquetes ni tecnologías; MUST NOT crearse referencias entre servicios. [Constitución §12] [Feature 002]
- **FR-021**: MUST conservarse la fundación (`Response<T>`, manejo global de errores, Serilog, `/health`, Dockerfiles, Compose) y las reglas de negocio de la Feature 001. [Decisión]
- **FR-022**: MUST NOT añadirse pruebas automatizadas, proyectos ni dependencias de pruebas; la verificación es manual. [Constitución §11]
- **FR-023**: MUST NOT modificarse la constitución, el README ni las bases, datos y volúmenes existentes (`CargaMasiva`, `CargaMasivaDb`). [Decisión]
- **FR-024**: El procedimiento de ingeniería inversa MUST apoyarse en lo documentado en la Feature 003 (`data-model.md`) y MAY ajustarse en el plan; los comandos y la organización técnica concretos MUST definirse en el plan. [Decisión]

### Key Entities

- **Conexión a CargaMasivaApp**: la cadena provista por entorno y el contexto de datos de cada servicio.
- **Distribución de entidades**: matriz servicio × tabla con la selección justificada (10 mapeos de 7 tablas).
- **Entidad de servicio**: clase de `Acity.Domain` que representa una tabla de `CargaMasivaApp` para un servicio, con su mapeo.
- **Mapeo**: descripción en `Acity.Infrastructure/Persistence` de cómo la entidad corresponde a la tabla existente; no define el esquema.
- **Tablas de negocio**: `Rol`, `Usuario`, `CargaArchivo`, `CargaEstadoHistorial`, `CargaPeriodo`, `Producto`, `Incidencia` (definidas en `V001`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 4 de 4 servicios abren una conexión a `CargaMasivaApp` y consultan una tabla de su conjunto sin error con EF Core + LINQ.
- **SC-002**: 0 cadenas de conexión o contraseñas en archivos versionados, logs o respuestas de error.
- **SC-003**: El 100 % de las entidades de cada servicio coincide con la tabla de distribución: 2, 3, 4 y 1 entidades en ms-autenticacion, ms-control, ms-carga-masiva y ms-notificaciones; 0 entidades fuera de la tabla; 0 servicios con las siete tablas.
- **SC-004**: El 100 % de las entidades tiene justificación con procedencia; 0 justificadas por necesidades futuras supuestas.
- **SC-005**: El 100 % de las columnas, tipos, longitudes, nulabilidad, claves y generación de valores de las entidades incorporadas coincide con el catálogo de `CargaMasivaApp` (0 diferencias sin registrar).
- **SC-006**: 0 datos base sembrados, definiciones de índices o restricciones, y 0 operaciones de esquema ejecutadas al arrancar (esquema y registro de scripts idénticos antes y después).
- **SC-007**: La comparación de `CodigoProducto` mediante LINQ iguala "p0117" con "P0117" y no iguala "á" con "a".
- **SC-008**: 0 endpoints, controladores, handlers, repositorios por entidad, `BaseEntity` o abstracciones nuevos; 0 paquetes nuevos; 0 referencias entre servicios.
- **SC-009**: Los cuatro servicios compilan sin advertencias y arrancan healthy con Compose.
- **SC-010**: 0 cambios en la constitución, el README, las bases `CargaMasiva` y `CargaMasivaDb` y sus volúmenes.

## Assumptions

- El esquema es el de la Feature 001 / `V001`; la base `CargaMasivaApp` creada por el runner es idéntica a `referencias/`. [Observado]
- La distribución de la tabla de esta spec parte de lo documentado: ms-autenticacion con `Usuario` y `Rol` (Feature 002 `data-model.md`), las responsabilidades por servicio de la Feature 001 (`research.md` R-09) y la división de escrituras del contrato de procedimientos; donde no era explícita se deduce de las funciones del README, con la procedencia indicada. [Decisión]
- Control cubre el seguimiento de cargas (estado, historial e incidencias por rol) según la constitución §2 y §6; su implementación funcional es de su feature.
- La verificación de la conexión se hace manualmente sin añadir endpoints; el plan define el mecanismo (p. ej., herramientas de desarrollo o una comprobación puntual fuera del repositorio).
- La conexión usa las credenciales de la fundación (`sa` desde `.env`); no se reabre esa decisión. [Feature 002]
- Las claves foráneas hacia tablas no mapeadas se mantienen como columnas de clave; la forma exacta (con o sin propiedades de navegación) se decide en el plan, sin añadir entidades.
- La verificación es manual; el motor amd64 emulado sin soporte de Microsoft limita lo comprobado a este equipo.

## Asuntos pendientes (no bloquean esta spec; no son requisitos aprobados)

- **Diferidos por decisión del plan** (se añadirán, con el procedimiento de ingeniería inversa, cuando sus contratos o casos de uso justifiquen su acceso): `Usuario` en Control (depende de los claims del JWT de la feature de Autenticación) y `Producto`/`CargaPeriodo` en Control (el README no asigna el servicio que sirve el contenido del archivo ni el detalle por periodo).
- ~~**Notificaciones y `CargaArchivo`**~~: resuelto en el plan (D-03): no se mapea; marcar Notificado se hace con el procedimiento `usp_CambiarEstadoCarga`, que recibe el identificador de la carga. El contenido del correo y la recuperación de notificaciones no publicadas siguen pendientes en la constitución y no se asumen.
- ~~**Mecanismo de verificación de la conexión**~~: resuelto en el plan (D-06): archivo `.cs` fuera del repositorio que usa el `DbContext` de cada servicio, sin endpoints ni cambios en `/health`.
- **Verificación de Docker y SQL Server**: motor amd64 emulado sin soporte oficial de Microsoft; solo se comprobará en este equipo; no se declara validado en el equipo de evaluación.
- Del repositorio de referencia no se leyeron controladores, handlers ni el resto de `Application`/`Api` (ver "Contexto").
