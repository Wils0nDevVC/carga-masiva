# Feature Specification: Reactivación del runner de database/ y refactorización de ms-control

**Feature Branch**: `003-runner-sql-refactor-control` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-02

**Status**: Draft

**Input**: Reactivar `database/` (scripts SQL versionados y runner ejecutado desde Docker Compose) y refactorizar `ms-control` conforme a la constitución v3.0.0: retirar EF Core Migrations y su ejecución al iniciar, retirar las entidades y mapeos creados solo para administrar el esquema global y establecer la ingeniería inversa con `dotnet ef dbcontext scaffold` para las tablas que cada servicio necesite. Una sola feature; esta etapa genera solo la especificación.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v3.0.0; `[Decisión]` = decisión explícita del usuario (incluye el prompt de esta feature); `[Feature 001]` = `specs/001-persistencia-integridad-carga/` (reglas de negocio, modelo y contratos de procedimientos, que **no cambian**); `[Feature 002]` = `specs/002-fundacion-microservicios/` (fundación implementada); `[Observado]` = comprobado leyendo el repositorio en esta etapa. Un requisito sin etiqueta propia no es una decisión aprobada.

## Contexto

Estado observado hoy en el repositorio: [Observado]

- `database/` (versionado en git) contiene `scripts/V001__esquema_inicial.sql` y `V002__procedimientos.sql` (idénticos a `referencias/`), `bootstrap.sql`, `run-scripts.sh` y un `Dockerfile` que reutiliza la imagen de SQL Server para disponer de `sqlcmd`. El runner espera a SQL Server, crea la base y la tabla `SchemaMigraciones`, aplica en orden los scripts `Vnnn__*.sql` aún no registrados (cada uno en su transacción, con cerrojo de sesión), registra nombre y checksum, y falla si un script aplicado cambió o si uno nuevo tiene número menor que el último aplicado. Su nombre de base por defecto es `CargaMasiva`.
- `compose.yaml` declara SQL Server y los cuatro microservicios; el runner no figura en él. `ms-control` espera a SQL Server y los otros tres esperan a que `ms-control` esté `healthy`.
- `ms-control` (implementación de la Feature 002) contiene el modelo completo de la base compartida: 7 entidades en `Acity.Domain/Entities`, 7 configuraciones, `AppDbContext`, las migraciones EF Core `InitialSchema` y `StoredProcedures`, `DatabaseInitializer` (espera de SQL Server, guarda contra base heredada y aplicación de migraciones antes de escuchar) y la llamada a este en `Program.cs`, además de `dotnet-ef` como herramienta local y el paquete `Design`. No existe todavía ningún endpoint, comando, consulta ni repositorio de negocio que use las entidades: solo `GET /health`.
- Existen en este equipo dos bases en la instancia: `CargaMasiva` (creada por el runner; objetos de V001/V002 y `SchemaMigraciones`) y `CargaMasivaDb` (creada por las migraciones EF; con `__EFMigrationsHistory`). Cada una tiene su propio historial. [Observado] [Feature 002]

La constitución v3.0.0 sustituyó la gestión del esquema con EF Core Migrations por scripts versionados y un runner. Esta feature lo materializa. [Decisión] [Constitución §2, §6, §11]

**Decisiones anteriores que esta feature sustituye** (las reglas de negocio, el esquema aprobado y los contratos de procedimientos no se tocan):

| Decisión anterior | Dónde está | Pasa a ser |
|---|---|---|
| Control es único propietario de las migraciones EF Core y las aplica al iniciar | Feature 002 FR-025, FR-029, FR-030; US2; Constitución v2.0.0/v2.1.0 §6 | Las aplica el runner de `database/`; ningún microservicio toca el esquema al arrancar |
| La migración inicial y los procedimientos se generan como migraciones EF (`InitialSchema`, `StoredProcedures`) | Feature 002 FR-026, FR-027 | Los materializan `V001` y `V002` ejecutados por el runner |
| `database/` y su runner fuera del flujo activo; no reactivarlo | Feature 002 FR-033; Feature 001 notas de estrategia sustituida | `database/` es componente activo [Constitución §2] |
| Historial `__EFMigrationsHistory`; `SchemaMigraciones` fuera del modelo | Feature 002 FR-028 | El historial es `SchemaMigraciones` del runner; no forma parte del modelo EF de los servicios |
| Guarda de Control contra la base heredada (FR-034) | Feature 002 FR-034 | Protección equivalente en el runner: no mezclar historiales (ver FR-013) |
| Orden SQL Server → Control (migra y queda healthy) → los otros tres | Feature 002 FR-038, FR-039; US3; SC-009 | SQL Server → runner completado correctamente → los cuatro microservicios [Constitución §11] |
| `/health` de Control indica listo solo tras migrar | Feature 002 FR-022 | `/health` de Control indica que el host arrancó, igual que los demás; el esquema lo garantiza el orden de Compose |
| Control posee el modelo completo de la base | Feature 002 FR-025; Constitución v2.0.0 §6 | Cada servicio solo tiene las entidades y mapeos que necesita funcionalmente [Constitución §6] |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - El runner crea y versiona el esquema al levantar Compose (Priority: P1)

Al levantar Docker Compose sobre una base nueva y vacía, un runner de `database/` espera a SQL Server, aplica en orden los scripts SQL versionados y registra cada uno al completarse; al terminar correctamente, el esquema aprobado y los dos procedimientos están creados. [Decisión] [Constitución §6, §11] [README: migraciones automáticas, procedimientos almacenados, scripts de base de datos]

**Why this priority**: Es el mecanismo que pasa a ser único responsable del esquema; sin él ningún servicio dispone de las tablas.

**Independent Test**: Levantar SQL Server y el runner contra un nombre de base nuevo y vacío y comparar el catálogo con `referencias/V001` y `V002`; repetir el arranque y comprobar que no se reaplica nada.

**Acceptance Scenarios**:

1. **Given** SQL Server aún no disponible, **When** arranca el runner, **Then** espera su disponibilidad hasta un límite acotado y, si no se alcanza, termina con error sin crear nada.
2. **Given** una base nueva y vacía, **When** el runner termina correctamente, **Then** existen las 7 tablas de negocio del modelo aprobado con sus relaciones, índices, restricciones y los dos roles base, los procedimientos `usp_ReservarPeriodo` y `usp_CambiarEstadoCarga`, y el registro de scripts aplicados con cada script y su checksum.
3. **Given** el esquema ya aplicado y sin scripts nuevos, **When** el runner corre otra vez, **Then** informa que no hay versiones pendientes, no ejecuta ningún script y no modifica la base ni su registro.
4. **Given** un nuevo script versionado con número mayor, **When** el runner corre, **Then** aplica solo ese script, lo registra y no reejecuta los anteriores.
5. **Given** un script aplicado cuyo contenido cambió, o un script nuevo con número menor que el último aplicado, **When** el runner corre, **Then** termina con error, no aplica ningún script pendiente y no reescribe el registro en silencio.
6. **Given** un script que falla a mitad, **When** el runner corre, **Then** el script no queda registrado, sus cambios se revierten, el runner termina con error y los scripts anteriores conservan su registro.
7. **Given** dos ejecuciones simultáneas del runner sobre la misma base, **When** ambas intentan aplicar versiones, **Then** solo una aplica y la otra no duplica ni corrompe nada (comportamiento ya cubierto por el cerrojo del runner; se verifica en esta feature).
8. **Given** una ejecución del runner, **When** se revisan su salida y los registros, **Then** no aparece la contraseña de SQL Server ni cadenas de conexión.

---

### User Story 2 - Compose arranca los microservicios solo si el runner termina bien (Priority: P1)

El Compose de la raíz impone el orden SQL Server disponible → runner completado correctamente → microservicios. Si el runner falla, los microservicios no arrancan. [Decisión] [Constitución §11]

**Why this priority**: Garantiza que ningún servicio se ejecute sobre un esquema inexistente o parcial, y reemplaza el papel de Control como propietario del esquema.

**Independent Test**: Levantar Compose sobre una base nueva y observar el orden y el estado final; repetir forzando el fallo de un script y comprobar que ningún microservicio arranca.

**Acceptance Scenarios**:

1. **Given** Compose sobre una base nueva y vacía, **When** se levanta, **Then** SQL Server pasa a saludable, el runner se ejecuta y termina con éxito, y solo entonces arrancan `ms-control`, `ms-autenticacion`, `ms-carga-masiva` y `ms-notificaciones`.
2. **Given** un runner que termina con error, **When** Compose evalúa las dependencias, **Then** ningún microservicio arranca y el fallo queda visible en el estado y los logs del runner.
3. **Given** un Compose levantado antes, **When** se levanta de nuevo sin scripts nuevos, **Then** el runner termina de inmediato sin pendientes y los microservicios arrancan.
4. **Given** los microservicios arrancados, **When** se revisa el esquema antes y después de su arranque, **Then** ninguno lo creó ni lo modificó.
5. **Given** el Compose existente, **When** se amplía con el runner, **Then** conserva SQL Server, sus volúmenes, los volúmenes de logs y los recursos ajenos al proyecto, y no se añade otro microservicio ni un ejecutable .NET para migraciones. [Decisión] [Constitución §11]
6. **Given** el arranque real, **When** no se ha comprobado, **Then** Docker no se declara funcional (ver Asuntos pendientes).

---

### User Story 3 - ms-control sin EF Core Migrations y sin migrar al iniciar (Priority: P1)

`ms-control` deja de contener y ejecutar EF Core Migrations. Arranca directamente, sin esperar ni modificar el esquema, y mantiene la fundación (estructura de capas, `Response<T>`, manejo global de errores, logs, `GET /health`, Dockerfile). [Decisión] [Constitución §6] [Feature 002]

**Why this priority**: Es la mitad de la refactorización: mientras Control migre al iniciar, hay dos responsables del esquema.

**Independent Test**: Compilar Control, buscar referencias a migraciones y al inicializador, arrancarlo contra una base ya creada por el runner y comprobar que no aplica nada ni escribe en `__EFMigrationsHistory`.

**Acceptance Scenarios**:

1. **Given** `ms-control` refactorizado, **When** se revisa su código y su proyecto, **Then** no existen migraciones EF Core ni su instantánea, ni el inicializador ni su invocación en `Program.cs`, ni código que cree, migre o compruebe el esquema al arrancar.
2. **Given** una base creada por el runner, **When** arranca `ms-control`, **Then** escucha sin ejecutar ninguna operación de esquema, responde `GET /health` con 200 y no crea `__EFMigrationsHistory`.
3. **Given** `ms-control` refactorizado, **When** se compila por su ruta, **Then** compila sin advertencias nuevas, sin referencias entre servicios y sin paquetes no autorizados, y conserva sus capas, `Response<T>`, manejo global de excepciones, Serilog y Dockerfile.
4. **Given** los otros tres servicios, **When** se revisan, **Then** no contienen migraciones ni inicializadores de esquema (ya sin ellos desde la Feature 002) y siguen compilando y arrancando.

---

### User Story 4 - Modelo de Control limitado a lo que necesita (Priority: P2)

Se revisan los usos de las siete entidades y sus mapeos en `ms-control`; se retiran únicamente las entidades y mapeos creados para administrar el esquema global, y se conservan las necesarias para las funciones de Control (recepción de archivos y seguimiento de cargas). [Decisión] [Constitución §2, §6]

**Why this priority**: Elimina el modelo completo que sobraba solo para migrar, sin romper lo que Control necesita ni inventar funcionalidad.

**Independent Test**: Listar cada entidad con su uso real (dónde se referencia y qué función de Control la requiere) y comprobar que el resultado compila, que lo retirado no tenía usos y que lo conservado coincide con las tablas reales.

**Acceptance Scenarios**:

1. **Given** las siete entidades y sus configuraciones, **When** se revisan, **Then** existe un resultado trazable (entidad → uso o función de Control que la requiere → conservar o retirar) y cada retiro cita la ausencia de usos y de necesidad funcional.
2. **Given** una entidad o mapeo sin uso en Control y sin función de Control que lo requiera, **When** se aplica la limpieza, **Then** se retira junto con su configuración, y el resto compila sin referencias rotas.
3. **Given** una entidad conservada, **When** se revisa su mapeo, **Then** corresponde a la tabla real creada por el script (columnas, tipos, nulabilidad y relaciones), sin redefinir el esquema: sin nombres explícitos de restricciones creados para generar el DDL, sin datos base sembrados desde el modelo, sin la convención de índices de claves foráneas retirada ni anotaciones añadidas solo para replicar el DDL.
4. **Given** el trabajo existente de Control (fundación, repositorio y Unit of Work mínimos, `AppDbContext`, `/health`), **When** se compila y se arranca, **Then** sigue funcionando sin cambios de comportamiento.
5. **Given** una entidad sin referencias ni función de Control implementada o aprobada ahora, **When** se revisa, **Then** se retira; no se conserva ni se agrega por una necesidad futura supuesta, y se regenera con la ingeniería inversa cuando una feature funcional la necesite. *(Precisado en el plan, D-04: sustituye el criterio inicial de conservar ante la duda.)*

---

### User Story 5 - Ingeniería inversa para los modelos de cada servicio (Priority: P2)

Se establece cómo obtener, durante el desarrollo, el modelo EF de cada servicio con `dotnet ef dbcontext scaffold` a partir de la base ya creada por el runner, limitado a las tablas que ese servicio necesita, e integrarlo respetando las capas. [Decisión] [Constitución §6]

**Why this priority**: Fija el método para todas las features funcionales posteriores y evita volver a un modelo completo por servicio.

**Independent Test**: Ejecutar el procedimiento documentado contra una base creada por el runner para las tablas de Control y comprobar que genera solo esas tablas, que el resultado se integra en las capas aprobadas y que no se ejecuta al arrancar.

**Acceptance Scenarios**:

1. **Given** una base creada por el runner, **When** se aplica el procedimiento para un servicio, **Then** se generan únicamente las entidades y mapeos de las tablas que ese servicio necesita, sin CRUD ni lógica adicional.
2. **Given** código generado, **When** se integra, **Then** las entidades quedan en `Acity.Domain`, el contexto y los mapeos en `Acity.Infrastructure`, sin dependencias invertidas, y las personalizaciones existentes no se sobrescriben indiscriminadamente.
3. **Given** el arranque de cualquier servicio, **When** se revisa, **Then** la ingeniería inversa no forma parte del arranque ni del Dockerfile; es una operación de desarrollo.
4. **Given** una tabla que un servicio no necesita, **When** se aplica el procedimiento, **Then** no se genera su entidad en ese servicio.
5. **Given** el procedimiento, **When** se documenta, **Then** indica los requisitos mínimos del equipo de desarrollo (herramienta y paquete de diseño que ya usa Control) sin añadir tecnologías nuevas.

---

### User Story 6 - Validación sobre una base nueva sin mezclar historiales (Priority: P2)

La validación del runner se hace sobre una base nueva y vacía, sin mezclar su historial con el de las bases existentes. [Decisión]

**Why this priority**: Evita falsos resultados y protege los datos existentes.

**Independent Test**: Ejecutar el runner con un nombre de base nuevo, comprobar el resultado y verificar que las bases existentes y sus historiales no cambiaron.

**Acceptance Scenarios**:

1. **Given** una base nueva y vacía con un nombre distinto de las existentes, **When** se valida el runner, **Then** el resultado cumple la User Story 1 y las bases existentes (`CargaMasiva`, `CargaMasivaDb`) y sus volúmenes no cambian.
2. **Given** una base que ya contiene objetos del esquema sin registro del runner (por ejemplo, creada con EF Core Migrations y con `__EFMigrationsHistory`), **When** el runner corre sobre ella, **Then** termina con un mensaje claro sin modificarla ni inventar un historial, y los microservicios no arrancan.
3. **Given** la base `CargaMasiva` creada por el runner anterior, **When** se revisa su relación con el runner, **Then** no se modifica durante la validación y su uso operativo se resuelve en el plan (ver Asuntos pendientes).
4. **Given** el esquema creado por el runner, **When** se repiten los escenarios de base de la Feature 001 (reserva, índice filtrado, comparación de códigos, matriz de estados, fallo terminal), **Then** los resultados coinciden con los ya obtenidos.

---

### Edge Cases

- SQL Server no responde: el runner agota el límite de espera y falla; Compose no arranca los servicios.
- Un script falla a mitad: se revierte, no se registra, el runner sale con error y los servicios no arrancan; el estado de scripts anteriores se conserva.
- Se renombra o edita un script ya aplicado, o se añade uno con número menor: el runner falla explícitamente.
- Dos ejecuciones del runner a la vez (por ejemplo, dos `compose up`): una aplica, la otra espera o falla sin duplicar.
- La base operativa contiene objetos creados por otro mecanismo (EF Core Migrations): el runner no los toca ni mezcla historiales.
- Reinicio de microservicios con el runner ya completado: el runner termina sin pendientes y los servicios arrancan sin tocar el esquema.
- Una entidad de Control parece sin uso porque aún no hay funciones implementadas: se aplica el criterio de funciones de Control del README, la constitución y la Feature 001; no se retira por falta de código.
- El motor SQL Server amd64 corre emulado en Apple silicon, sin soporte oficial de Microsoft; la verificación manual vale solo para el equipo donde se hizo.
- Los scripts del runner usan la imagen de SQL Server amd64 para disponer de `sqlcmd`; la validación se limita a este equipo.

## Requirements *(mandatory)*

### Functional Requirements

**Runner y scripts versionados**

- **FR-001**: Debe existir un runner en `database/`, ejecutado desde Docker Compose, que espere a SQL Server, aplique en orden los scripts SQL versionados de `database/scripts/` y termine con éxito solo si todos los pendientes se aplicaron. [Decisión] [Constitución §6, §11]
- **FR-002**: El runner MUST aplicar únicamente las versiones pendientes y registrar cada versión completada con su nombre y checksum. [Decisión] [Constitución §6]
- **FR-003**: Cada script MUST aplicarse de forma atómica: si falla, sus cambios se revierten, no se registra y el runner termina con error. [Decisión] [Observado: comportamiento del runner actual]
- **FR-004**: El runner MUST fallar sin aplicar nada si un script ya aplicado cambió de contenido o si un script nuevo tiene un número menor que el último aplicado. [Observado: comportamiento actual, se conserva]
- **FR-005**: El runner MUST impedir que dos ejecuciones simultáneas apliquen scripts a la vez sobre la misma base. [Observado: cerrojo actual, se conserva]
- **FR-006**: La espera de SQL Server MUST estar acotada y terminar con error si no se alcanza. [Decisión: esperar a SQL Server] [Feature 002 research: espera acotada]
- **FR-007**: Los scripts MUST materializar exactamente el esquema aprobado de la Feature 001 (7 tablas, relaciones, índices, restricciones, roles base) y los procedimientos `usp_ReservarPeriodo` y `usp_CambiarEstadoCarga`; los contenidos de `V001` y `V002` coinciden con `referencias/`. Cualquier cambio posterior al esquema se hace con un script nuevo. [Decisión] [Feature 001]
- **FR-008**: Los secretos MUST proveerse al runner por variables de entorno y MUST NOT aparecer en su salida, en archivos versionados ni en el registro de scripts. [Constitución §6, §10] [Observado: runner actual]
- **FR-009**: Las reglas de negocio, el esquema aprobado, los contratos de procedimientos, los roles, las contraseñas y los usuarios iniciales de la Feature 001 y la constitución MUST conservarse; esta feature no los modifica. [Decisión]

**Docker Compose**

- **FR-010**: Compose MUST ejecutar el runner después de que SQL Server esté saludable y antes de arrancar los microservicios. [Decisión] [Constitución §11]
- **FR-011**: Los cuatro microservicios MUST arrancar solo cuando el runner haya terminado correctamente; si el runner falla, MUST NOT arrancar los dependientes. [Decisión] [Constitución §11]
- **FR-012**: El runner MUST ser un contenedor de ejecución única (no un microservicio ni un ejecutable .NET): MUST NOT añadirse otro microservicio ni un proyecto .NET para migraciones. [Decisión] [Constitución §11]
- **FR-013**: Si la base destino contiene objetos del esquema sin registro del runner (por ejemplo, un historial `__EFMigrationsHistory` de EF Core Migrations), el runner MUST terminar con un mensaje claro sin modificar esa base ni inventar un historial. [Decisión: no mezclar historiales]
- **FR-014**: Compose MUST conservar SQL Server, sus volúmenes, los volúmenes de logs y los recursos ajenos al proyecto; MUST NOT eliminarse bases, datos ni volúmenes. [Decisión]
- **FR-015**: Docker MUST NOT declararse funcional hasta comprobar el arranque real. [Decisión] [Feature 002 FR-041]

**Refactorización de ms-control**

- **FR-016**: `ms-control` MUST NOT contener EF Core Migrations (clases de migración, diseñadores ni instantánea del modelo) ni ejecutarlas. [Decisión] [Constitución §6]
- **FR-017**: `ms-control` MUST NOT esperar a SQL Server para migrar, comprobar bases heredadas ni modificar el esquema al arrancar; se retira el inicializador y su invocación en `Program.cs`. [Decisión] [Constitución §6]
- **FR-018**: Ningún microservicio MUST crear o modificar el esquema al arrancar. [Constitución §6]
- **FR-019**: `GET /health` de `ms-control` MUST indicar que el host arrancó, con el mismo comportamiento que en los demás servicios; la disponibilidad del esquema la garantiza el orden de Compose. [Decisión derivada de FR-011]
- **FR-020**: La refactorización MUST conservar la fundación de la Feature 002: estructura de capas, referencias, `Response<T>`, manejo global de errores, FluentValidation y MediatR, repositorio y Unit of Work mínimos, Serilog y volúmenes de logs, Dockerfile y cadena de conexión por variable de entorno. [Decisión] [Feature 002]
- **FR-021**: Se MUST revisar el uso de cada entidad y mapeo de `ms-control`; MUST retirarse únicamente lo creado para administrar el esquema global (modelo completo, datos base sembrados desde el modelo, nombres de restricciones e índices, convenciones y anotaciones añadidas solo para replicar el DDL) y MUST conservarse lo necesario para las funciones de Control. [Decisión]
- **FR-022**: La revisión MUST dejar un resultado trazable por entidad (uso real, función de Control que la requiere o su ausencia, conservar o retirar). La evidencia para conservar una entidad es una referencia actual o una función de Control implementada o aprobada ahora; MUST NOT conservarse ni agregarse entidades por necesidades futuras supuestas. [Decisión: plan]
- **FR-023**: Las entidades conservadas MUST corresponder a las tablas reales creadas por el runner; MUST NOT redefinir el esquema desde el modelo ni sembrar datos base. [Decisión] [Constitución §6]
- **FR-024**: MUST NOT eliminarse bases, datos ni volúmenes existentes al retirar migraciones; la limpieza afecta solo al código y a la configuración de Control. [Decisión]

**Ingeniería inversa**

- **FR-025**: MUST establecerse y documentarse el procedimiento de ingeniería inversa con `dotnet ef dbcontext scaffold` como operación de desarrollo, a partir de la base creada por el runner y limitado a las tablas necesarias de cada servicio. [Decisión] [Constitución §6]
- **FR-026**: El código generado MUST revisarse e integrarse respetando las capas aprobadas (entidades en Domain; contexto y mapeos en Infrastructure) sin sobrescribir personalizaciones de forma indiscriminada, sin CRUD ni lógica adicional. [Decisión] [Constitución §4, §6]
- **FR-027**: La ingeniería inversa MUST NOT ejecutarse al arrancar ni formar parte de las imágenes Docker. [Decisión]
- **FR-028**: Cada servicio MUST incorporar únicamente las entidades y mapeos que necesita funcionalmente. Para `ms-autenticacion`, `ms-carga-masiva` y `ms-notificaciones`, que hoy no tienen funciones, el procedimiento queda establecido y se aplica en su feature funcional; esta feature no les añade entidades. [Decisión] [Feature 002 FR-032]
- **FR-029**: El procedimiento MUST usar solo la herramienta y paquetes ya presentes en Control (`dotnet-ef` local y el paquete de diseño); MUST NOT añadir tecnologías ni paquetes nuevos sin aprobación. [Decisión] [Constitución §12]

**Validación y límites**

- **FR-030**: La validación del runner MUST realizarse sobre una base nueva y vacía con un nombre distinto de las bases existentes, sin mezclar historiales, y MUST dejar intactas las bases y volúmenes existentes. [Decisión]
- **FR-031**: MUST NOT añadirse tecnologías, funcionalidades, endpoints, CRUD, ni pruebas automatizadas, proyectos de tests o dependencias de testing; la verificación es manual. [Decisión] [Constitución §11]
- **FR-032**: MUST NOT modificarse `referencias/`, `referencias/README.md` ni los documentos de las features 001 y 002 en esta etapa; su actualización forma parte de la implementación o de una etapa posterior. [Decisión]
- **FR-033**: MUST NOT reabrirse decisiones aprobadas (base compartida única, EF Core + LINQ, Clean Architecture, cuatro microservicios con `Acity.Api`, sin Dapper); los detalles menores se resuelven con lo existente. [Decisión]

### Key Entities

- **Script versionado**: archivo SQL numerado (`Vnnn__descripcion.sql`) en `database/scripts/` que describe un cambio del esquema o de los procedimientos; una vez aplicado no se edita.
- **Runner de scripts**: contenedor de ejecución única que espera a SQL Server, aplica los scripts pendientes en orden y registra cada uno; su éxito condiciona el arranque de los servicios.
- **Registro de scripts aplicados**: tabla que conserva el nombre, el checksum y la fecha de cada script aplicado; no forma parte del modelo EF de los servicios.
- **Esquema compartido**: base SQL Server única con las 7 tablas y 2 procedimientos aprobados en la Feature 001; lo crea y versiona solo el runner.
- **Modelo EF de un servicio**: entidades y mapeos de las tablas que ese servicio necesita, obtenidos por ingeniería inversa; no define el esquema.
- **Resultado de revisión de Control**: tabla entidad → uso → función de Control que la requiere → conservar/retirar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Sobre una base nueva y vacía, el runner termina con éxito y deja las 7 tablas, sus índices y restricciones, los 2 roles base y los 2 procedimientos, con una comparación de catálogo contra `referencias/V001` y `V002` sin diferencias.
- **SC-002**: Una segunda ejecución del runner sobre la misma base ejecuta 0 scripts, no modifica el esquema ni su registro y termina con éxito.
- **SC-003**: Al añadir un script con número mayor, el runner ejecuta exactamente 1 script (el nuevo) y 0 de los anteriores.
- **SC-004**: Con un script aplicado modificado, o uno nuevo de número menor, el runner termina con error y aplica 0 scripts.
- **SC-005**: Con un script fallido, 0 de sus cambios permanecen, no se registra y 0 microservicios arrancan.
- **SC-006**: En Compose, el orden SQL Server → runner → 4 microservicios se cumple en el 100 % de los arranques comprobados, y con el runner fallido 0 microservicios arrancan.
- **SC-007**: 0 microservicios modifican el esquema al arrancar (esquema y registro idénticos antes y después).
- **SC-008**: `ms-control` tiene 0 archivos de migraciones EF, 0 referencias al inicializador de esquema y 0 llamadas de migración en su arranque; compila y `GET /health` responde 200.
- **SC-009**: El 100 % de las entidades de Control figura en el resultado de revisión con su decisión y evidencia; 0 entidades retiradas con usos o sin documentar; 0 referencias rotas.
- **SC-010**: El procedimiento de ingeniería inversa genera solo las tablas indicadas para un servicio y no se ejecuta al arrancar ni en las imágenes.
- **SC-011**: 0 bases, datos o volúmenes existentes eliminados o modificados por la validación; el runner sobre una base con objetos sin registro termina con error y 0 cambios.
- **SC-012**: 0 paquetes no autorizados y 0 proyectos o dependencias de pruebas.

## Assumptions

- **Reglas de negocio y esquema**: son los de la Feature 001 y `referencias/V001`/`V002`; los scripts de `database/scripts/` ya coinciden con ellos (idénticos en el repositorio) y el runner existente es el punto de partida; su adaptación y validación se deciden en el plan. [Observado] [Decisión]
- **Registro de scripts**: se conserva `SchemaMigraciones` como registro del runner. [Observado]
- **Nombre de la base operativa**: una base nueva y vacía (`CargaMasivaApp`, plan D-02) sirve al runner y a los cuatro servicios; `CargaMasiva` y `CargaMasivaDb` se conservan sin cambios. No se elimina ni se renombra ninguna base existente. [Decisión]
- **Entidades de Control**: la constitución y el README asignan a Control la recepción de archivos y el seguimiento de cargas (consultas de cargas e incidencias por rol). Sin código funcional que las use hoy, el plan (D-04) comprueba que ninguna entidad tiene referencias y las retira todas, sin conservar entidades por necesidades futuras supuestas. [Constitución §2] [Observado] [Decisión: plan]
- **Servicios sin funciones**: `ms-autenticacion`, `ms-carga-masiva` y `ms-notificaciones` mantienen su `AppDbContext` sin entidades hasta su feature funcional. [Feature 002 FR-032]
- **Herramientas de ingeniería inversa**: `dotnet ef dbcontext scaffold` requiere el paquete de diseño y la herramienta ya presentes en Control; el plan define dónde se ejecuta para el resto de servicios sin añadir nada nuevo. [Observado]
- **Instancia única del runner**: una ejecución por `compose up`; la protección contra ejecuciones simultáneas ya existe y se verifica. [Observado]
- **README**: "migraciones automáticas" se interpreta como la aplicación automática de versiones pendientes al levantar Compose; el README no exige EF Core Migrations. No hay contradicción con el README. [README Base de datos] [Constitución §6]
- La verificación es manual; no se crean suites de pruebas. [Constitución §11]

## Asuntos pendientes (no son requisitos aprobados)

- ~~**Base operativa**~~: resuelto en el plan (D-02): base nueva y vacía `CargaMasivaApp`; las dos existentes se conservan.
- ~~**Clasificación de entidades de Control**~~: resuelto en el plan (D-04): las siete se retiran (0 referencias; sin función actual de Control).
- **Verificación operativa de Docker y SQL Server**: la imagen es amd64 y corre emulada en Apple silicon, sin soporte oficial de Microsoft; no se declara Docker funcional hasta validar el flujo real. La validación vale solo para este equipo.
- **Documentos de las features 001 y 002**: quedan afectados (estrategia de migraciones EF, orden de Compose, `/health` de Control, guarda de base heredada, `quickstart`); se actualizan en una etapa posterior, no aquí.
- **Constitución**: la lista de Pendientes conserva, sin resolver, los asuntos ajenos a esta feature (serialización de `Response<T>`, retención de logs, parámetros de Polly, entre otros). [Constitución Pendientes]
