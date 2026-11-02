# Tasks: Fundación común de los cuatro microservicios

**Input**: `specs/002-fundacion-microservicios/` (plan.md, spec.md, research.md, data-model.md, contracts/http-y-operacion.md, quickstart.md)

**Alcance**: los cuatro `ms-*/` y `compose.yaml`/`.env.example` de la raíz. No se tocan `database/`, `referencias/`, `api-gateway/` ni la constitución. Sin pruebas automatizadas, proyectos de tests ni funcionalidades nuevas (login, Excel, RabbitMQ, SeaweedFS, correo, Polly). Verificación manual con `quickstart.md`.

**Reglas de ejecución**
- **No dar por comprobado** lo marcado "Sin verificar" en research (bash en la imagen, permisos de volumen, `dotnet ef` y el código posterior a `Build()`): cada uno tiene una tarea de comprobación y, si falla, su solución mínima compatible con la arquitectura aprobada.
- **No modificar recursos ajenos**: la base `CargaMasiva` anterior y el volumen `carga-masiva_sqlserver-data` se conservan; en este equipo hay contenedores y volúmenes de otros proyectos que no se tocan. Nunca usar `docker compose down -v` ni borrar volúmenes. Las bases de prueba (`CargaMasivaDbPrueba`, `CargaMasivaDbFallo`) se crean y eliminan solo dentro de la instancia de este proyecto.
- Marcar `[x]` solo tareas realmente ejecutadas y verificadas; si una verificación falla, registrar el error concreto y aplicar la solución mínima.
- Sin secretos en archivos versionados. Contraseña solo por `.env` (no versionado).

**Formato**: `- [ ] ID [P] [US] Descripción con ruta`. `[P]` = archivos distintos y sin dependencia pendiente (normalmente, servicios distintos). Notación: `<s>` = cada uno de `ms-autenticacion`, `ms-control`, `ms-carga-masiva`, `ms-notificaciones`.

## Phase 1: Setup (estructura y paquetes)

- [ ] T001 [P] Crear `ms-control/src/Acity.Control.slnx` y los proyectos .NET 10 `Acity.Api` (SDK Web), `Acity.Application`, `Acity.Domain`, `Acity.Infrastructure` y `Acity.Common`, con referencias `Api → Infrastructure → Application → {Common, Domain}`; Domain y Common sin referencias; paquetes con versión exacta: Application = MediatR 12.5.0, FluentValidation 12.2.0, FluentValidation.DependencyInjectionExtensions 12.2.0; Infrastructure = Microsoft.EntityFrameworkCore.SqlServer 10.0.14; Api = Serilog.AspNetCore 10.0.0 y Microsoft.EntityFrameworkCore.Design 10.0.14 con `PrivateAssets=all` (solo Control)
- [ ] T002 [P] Crear `ms-autenticacion/src/Acity.Autenticacion.slnx` con la misma estructura, referencias y paquetes que T001, **sin** el paquete `Design`
- [ ] T003 [P] Crear `ms-carga-masiva/src/Acity.CargaMasiva.slnx` con la misma estructura, referencias y paquetes que T002
- [ ] T004 [P] Crear `ms-notificaciones/src/Acity.Notificaciones.slnx` con la misma estructura, referencias y paquetes que T002
- [ ] T005 Crear `ms-control/.config/dotnet-tools.json` con la herramienta local `dotnet-ef` 10.0.14 y ejecutar `dotnet tool restore` desde `ms-control/`

## Phase 2: Foundational (contrato común, bloquea las historias)

- [ ] T006 [P] Crear `ms-control/src/Acity.Common/Helpers/Response.cs`: `record Response<T>` con `Data` (nullable), `IsSuccess`, `Message`, `Code` y `Errors` (colección de mensajes **nullable**), y dos fábricas estáticas mínimas (éxito y fallo); sin referencias a ASP.NET
- [ ] T007 [P] Crear `ms-autenticacion/src/Acity.Common/Helpers/Response.cs` igual que T006
- [ ] T008 [P] Crear `ms-carga-masiva/src/Acity.Common/Helpers/Response.cs` igual que T006
- [ ] T009 [P] Crear `ms-notificaciones/src/Acity.Common/Helpers/Response.cs` igual que T006

## Phase 3: US1 - Estructura común y compilable por servicio (P1)

**Meta**: los cuatro servicios con la misma organización, compilables e independientes.
**Prueba independiente**: quickstart 1 y 2.

- [ ] T010 [P] [US1] En `ms-control/src/Acity.Application/`: `Interfaces/Repositories/IGenericRepository.cs` (`TEntity : class`; operaciones **solo**: `AsQueryable`, `AsNoTracking`, `FindAsync`, `CreateAsync`, `Update`, `Delete`) e `IUnitOfWork.cs` (**solo** `SaveChangesAsync`); `Behaviors/ValidationBehavior.cs` (comportamiento abierto de MediatR que ejecuta los validadores y lanza `ValidationException` de FluentValidation si hay fallos); `DependencyInjection.cs` con `AddApplication` (MediatR desde el ensamblado, `AddValidatorsFromAssembly`, registro abierto del `ValidationBehavior`). Sin operaciones masivas, truncado, transacciones ni repositorios por entidad; sin AutoMapper
- [ ] T011 [P] [US1] Igual que T010 en `ms-autenticacion/src/Acity.Application/`
- [ ] T012 [P] [US1] Igual que T010 en `ms-carga-masiva/src/Acity.Application/`
- [ ] T013 [P] [US1] Igual que T010 en `ms-notificaciones/src/Acity.Application/`
- [ ] T014 [P] [US1] En `ms-control/src/Acity.Infrastructure/`: `Persistence/AppDbContext.cs` (con `ApplyConfigurationsFromAssembly`, sin `DbSet` por entidad), `Repositories/GenericRepository.cs`, `Repositories/UnitOfWork.cs` y `DependencyInjection.cs` con `AddInfrastructure` (`AddDbContext` con `UseSqlServer` sobre `ConnectionStrings:Default`; registro abierto `Scoped` del repositorio y `IUnitOfWork`; **sin** `EnableSensitiveDataLogging`)
- [ ] T015 [P] [US1] Igual que T014 en `ms-autenticacion/src/Acity.Infrastructure/`; el `AppDbContext` queda sin entidades y no se conecta a la base al arrancar
- [ ] T016 [P] [US1] Igual que T015 en `ms-carga-masiva/src/Acity.Infrastructure/`
- [ ] T017 [P] [US1] Igual que T015 en `ms-notificaciones/src/Acity.Infrastructure/`
- [ ] T018 [P] [US1] Crear `ms-control/src/Acity.Api/Program.cs`: `WebApplication` con `AddControllers`, `AddApplication`, `AddInfrastructure`, `MapControllers`; sin Swagger, CORS abierto ni `UsePathBase`; sin endpoints de ejemplo
- [ ] T019 [P] [US1] Igual que T018 en `ms-autenticacion/src/Acity.Api/Program.cs`
- [ ] T020 [P] [US1] Igual que T018 en `ms-carga-masiva/src/Acity.Api/Program.cs`
- [ ] T021 [P] [US1] Igual que T018 en `ms-notificaciones/src/Acity.Api/Program.cs`
- [ ] T022 [US1] Compilar los cuatro servicios por su ruta (`dotnet build ms-<s>/src/Acity.<Servicio>.slnx`) y corregir errores (quickstart 1)
- [ ] T023 [US1] Verificar que no hay referencias entre servicios (revisar todos los `ProjectReference` de los `.csproj`), que las dependencias entre capas son las de T001, y que los `PackageReference` son solo los autorizados (sin Reec, AutoMapper, EPPlus, Cronos, CloudWatch, Dapper, BulkExtensions, Swagger, Newtonsoft ni paquetes de pruebas) (quickstart 2)

## Phase 4: US4 - Contrato común de respuesta y errores (P2)

**Meta**: `Response<T>` y manejo global compatible, `Code` = estado HTTP.
**Prueba independiente**: quickstart 12.

- [ ] T024 [P] [US4] Crear `ms-control/src/Acity.Api/Handlers/GlobalExceptionHandler.cs` (`IExceptionHandler`): `ValidationException` → 400 con `Errors`; cualquier otra excepción → 500 con mensaje genérico, registrando la excepción; sin trazas, cadenas de conexión ni secretos en el cuerpo; `Code` = estado HTTP. En `Program.cs`: `AddExceptionHandler`, `UseExceptionHandler` y `UseStatusCodePages` que escribe un `Response<T>` para respuestas sin cuerpo (404, 405)
- [ ] T025 [P] [US4] Igual que T024 en `ms-autenticacion/src/Acity.Api/`
- [ ] T026 [P] [US4] Igual que T024 en `ms-carga-masiva/src/Acity.Api/`
- [ ] T027 [P] [US4] Igual que T024 en `ms-notificaciones/src/Acity.Api/`

## Phase 5: US5 - Configuración, logs y salud (P2)

**Meta**: configuración por entorno, logs propios por servicio y `GET /health`.
**Prueba independiente**: quickstart 11, 13 y 14.

- [ ] T028 [P] [US5] Crear `ms-control/src/Acity.Api/appsettings.json` con Serilog (`ReadFrom.Configuration`): sinks Console y File con ruta `logs/control-.log`, `rollingInterval` Day, `retainedFileCountLimit` 14, propiedad `Application` = "ms-control", nivel Information y `Microsoft.AspNetCore` en Warning; claves `Startup:SqlWaitAttempts` = 30 y `Startup:SqlWaitSeconds` = 2; sin cadenas de conexión ni secretos. Activar Serilog en `Program.cs` (`UseSerilog`) sin middleware de logging de solicitudes
- [ ] T029 [P] [US5] Igual que T028 en `ms-autenticacion/src/Acity.Api/` con ruta `logs/autenticacion-.log` y `Application` = "ms-autenticacion"; sin claves `Startup`
- [ ] T030 [P] [US5] Igual que T029 en `ms-carga-masiva/src/Acity.Api/` (`logs/carga-masiva-.log`, "ms-carga-masiva")
- [ ] T031 [P] [US5] Igual que T029 en `ms-notificaciones/src/Acity.Api/` (`logs/notificaciones-.log`, "ms-notificaciones")
- [ ] T032 [P] [US5] Crear `ms-control/src/Acity.Api/Controllers/HealthController.cs`: `GET /health`, sin autenticación, responde 200 con `Response<string>` (`Data` = "Healthy", `IsSuccess` verdadero, `Code` 200, `Message` "OK"); no consulta la base; sin paquetes de salud
- [ ] T033 [P] [US5] Igual que T032 en `ms-autenticacion/src/Acity.Api/Controllers/HealthController.cs`
- [ ] T034 [P] [US5] Igual que T032 en `ms-carga-masiva/src/Acity.Api/Controllers/HealthController.cs`
- [ ] T035 [P] [US5] Igual que T032 en `ms-notificaciones/src/Acity.Api/Controllers/HealthController.cs`
- [ ] T036 [US5] Ejecutar manualmente `ms-autenticacion`, `ms-carga-masiva` y `ms-notificaciones` con `dotnet run --project ms-<s>/src/Acity.Api` (sin base de datos): `GET /health` → 200 `Response<string>`; ruta inexistente → 404 y `POST /health` → 405 como `Response<T>` con `code` igual al estado; se crea su archivo `.log` propio sin secretos (quickstart 11–13 en lo que aplica fuera de Docker)

## Phase 6: US2 - Control crea el esquema con migraciones EF Core (P1)

**Meta**: ms-control es el único propietario del modelo y de las migraciones; las aplica al iniciar antes de escuchar.
**Prueba independiente**: quickstart 4–10 (los que no requieren Compose completo se hacen aquí con SQL Server del Compose existente en `localhost:${SQLSERVER_HOST_PORT}`).

### Modelo (Domain y configuraciones)

- [ ] T037 [US2] Crear en `ms-control/src/Acity.Domain/Entities/` las 7 clases `Rol`, `Usuario`, `CargaArchivo`, `CargaPeriodo`, `Producto`, `Incidencia` y `CargaEstadoHistorial` con los campos y tipos de `data-model.md` (sin propiedades de navegación ni `BaseEntity`; `Usuario.RolId` único rol, sin tabla intermedia)
- [ ] T038 [P] [US2] `ms-control/src/Acity.Infrastructure/Persistence/Configurations/RolConfiguration.cs`: tabla `dbo.Rol`; `Id` smallint **sin identidad**; `Nombre` nvarchar(50); `PK_Rol`; único `UQ_Rol_Nombre`; `HasData` (1, Administrador) y (2, Operador)
- [ ] T039 [P] [US2] `UsuarioConfiguration.cs`: `Id` int identity; `Email` nvarchar(256) único `UQ_Usuario_Email`; `PasswordHash` varchar(255) no Unicode; `RolId` smallint; `PK_Usuario`; `FK_Usuario_Rol`
- [ ] T040 [P] [US2] `CargaArchivoConfiguration.cs`: `Id` bigint identity; `NombreArchivo` nvarchar(260); `UsuarioId` int; `FechaRecepcion` datetime2; `RutaArchivo` nvarchar(500); `Estado` varchar(20) con `CK_CargaArchivo_Estado` ∈ {Pendiente, En proceso, Cargado, Finalizado, Notificado, Fallido}; `FechaFin` datetime2 nulo; `IntentosProceso` int con `DF_CargaArchivo_IntentosProceso` = 0; `PK_CargaArchivo`; `FK_CargaArchivo_Usuario`; índice `IX_CargaArchivo_Usuario_FechaRecepcion` (`UsuarioId`, `FechaRecepcion`)
- [ ] T041 [P] [US2] `CargaPeriodoConfiguration.cs`: `Id` bigint identity; `CargaId` bigint; `Periodo` char(7) con `CK_CargaPeriodo_Periodo` (formato AAAA-MM, mes 01–12); `Resultado` varchar(20) con `CK_CargaPeriodo_Resultado` ∈ {Admitido, Completado, Rechazado, Bloqueado, Fallido, NoProcesado}; `PeriodoReservado` char(7) nulo; `FechaResultado` datetime2; `CK_CargaPeriodo_Reserva` (Resultado ∈ {Admitido, Completado} ⇒ `PeriodoReservado IS NOT NULL AND PeriodoReservado = Periodo`; otro ⇒ `PeriodoReservado IS NULL`); único `UQ_CargaPeriodo_Carga_Periodo` (`CargaId`, `Periodo`); **índice único filtrado** `UX_CargaPeriodo_PeriodoReservado` sobre `PeriodoReservado` con filtro `[PeriodoReservado] IS NOT NULL`; `PK_CargaPeriodo`; `FK_CargaPeriodo_Carga`
- [ ] T042 [P] [US2] `ProductoConfiguration.cs`: `Id` bigint identity; `CodigoProducto` nvarchar(100) con collation `Latin1_General_100_CI_AS_WS`; `NombreProducto` nvarchar(200); `Precio` decimal(18,2); `CargaPeriodoId` bigint; `PK_Producto`; `FK_Producto_CargaPeriodo`; **índice único global** `UX_Producto_CodigoProducto` sobre `CodigoProducto`, sin llave compuesta con el periodo
- [ ] T043 [P] [US2] `IncidenciaConfiguration.cs`: `Id` bigint identity; `CargaId` bigint; `CargaPeriodoId` bigint nulo; `Tipo` varchar(30) con `CK_Incidencia_Tipo` ∈ {PeriodoRechazado, PeriodoBloqueado, FilaOmitida, Existente, NoProcesado, ErrorTecnico, ErrorNotificacion}; `NumeroFila` int nulo; `CodigoProducto` nvarchar(100) nulo; `PeriodoTexto` nvarchar(50) nulo; `Motivo` nvarchar(500); `FechaRegistro` datetime2; `PK_Incidencia`; `FK_Incidencia_Carga`; `FK_Incidencia_CargaPeriodo`; índice `IX_Incidencia_Carga_Id` (`CargaId`, `Id`)
- [ ] T044 [P] [US2] `CargaEstadoHistorialConfiguration.cs`: `Id` bigint identity; `CargaId` bigint; `Estado` varchar(20) con `CK_CargaEstadoHistorial_Estado` (mismos seis estados); `FechaCambio` datetime2; `PK_CargaEstadoHistorial`; `FK_CargaEstadoHistorial_Carga`; índice `IX_CargaEstadoHistorial_Carga_Id` (`CargaId`, `Id`)

### Inicialización al arrancar (sin migrar todavía)

- [ ] T045 [US2] Crear `ms-control/src/Acity.Infrastructure/Persistence/DatabaseInitializer.cs` y su uso en `ms-control/src/Acity.Api/Program.cs` **entre `Build()` y `Run()`**: (1) fallar con mensaje claro, sin imprimir el valor, si falta `ConnectionStrings__Default`; (2) esperar a SQL Server abriendo conexión a la base `master` de la misma instancia, hasta `Startup:SqlWaitAttempts` intentos cada `Startup:SqlWaitSeconds` s, sin Polly; (3) guarda de base heredada: si la base objetivo existe, no tiene historial de migraciones de EF y contiene tablas de usuario, fallar sin migrar; (4) `Database.MigrateAsync()` **sin** `suppressTransaction` y sin suprimir `PendingModelChangesWarning`. Cualquier fallo: registrar el error sin secretos, código de salida distinto de cero y no llamar a `Run()`

### Comprobación de `dotnet ef` en tiempo de diseño (no dar por supuesto)

- [ ] T046 [US2] Comprobar si `dotnet ef` crea el `AppDbContext` sin ejecutar el arranque normal: desde `ms-control/`, con `ConnectionStrings__Default` apuntando a un puerto cerrado y logs visibles, ejecutar `dotnet ef dbcontext info --project src/Acity.Infrastructure --startup-project src/Acity.Api`. **Pasa** solo si devuelve la información del contexto **sin** esperar a SQL Server, sin comprobar la base heredada y sin ejecutar `MigrateAsync`; **falla** si aparece cualquiera de esos pasos, si exige conexión o si el host no se puede construir. Registrar el resultado en `research.md` D-11
- [ ] T047 [US2] **Si T046 falla** (solución mínima, sin cambiar la arquitectura): crear `ms-control/src/Acity.Infrastructure/Persistence/AppDbContextFactory.cs` (`IDesignTimeDbContextFactory<AppDbContext>`) que construye el contexto con `UseSqlServer` leyendo `ConnectionStrings__Default` o una cadena local de diseño **sin secretos** (solo se usa para generar migraciones, no se conecta), sin código de arranque ni migraciones; repetir T046 hasta que pase. Si T046 pasa, anotar "no necesario" y no crear el archivo

### Migraciones

- [ ] T048 [US2] Generar la migración inicial desde `ms-control/`: `dotnet tool run dotnet-ef migrations add InitialSchema --project src/Acity.Infrastructure --startup-project src/Acity.Api --output-dir Persistence/Migrations`; revisar que no crea `SchemaMigraciones`, que usa los nombres de T038–T044, que incluye los datos base de `Rol` y que no tiene `suppressTransaction`
- [ ] T049 [US2] Crear la migración `StoredProcedures` (`migrations add StoredProcedures` y completar `Up`/`Down` a mano) en `ms-control/src/Acity.Infrastructure/Persistence/Migrations/`: `Up` con **una llamada `migrationBuilder.Sql` por procedimiento**, sin `GO`, usando el contenido de `referencias/V002__procedimientos.sql` para `usp_ReservarPeriodo` y `usp_CambiarEstadoCarga` (UTF-8; mismos mensajes y números de error 50010–50022); `Down` elimina ambos. No ejecutar los scripts de `referencias/`
- [ ] T050 [US2] Comprobar que no hay cambios de modelo pendientes: `dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Acity.Infrastructure --startup-project src/Acity.Api` (desde `ms-control/`) y compilar

### Aplicación, equivalencia y comportamiento

- [ ] T051 [US2] Aplicar con el arranque real de Control (`dotnet run --project ms-control/src/Acity.Api`) contra la instancia SQL Server del Compose existente (levantarla con `docker compose up -d sqlserver`) usando una base **nueva** `CargaMasivaDbPrueba`: la base y las migraciones se crean, Control escucha y `GET /health` responde 200 solo después (quickstart 4, 11). No usar la base `CargaMasiva`
- [ ] T052 [US2] Comparar el esquema generado con `V001` y `V002` mediante consultas de catálogo **de solo lectura** contra `CargaMasivaDbPrueba` y contra la base anterior `CargaMasiva` (creada con scripts idénticos a `referencias/`, verificado por `diff`): tablas, columnas y tipos, nulabilidad, valores por defecto, collation de `CodigoProducto`, CHECK, claves foráneas, índices (incluida la definición del filtro), datos base de `Rol` y texto de `OBJECT_DEFINITION` de los dos procedimientos (normalizando solo espacios). **Diferencias deliberadas permitidas**: `__EFMigrationsHistory` en lugar de `SchemaMigraciones`; las unicidades `UQ_*` como índices únicos. Cualquier otra diferencia se corrige o se documenta con su motivo. Registrar resultados en `quickstart.md` (escenario 4)
- [ ] T053 [US2] Repetir manualmente sobre `CargaMasivaDbPrueba` los escenarios de base de la Feature 001 (reserva con Completado/Admitido previo, repetición idempotente, índice filtrado y CHECK, unicidad global y comparación de `CodigoProducto`, matriz de estados, fallo terminal con `NoProcesado`) con datos de prueba que se limpian al final (quickstart 5)
- [ ] T054 [US2] Reiniciar Control sobre `CargaMasivaDbPrueba`: sin objetos nuevos y `__EFMigrationsHistory` sin filas nuevas (quickstart 6)
- [ ] T055 [US2] Fallo de migración: crear una base vacía `CargaMasivaDbFallo` con un procedimiento `dbo.usp_ReservarPeriodo` preexistente y arrancar Control contra ella. Esperado: `InitialSchema` aplicada, `StoredProcedures` revertida (conflicto), historial solo con `InitialSchema`, error registrado sin secretos, código de salida distinto de cero y sin escuchar (quickstart 8). Documentar el resultado real
- [ ] T056 [US2] Base heredada: registrar antes el número de objetos de `CargaMasiva` (`sys.objects`), arrancar Control apuntando a ella y comprobar que se niega a migrar con un mensaje claro, que el número de objetos no cambia y que no se crea `__EFMigrationsHistory` (quickstart 9). Si la guarda falla y modifica algo, detener y reportar
- [ ] T057 [US2] SQL Server inaccesible: arrancar Control con `ConnectionStrings__Default` hacia un puerto cerrado y comprobar la espera acotada (30 intentos × 2 s por defecto), el fallo final y que no queda listo (quickstart 10)
- [ ] T058 [US2] Eliminar las bases de prueba `CargaMasivaDbPrueba` y `CargaMasivaDbFallo` (solo esas dos, dentro de la instancia de este proyecto) y comprobar que `CargaMasiva` sigue intacta

## Phase 7: US3 - Docker y Docker Compose (P1)

**Meta**: Dockerfile por servicio y Compose raíz con el orden SQL Server → Control → resto.
**Prueba independiente**: quickstart 3, 11, 13 y 15.

### Comprobar las imágenes reales (no dar por supuesto)

- [ ] T059 [US3] Descargar `mcr.microsoft.com/dotnet/sdk:10.0` y `mcr.microsoft.com/dotnet/aspnet:10.0` y registrar su digest y plataforma (amd64 o arm64) en `research.md` D-13
- [ ] T060 [US3] Comprobar en la imagen real `aspnet:10.0`: existencia de `bash`, soporte de `/dev/tcp`, usuario y `APP_UID` por defecto (p. ej. `docker run --rm --entrypoint bash <imagen> -c 'id; echo $APP_UID; exec 3<>/dev/tcp/127.0.0.1/1 || echo sin-tcp'`). **Si falla** (sin `bash` o sin `/dev/tcp`): solución mínima: usar la variante Debian de la misma versión; si aún no sirve, instalar `curl` en la etapa final del Dockerfile y usarlo en el healthcheck. Registrar la decisión en `research.md` D-07

### Dockerfiles

- [ ] T061 [P] [US3] `ms-control/Dockerfile` (varias etapas: `sdk:10.0` restaura y publica `src/Acity.Api`; `aspnet:10.0` ejecuta; contexto = `ms-control/`; escucha en 8080; `/app/logs` creado y asignado al usuario de ejecución; `USER $APP_UID`; entrada `dotnet Acity.Api.dll`) y `ms-control/.dockerignore` (`bin/`, `obj/`, `logs/`)
- [ ] T062 [P] [US3] `ms-autenticacion/Dockerfile` y `.dockerignore` igual que T061
- [ ] T063 [P] [US3] `ms-carga-masiva/Dockerfile` y `.dockerignore` igual que T061
- [ ] T064 [P] [US3] `ms-notificaciones/Dockerfile` y `.dockerignore` igual que T061
- [ ] T065 [US3] Construir las cuatro imágenes y comprobar con `docker run --rm --entrypoint ls`/`docker history` que cada una contiene solo su servicio (sin proyectos de otros servicios) (quickstart 15)
- [ ] T066 [US3] Comprobar en una imagen construida los **permisos del volumen** de logs: con un volumen con nombre temporal (`prueba-logs-tmp`) montado en `/app/logs` y el usuario no root, el servicio crea su `.log`. **Si falla** (permission denied): solución mínima: crear y asignar `/app/logs` antes del montaje en el Dockerfile; si persiste, ejecutar como root para ese servicio y documentarlo. Eliminar el volumen temporal al terminar

### Compose

- [ ] T067 [US3] Actualizar `compose.yaml` conservando el servicio `sqlserver` (imagen, `platform`, volumen `sqlserver-data` y healthcheck) y añadiendo `ms-control` (8082; `build: ./ms-control`; depende de `sqlserver` en `service_healthy`), `ms-autenticacion` (8081), `ms-carga-masiva` (8083) y `ms-notificaciones` (8084) (los tres dependen de `ms-control` en `service_healthy`), todos con `platform: linux/amd64` si T059 lo exige, `restart: "no"`, puerto 8080 interno, `ConnectionStrings__Default` ensamblada con `MSSQL_SA_PASSWORD` y `DB_NAME` (`Encrypt=True;TrustServerCertificate=True`), healthcheck de T060 sobre `/health` y un volumen de logs propio (`logs-control`, `logs-autenticacion`, `logs-carga-masiva`, `logs-notificaciones`) montado en `/app/logs`; sin RabbitMQ ni SeaweedFS
- [ ] T068 [US3] Actualizar `.env.example` con `DB_NAME=CargaMasivaDb` (sin contraseña) y, en el `.env` local (no versionado), cambiar `DB_NAME` de `CargaMasiva` a `CargaMasivaDb` dejando intacta la contraseña
- [ ] T069 [US3] Validar la configuración con `docker compose config -q` (sin imprimir secretos)

### Validación del arranque completo

- [ ] T070 [US3] Levantar toda la fundación con `docker compose up -d --build` sobre la base nueva `CargaMasivaDb` y observar el orden: `sqlserver` healthy → `ms-control` migra y queda healthy → arrancan los otros tres (quickstart 3). Documentar el tiempo observado y cualquier error concreto
- [ ] T071 [US3] Con la fundación arriba: `GET /health` de los cuatro (8081–8084) → 200; consultar la base y comprobar tablas, historial EF, roles y procedimientos; comprobar que los otros tres no modificaron el esquema (quickstart 7, 11)
- [ ] T072 [US3] Revisar los logs: cada servicio escribe su `.log` en su volumen; ningún archivo ni salida contiene contraseñas, tokens ni cadenas de conexión (`grep` del valor de `MSSQL_SA_PASSWORD` sin imprimirlo) (quickstart 13)
- [ ] T073 [US3] Reiniciar solo `ms-control` (`docker compose restart ms-control`): sin objetos nuevos ni migraciones reaplicadas (quickstart 6 en Docker)
- [ ] T074 [US3] Fallo en Compose: ejecutar `DB_NAME=CargaMasivaDbFallo docker compose up` con la base de T055 preparada y comprobar que `ms-control` termina con error, no queda healthy y los tres dependientes **no** arrancan (quickstart 3 y 8). Después `docker compose down` (**sin** `-v`) y eliminar la base `CargaMasivaDbFallo`
- [ ] T075 [US3] Comprobar que la base anterior `CargaMasiva`, el volumen `carga-masiva_sqlserver-data` y los contenedores y volúmenes ajenos siguen intactos; solo entonces declarar el resultado de la validación de Docker (con la limitación de emulación amd64 documentada)

## Phase 8: Polish y cierre

- [ ] T076 Actualizar `specs/002-fundacion-microservicios/quickstart.md` marcando por escenario si se ejecutó, con su resultado real; los no ejecutados se indican como tales
- [ ] T077 [P] Actualizar `specs/002-fundacion-microservicios/research.md` (D-07, D-10, D-11, D-13 y "Abiertos") con los resultados reales de T046–T047, T059–T060 y T066: qué suposición se confirmó, cuál falló y qué solución mínima se aplicó
- [ ] T078 [P] Revisión final: `git status`/`git diff --stat`; sin secretos ni `.env` en los cambios; sin archivos de `database/` ni `referencias/` modificados; sin paquetes no autorizados; sin proyectos de pruebas
- [ ] T079 Informar el resultado: tareas completadas, verificaciones, diferencias respecto de V001/V002, limitaciones (emulación amd64) y pendientes reales

## Dependencias

- Phase 1 → Phase 2 → US1 (T010–T023). US4 y US5 dependen de US1 y de Phase 2; US4 y US5 editan el mismo `Program.cs` por servicio, así que por servicio van en orden (T024 antes de T028/T032 en Control, etc.); servicios distintos en paralelo.
- US2 depende de US1, US4 y US5 de Control. Dentro de US2: T037 → T038–T044; T045 antes de T046; T046 → (T047 solo si falla) → T048 → T049 → T050 → T051 → T052–T057 → T058.
- US3 depende de US2 y de US5. T059 → T060 → T061–T064 → T065 → T066 → T067–T069 → T070–T075.
- Phase 8 al final.

## Paralelismo

- T001–T004, T006–T009, T010–T021, T024–T027, T028–T035 en paralelo entre servicios.
- T038–T044 en paralelo (archivos distintos).
- T061–T064 en paralelo.
- T077 y T078 en paralelo.

## Estrategia

1. Setup y contrato común (T001–T009).
2. US1: estructura compilable (T010–T023) → US4 y US5 (T024–T036).
3. US2: modelo, inicialización, comprobación de `dotnet ef`, migraciones, equivalencia y fallos (T037–T058).
4. US3: comprobar imágenes, Dockerfiles, Compose y validar el arranque completo (T059–T075).
5. Cierre (T076–T079).

MVP: Phase 1–2, US1 y US2 con la comprobación de equivalencia (T052). US3 es la validación de Docker; sin ella el flujo no se declara validado.

## Fuera de alcance (no hacer)

Login/JWT/usuarios de demostración, lectura de Excel, RabbitMQ, SeaweedFS, correo, Polly, API Gateway, React, pruebas automatizadas, modificar `database/`/`referencias/`/constitución, borrar volúmenes o la base anterior.
