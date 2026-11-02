# Data Model: registro de scripts, modelo de Control tras la limpieza e ingeniería inversa

> **Nota (Feature 004, 2026-11-02)**: las entidades por servicio de la tabla "Modelo EF Core por servicio" (sin entidades en ninguno) quedaron **precisadas** por `specs/004-conexion-entidades-por-servicio/`: ms-autenticacion `Rol` y `Usuario`; ms-control `CargaArchivo`, `CargaEstadoHistorial` e `Incidencia`; ms-carga-masiva `CargaArchivo`, `CargaPeriodo`, `Producto` e `Incidencia`; ms-notificaciones `Incidencia` (obtenidas por ingeniería inversa; sin el modelo completo en ningún servicio). El esquema sigue gestionado por el runner de `database/`. El texto original se conserva como historial.


El esquema, sus reglas y los procedimientos son los de la Feature 001 y de `referencias/V001__esquema_inicial.sql` y `V002__procedimientos.sql`, ya presentes sin cambios en `database/scripts/`. **No se cambian tablas, roles ni reglas.** Aquí solo se fijan qué modela cada servicio en EF Core y cómo se obtiene el modelo. Procedencia: `Dec`, `Obs`, `Plan` como en `research.md`.

## Esquema creado por el runner (Dec)

| Objeto | Origen | Notas |
|---|---|---|
| 7 tablas de negocio: `Rol`, `Usuario`, `CargaArchivo`, `CargaPeriodo`, `Producto`, `Incidencia`, `CargaEstadoHistorial` | `V001` | Con sus relaciones, índices (incluido el índice único filtrado), restricciones CHECK, collation de `CodigoProducto` y roles base (1 Administrador, 2 Operador); las unicidades son restricciones `UQ_*` como en V001 |
| Procedimientos `usp_ReservarPeriodo`, `usp_CambiarEstadoCarga` | `V002` | Contrato de la Feature 001 |
| `SchemaMigraciones` | `database/bootstrap.sql` (runner) | Registro de scripts aplicados; no es tabla de negocio ni parte del modelo EF |

Las diferencias físicas que introdujo la Feature 002 (unicidades como índices, `__EFMigrationsHistory`) desaparecen: el esquema es el de V001/V002 literalmente.

## Registro de scripts aplicados (`SchemaMigraciones`) (Obs)

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Script` | NVARCHAR(200), PK | No | Nombre del archivo `Vnnn__descripcion.sql` |
| `Checksum` | CHAR(64) | No | SHA-256 del contenido; si cambia, el runner falla |
| `FechaAplicado` | DATETIME2 | No | UTC al registrar |

Reglas del runner: se registra dentro de la transacción del script; orden por nombre; un script nuevo con número menor que el último aplicado falla.

## Modelo EF Core por servicio tras esta feature (Dec)

| Servicio | Entidades | Cambio |
|---|---|---|
| ms-control | Ninguna | Se retiran las 7 (ver tabla de `research.md` D-04); sin migraciones ni inicializador |
| ms-autenticacion | Ninguna | Sin cambios |
| ms-carga-masiva | Ninguna | Sin cambios |
| ms-notificaciones | Ninguna | Sin cambios |

Los cuatro `AppDbContext` quedan sin entidades y con `ApplyConfigurationsFromAssembly` inocuo; Control deja de retirar la convención de índices de claves foráneas (ya no genera el esquema). Las entidades se incorporan solo cuando una feature funcional las necesite, con el procedimiento de abajo; no se agregan por necesidades futuras supuestas.

## Procedimiento de ingeniería inversa (Dec, Plan)

Operación de **desarrollo**, manual, contra una base ya creada por el runner. No forma parte del arranque ni de las imágenes.

| Paso | Acción |
|---|---|
| 1 | Tener la base creada por el runner y accesible (puerto 14333) y la cadena de conexión en una variable de entorno de la sesión (no en archivos versionados) |
| 2 | Desde el directorio del servicio, con `dotnet tool restore` (herramienta `dotnet-ef` 10.0.14 del manifiesto local), ejecutar `dotnet ef dbcontext scaffold` con el proveedor `Microsoft.EntityFrameworkCore.SqlServer`, indicando `--table` solo para las tablas que ese servicio necesita, `--no-onconfiguring`, y salida (`--output-dir`, `--context-dir`) a una carpeta temporal fuera de las capas |
| 3 | Revisar el resultado: conservar solo lo necesario; sin CRUD ni lógica; no incorporar tablas, columnas ni relaciones que el servicio no use |
| 4 | Integrar a mano: entidades en `Acity.Domain`, mapeos y contexto en `Acity.Infrastructure`, comparando antes con lo existente para no sobrescribir personalizaciones; sin `--force` sobre carpetas del repositorio |
| 5 | Compilar el servicio por su ruta; el generador no se invoca desde `Program.cs` ni desde el Dockerfile |

Requisitos mínimos: la herramienta y el paquete de diseño ya presentes en Control. Los servicios sin ellos (`ms-autenticacion`, `ms-carga-masiva`, `ms-notificaciones`) los añaden en su propia feature funcional, con las mismas versiones ya autorizadas y como dependencia de desarrollo, cuando necesiten entidades; esta feature no los añade. Los procedimientos almacenados no se generan por ingeniería inversa; su uso desde EF/LINQ lo define la feature que los necesite.

## Verificación de equivalencia (en la implementación)

Sobre una base nueva y vacía, comparar con consultas de catálogo el resultado del runner con la referencia (tablas, columnas y tipos, collation de `CodigoProducto`, nulabilidad, valores por defecto, CHECK, claves foráneas, índices incluido el filtro, roles base y los dos procedimientos) y repetir manualmente los escenarios de base de la Feature 001 (reserva, índice filtrado, comparación de códigos, matriz de estados, fallo terminal). Ver `quickstart.md`.

## Verificación del procedimiento (implementación, T034; 2026-11-02, este equipo)

Ejecutado contra `CargaMasivaApp` (creada por el runner) para una sola tabla, con salida a una carpeta temporal fuera del repositorio (descartada después; `git status` sin archivos nuevos). Desde `ms-control/`, con la cadena de conexión en una variable de entorno de la sesión (`Server=localhost,14333;Database=<base>;User Id=sa;Password=<desde el entorno>;Encrypt=True;TrustServerCertificate=True`):

```text
dotnet tool restore
dotnet ef dbcontext scaffold "$CONN" Microsoft.EntityFrameworkCore.SqlServer \
  --project src/Acity.Infrastructure --startup-project src/Acity.Api \
  --table dbo.Rol --no-onconfiguring --context ScaffoldContext \
  --output-dir <carpeta-temporal>/Entities --context-dir <carpeta-temporal>/Context \
  --namespace Scaffold.Entities --context-namespace Scaffold.Context
```

Resultado: código 0 en ~3 s; la salida (sin la contraseña) solo menciona la compilación; se generó únicamente `Rol.cs` (entidad parcial con `Id` smallint y `Nombre` string) y `ScaffoldContext.cs` (un `DbSet<Rol>` llamado `Rols`, `ToTable("Rol")`, `HasIndex(e => e.Nombre, "UQ_Rol_Nombre").IsUnique()`, `ValueGeneratedNever` en `Id` y `HasMaxLength(50)`). Observaciones para la integración manual: el generador crea una entidad y un contexto propios que no se copian tal cual; de ahí solo se toma lo que el servicio use (la entidad hacia `Acity.Domain`; el mapeo hacia `Acity.Infrastructure/Persistence`, p. ej. una configuración o el `AppDbContext` existente); se revisan nombres como el plural `Rols`; no se llevan unicidades, índices ni longitudes que el servicio no necesite; la ejecución real de la herramienta no se integró al repositorio.

Los servicios `ms-autenticacion`, `ms-carga-masiva` y `ms-notificaciones` no tienen hoy el manifiesto de herramientas ni el paquete de diseño; aplicarán este procedimiento en su propia feature funcional, cuando necesiten entidades, añadiendo en su servicio la herramienta y el paquete con las mismas versiones ya autorizadas (10.0.14) como dependencia de desarrollo. Esta feature no los añade.
