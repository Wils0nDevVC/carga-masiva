# Research: Fundación común de los cuatro microservicios

Procedencia: **Dec** = decisión del usuario o de la constitución; **Ref** = observado en el repositorio de referencia; **Plan** = decisión técnica de este plan, tomada con la alternativa más sencilla compatible con las fuentes; **Verif** = comprobado en esta sesión (fecha 2-nov-2026) con la fuente indicada; **Sin verificar** = pendiente para la implementación. Ninguna decisión de negocio se reabre.

## D-01 Organización tomada de la referencia
- Cinco proyectos por servicio con la cadena `Api → Infrastructure → Application → {Common, Domain}`; Domain sin referencias; Common sin referencias a otros proyectos. [Ref] [Dec]
- Registro de dependencias por capa con métodos de extensión (`AddApplication`, `AddInfrastructure`) llamados desde `Program.cs`; `DbContext` con configuraciones aplicadas desde el ensamblado; repositorio genérico y Unit of Work `Scoped`. [Ref]
- No se adopta: `UsePathBase("/api")`, Swagger, Newtonsoft, CORS abierto, `Reec`, AutoMapper, Dapper, BulkExtensions, Cronos, EPPlus, CloudWatch, MediatR 10, `BaseEntity`/`IGenerateEntity`, ni las operaciones masivas/truncado del `IUnitOfWork`. [Dec] [Constitución §12]

## D-02 Versiones y licencias de paquetes
Consultado en la API de NuGet (versiones estables, licencia en el `.nuspec`) el 2-nov-2026.

| Paquete | Versión | Licencia | Dónde | Notas |
|---|---|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.14 | MIT | Infrastructure (4) | Trae `Microsoft.Data.SqlClient` 6.1.7 y `Relational` 10.0.14 |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.14 | MIT | Solo `ms-control/Acity.Api`, `PrivateAssets=all` | Necesario para `dotnet ef migrations add` |
| `dotnet-ef` (herramienta local) | 10.0.14 | MIT | Solo `ms-control/.config/dotnet-tools.json` | Manifiesto local; no global |
| `MediatR` | **12.5.0** (fijada) | Apache-2.0 | Application (4) | La última (14.x) no se usa: la constitución fija 12.5.0 por licencia |
| `FluentValidation` y `FluentValidation.DependencyInjectionExtensions` | 12.2.0 | Apache-2.0 | Application (4) | `AddValidatorsFromAssembly` |
| `Serilog.AspNetCore` | 10.0.0 | Apache-2.0 | Api (4) | Trae `Serilog.Sinks.File` 7.0.0, `Sinks.Console` 6.1.1 y `Settings.Configuration` 10.0.0; no se referencian por separado |

- ASP.NET Core Web API, `System.Text.Json`, la inyección de dependencias y los controladores vienen en el marco compartido: sin paquetes. Sin paquetes de salud, Swagger ni testing.
- SDK local: .NET 10 instalado (10.0.x). Imágenes base: las etiquetas `mcr.microsoft.com/dotnet/sdk:10.0` y `aspnet:10.0` existen en MCR (**Verif**, lista de etiquetas); no están descargadas localmente; su descarga se hace en la implementación.
- **Sin verificar**: compatibilidad real en ejecución (se comprueba al compilar). Los paquetes se fijan por versión exacta; no hay `Directory.Packages.props` compartido porque los servicios son independientes. [Plan]

## D-03 Estructura de solución por servicio
- `ms-<servicio>/src/` contiene los cinco proyectos y un archivo de solución propio con nombre inequívoco: `Acity.Autenticacion.slnx`, `Acity.Control.slnx`, `Acity.CargaMasiva.slnx`, `Acity.Notificaciones.slnx` (formato por defecto del SDK 10; `--format sln` si hiciera falta). [Plan]
- Comandos inequívocos por ruta: `dotnet build ms-control/src/Acity.Control.slnx` y `dotnet run --project ms-control/src/Acity.Api`. Sin solución en la raíz ni referencias entre servicios. [Dec]
- `Dockerfile` y `.dockerignore` en la raíz de cada servicio; el contexto de construcción es el directorio del servicio, lo que impide referenciar otro servicio. [Plan]

## D-04 Repository y Unit of Work mínimos
- **Contratos** en Application y **implementaciones** en Infrastructure de cada servicio (son proyectos independientes). [Ref] [Dec]
- `IGenericRepository<TEntity>` (`class`): `AsQueryable`, `AsNoTracking`, `FindAsync`, `CreateAsync`, `Update`, `Delete`. `IUnitOfWork`: `SaveChangesAsync`. Registro abierto `Scoped` en `AddInfrastructure`. [Ref] [Plan]
- **Fuera**: operaciones masivas, truncado, versiones síncronas, repositorios por entidad, y la API de transacciones (la necesita la feature de Carga Masiva para su transacción por periodo; se añade allí). [Dec] [Plan]
- Justificación de incluirlos ahora: la spec (FR-008) los exige "mínimos conforme a la referencia"; son los contratos que reutilizarán las features funcionales. Sin ellos no hay uso concreto en esta fundación, por eso no se añade nada más.

## D-05 DbContext por servicio y modelo de acceso
- Cada servicio tiene su `AppDbContext` en `Acity.Infrastructure/Persistence` con `ApplyConfigurationsFromAssembly`; las entidades entran al modelo por sus configuraciones (sin `DbSet` por entidad). [Ref] [Plan]
- **Control**: modelo completo (siete entidades; `data-model.md`), propietario de las migraciones. [Dec]
- **Autenticación, Carga Masiva y Notificaciones**: `AppDbContext` **sin entidades todavía**: cada feature añadirá solo lo que necesite (FR-032). Sin migraciones ni `Database.Migrate`. No se conectan a la base al arrancar. [Dec] [Plan]
- Sin `EnableSensitiveDataLogging` (la referencia lo activa en local; no se adopta por la regla de no registrar secretos). [Plan]

## D-06 Integración de FluentValidation
- Validadores registrados con `AddValidatorsFromAssembly` en `AddApplication`. Un único `ValidationBehavior<TRequest,TResponse>` abierto en el pipeline de MediatR ejecuta los validadores y, si hay fallos, lanza `ValidationException` (de FluentValidation). [Plan]
- El manejador global de excepciones la traduce a 400 con `Errors` (D-08). Sin validadores ni solicitudes de ejemplo: no hay reglas funcionales en esta feature. [Dec]
- Justificación: es el mínimo para que "FluentValidation" esté realmente integrado; no se añaden más comportamientos.

## D-07 Salud y espera de SQL Server
- **Endpoint**: `GET /health` en un `HealthController` de cada `Acity.Api` (la constitución §3 exige entradas HTTP con controladores). Responde 200 con `Response<string>`; sin autenticación; sin consultar la base (indica que el host arrancó). Sin paquetes ni `AddHealthChecks`. [Plan] [Dec]
- **Listo en Control**: el servidor HTTP de Control **no empieza a escuchar hasta terminar la inicialización**: la migración se ejecuta entre `builder.Build()` y `app.Run()`. Así `/health` solo responde tras migrar y un fallo nunca queda "listo". [Plan]
- **Espera de SQL Server**: Compose usa el `healthcheck` ya verificado de SQL Server (`service_healthy`). Además, Control reintenta de forma acotada abrir una conexión a la base `master` de la misma instancia (se usa `master` porque la base objetivo puede no existir aún): por defecto 30 intentos cada 2 s (`Startup:SqlWaitAttempts`, `Startup:SqlWaitSeconds`); si se agota, falla. Sin Polly (sus políticas se definen en las features funcionales). [Dec] [Plan]
- **Healthcheck de contenedor**: la imagen `aspnet` no trae `curl`; se usa `bash` con `/dev/tcp` para pedir `/health` y comprobar el 200, sin instalar paquetes. **Sin verificar** que `bash` esté disponible en la imagen elegida; se comprueba al construirla. [Plan]

## D-08 Contrato de respuesta y manejo global de errores
- `Response<T>` como `record` en `Acity.Common/Helpers`: `Data` (nullable), `IsSuccess`, `Message`, `Code`, `Errors` (colección de mensajes nullable). Dos fábricas estáticas mínimas (éxito y fallo). [Dec] [Plan]
- **Data sin resultado**: se serializa `null`; los cinco campos aparecen siempre. **Serialización**: `System.Text.Json` por defecto del marco (nombres en camelCase: `data`, `isSuccess`, `message`, `code`, `errors`); sin Newtonsoft. [Plan] (Resuelve los pendientes de la constitución §5.)
- **Manejador**: `IExceptionHandler` registrado con `AddExceptionHandler` y activado con `UseExceptionHandler`: `ValidationException` → 400 con `Errors`; cualquier otra excepción → 500 con un mensaje genérico, registrando la excepción en el log. `UseStatusCodePages` construye un `Response<T>` para respuestas sin cuerpo (404, 405, etc.). `Code` se toma del estado HTTP efectivo. Sin trazas ni cadenas de conexión en el cuerpo. [Plan] [Dec]
- Sin errores de negocio ni nuevos códigos: no hay funcionalidad que los genere.

## D-09 Configuración y secretos
- `appsettings.json` y `appsettings.{Environment}.json` (opcional); las variables de entorno prevalecen. Secretos solo por variables de entorno (`ConnectionStrings__Default` ensamblada por Compose con `MSSQL_SA_PASSWORD` y `DB_NAME`). [Dec]
- Cadena de conexión: `Encrypt=True;TrustServerCertificate=True` por el certificado autofirmado del contenedor (entorno de desarrollo del examen). [Plan]
- Fallo claro si falta `ConnectionStrings__Default` en Control; sin imprimir el valor. Los demás servicios no la exigen al arrancar. [Plan]

## D-10 Logging
- Serilog configurado desde `appsettings` (`ReadFrom.Configuration`): sinks Console y File; plantilla estructurada con propiedad `Application` = nombre del servicio; nivel Information con `Microsoft.AspNetCore` en Warning. [Dec] [Plan]
- **Archivos**: `logs/<servicio>-.log` relativo a `/app`, rotación diaria, retención de 14 archivos. Un volumen con nombre por servicio montado en `/app/logs` (`logs-autenticacion`, `logs-control`, `logs-carga-masiva`, `logs-notificaciones`). [Plan] (Resuelve rotación y retención: constitución §10.)
- Contenedor con usuario no root (`USER $APP_UID`) y `/app/logs` creado y asignado a ese usuario. **Sin verificar** permisos de escritura del volumen; se comprueba en la implementación. [Plan]
- No se registran contraseñas, tokens, cadenas de conexión ni el cuerpo de solicitudes. Sin middleware de logging de solicitudes por ahora. [Dec] [Plan]

## D-11 Migraciones EF Core en Control
- **Propiedad**: solo `ms-control/Acity.Infrastructure` contiene el `AppDbContext` con el modelo completo y las migraciones; los demás servicios no tienen paquete `Design` ni migraciones. [Dec]
- **Generación**: dos migraciones: `InitialSchema` (generada con `dotnet ef migrations add` desde el modelo y revisada contra `referencias/V001__esquema_inicial.sql`) y `StoredProcedures` (SQL escrito a mano con el contenido de `referencias/V002__procedimientos.sql`; `Down` elimina los procedimientos). `HasData` inserta los roles base. No se genera ni se ejecuta nada en esta etapa. [Dec] [Plan]
- **Procedimientos en SQL**: **Verif** (código fuente de EF Core, rama `main`) que `SqlServerMigrationsSqlGenerator` divide el SQL de `migrationBuilder.Sql` en lotes por líneas `GO`; aun así se usa una llamada `Sql` por procedimiento, sin `GO`, porque `CREATE PROCEDURE` debe ir solo en su lote. El código fuente verificado es el de `main`, no necesariamente el de 10.0.14.
- **Aplicación**: en `Program.cs` de Control, tras `Build()` y antes de `Run()`: (1) esperar a SQL Server (D-07); (2) comprobar la base heredada (D-12); (3) `Database.MigrateAsync()`. [Dec] [Plan]
- **Bloqueo y arranques simultáneos**: **Verif** (documentación de EF Core y código fuente): desde EF Core 9, `Migrate`/`MigrateAsync` toman un bloqueo de base de datos (en SQL Server, `sp_getapplock` con recurso `__EFMigrationsLock`, propietario de sesión, exclusivo) durante toda la migración. Con una sola instancia en Compose basta; si otra instancia no obtiene el bloqueo, falla y no queda lista. No se añade otro mecanismo. [Plan]
- **Migración fallida**: **Verif** (código fuente) que cada migración se ejecuta dentro de una transacción salvo que se suprima; no se usará `suppressTransaction`. Si una migración falla, se revierte esa migración, no se inserta en el historial, las anteriores permanecen, Control registra el error (sin secretos), termina con código distinto de cero y no escucha; Compose no arranca los dependientes. Reiniciar tras corregir reintenta desde la migración fallida. [Plan]
- **Modelo con cambios pendientes**: EF Core 9+ lanza una excepción en `MigrateAsync` si el modelo tiene cambios sin migración (**Verif**, documentación); no se suprime la advertencia. [Plan]
- **Advertencia de la documentación**: Microsoft prefiere paquetes de migración o scripts frente a migrar al arrancar la aplicación; esta feature mantiene la decisión aprobada de migrar al iniciar Control y acepta esas compensaciones (credencial con permisos de esquema, sin revisión previa del SQL). [Dec]
- **Sin verificar**: que `dotnet ef` con el proyecto de inicio no ejecute el código posterior a `Build()` (comportamiento esperado de la herramienta con el host mínimo); si lo hiciera, se añadiría una fábrica de tiempo de diseño. Se comprueba en la implementación.

## D-12 Base nueva y protección de la heredada
- La fundación usa la base **`CargaMasivaDb`** (variable `DB_NAME`, valor por defecto en Compose) dentro de la instancia SQL Server **y el volumen existentes**. La base anterior `CargaMasiva` y su volumen se conservan sin cambios; Compose no declara ni toca recursos ajenos. [Dec]
- **Guarda**: antes de migrar, si la base objetivo existe, no tiene historial de migraciones de EF y contiene tablas de usuario, Control falla con un mensaje claro y no migra ni inventa historial (FR-034). Una base creada vacía por un intento fallido previo (sin tablas) sí continúa. [Dec] [Plan]
- El `.env` local de este equipo todavía define `DB_NAME=CargaMasiva` (base anterior): al implementar hay que actualizar `.env.example` y el `.env` local, o dejar que Compose use el valor por defecto. Es una acción de la implementación, no de este plan.

## D-13 Docker y Compose
- Un `Dockerfile` por servicio, de varias etapas: `sdk:10.0` para `dotnet publish` y `aspnet:10.0` para ejecutar; contexto = directorio del servicio; escucha en el puerto 8080 del contenedor. [Dec] [Plan]
- Compose en la raíz: se conserva el servicio `sqlserver` (imagen `2022-CU27-ubuntu-22.04`, `platform: linux/amd64`, volumen y healthcheck existentes) y se añaden los cuatro servicios. Dependencias: `ms-control` depende de `sqlserver` (`service_healthy`); los otros tres, de `ms-control` (`service_healthy`). Sin `restart` automático. Puertos del host: 8081 autenticación, 8082 control, 8083 carga masiva, 8084 notificaciones (libres en este equipo; el 14333 de SQL Server ya está en uso por este proyecto). [Plan]
- RabbitMQ y SeaweedFS no se declaran todavía: sus servicios y dependencias pertenecen a las features que los usan (FR-043). [Dec]
- **Limitación documentada, sin cambios**: el motor es amd64 y corre emulado en Apple silicon, sin soporte oficial de Microsoft. Lo verificado el 2-nov-2026 fue el arranque de SQL Server con el **runner anterior**; eso **no valida** el flujo con EF Core Migrations ni las imágenes .NET. La validación real se hace en la implementación y el flujo no se declara validado hasta entonces. [Dec]

## Abiertos
Ninguno bloquea la implementación. Pendientes de comprobar al implementar (marcados "Sin verificar" arriba): `bash` y permisos de volumen en la imagen `aspnet:10.0`, comportamiento de `dotnet ef` con el host mínimo, equivalencia exacta del esquema migrado con `V001`/`V002` y validación de Docker con el equipo de ejecución.
