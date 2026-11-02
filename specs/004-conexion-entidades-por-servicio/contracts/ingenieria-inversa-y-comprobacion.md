# Contrato: ingeniería inversa por servicio y comprobación manual

No hay endpoints nuevos. Este contrato fija los comandos que ejecuta el usuario manualmente, las reglas de integración y la comprobación de conexión. Procedencia: `Dec`, `Plan`, `Verif` como en `research.md`. Estado: **diseño**; no se ha ejecutado scaffolding ni se ha modificado código en esta etapa.

## Prerrequisitos (Dec)

- Compose levantado y `CargaMasivaApp` creada por el runner (feature 003); puerto del host 14333.
- `MSSQL_SA_PASSWORD` en el entorno del shell (la que acepta SQL Server en este equipo; ver nota de precedencia de `.env`). **No** imprimirla ni guardarla en archivos.
- SDK .NET 10 y la herramienta local de `ms-control` (`dotnet tool restore` desde `ms-control/`).
- La carpeta temporal de salida **fuera del repositorio**.

## Ingeniería inversa: preparación común

```bash
# Desde la raíz del repositorio, en tu terminal (la contraseña sale del entorno, no se escribe aquí)
export CONN="Server=localhost,14333;Database=CargaMasivaApp;User Id=sa;Password=$MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"
export OUT="$(mktemp -d)"            # carpeta temporal fuera del repositorio
cd ms-control && dotnet tool restore
```

Plantilla de invocación (se repite por servicio cambiando `<svc>` y la lista de `--table`):

```bash
dotnet ef dbcontext scaffold "$CONN" Microsoft.EntityFrameworkCore.SqlServer \
  --project src/Acity.Infrastructure --startup-project src/Acity.Api \
  --context ScaffoldContext --no-onconfiguring --no-pluralize \
  --namespace Acity.Domain.Entities --context-namespace Acity.Infrastructure.Persistence \
  --output-dir "$OUT/<svc>/Entities" --context-dir "$OUT/<svc>/Context" \
  --table dbo.<Tabla1> --table dbo.<Tabla2>
```

Por qué desde `ms-control` para todos: el resultado depende de la base y de las opciones, no del proyecto destino; solo Control tiene la herramienta y el paquete de diseño, y los demás servicios no reciben paquetes nuevos (FR-020). La salida va a la carpeta temporal; **no** se usa `--force` ni se apunta `--output-dir`/`--context-dir` a carpetas del repositorio.

## Comandos por servicio (Dec)

| Servicio | Tablas (`--table`) | `<svc>` |
|---|---|---|
| ms-autenticacion | `dbo.Rol`, `dbo.Usuario` | `autenticacion` |
| ms-control | `dbo.CargaArchivo`, `dbo.CargaEstadoHistorial`, `dbo.Incidencia` | `control` |
| ms-carga-masiva | `dbo.CargaArchivo`, `dbo.CargaPeriodo`, `dbo.Producto`, `dbo.Incidencia` | `carga-masiva` |
| ms-notificaciones | `dbo.Incidencia` | `notificaciones` |

Comandos completos (copiar y pegar tras la preparación común):

```bash
# ms-autenticacion
dotnet ef dbcontext scaffold "$CONN" Microsoft.EntityFrameworkCore.SqlServer --project src/Acity.Infrastructure --startup-project src/Acity.Api --context ScaffoldContext --no-onconfiguring --no-pluralize --namespace Acity.Domain.Entities --context-namespace Acity.Infrastructure.Persistence --output-dir "$OUT/autenticacion/Entities" --context-dir "$OUT/autenticacion/Context" --table dbo.Rol --table dbo.Usuario

# ms-control
dotnet ef dbcontext scaffold "$CONN" Microsoft.EntityFrameworkCore.SqlServer --project src/Acity.Infrastructure --startup-project src/Acity.Api --context ScaffoldContext --no-onconfiguring --no-pluralize --namespace Acity.Domain.Entities --context-namespace Acity.Infrastructure.Persistence --output-dir "$OUT/control/Entities" --context-dir "$OUT/control/Context" --table dbo.CargaArchivo --table dbo.CargaEstadoHistorial --table dbo.Incidencia

# ms-carga-masiva
dotnet ef dbcontext scaffold "$CONN" Microsoft.EntityFrameworkCore.SqlServer --project src/Acity.Infrastructure --startup-project src/Acity.Api --context ScaffoldContext --no-onconfiguring --no-pluralize --namespace Acity.Domain.Entities --context-namespace Acity.Infrastructure.Persistence --output-dir "$OUT/carga-masiva/Entities" --context-dir "$OUT/carga-masiva/Context" --table dbo.CargaArchivo --table dbo.CargaPeriodo --table dbo.Producto --table dbo.Incidencia

# ms-notificaciones
dotnet ef dbcontext scaffold "$CONN" Microsoft.EntityFrameworkCore.SqlServer --project src/Acity.Infrastructure --startup-project src/Acity.Api --context ScaffoldContext --no-onconfiguring --no-pluralize --namespace Acity.Domain.Entities --context-namespace Acity.Infrastructure.Persistence --output-dir "$OUT/notificaciones/Entities" --context-dir "$OUT/notificaciones/Context" --table dbo.Incidencia
```

Se verificó (F003 T034) este mismo mecanismo para `dbo.Rol`; las cuatro invocaciones de arriba **no se han ejecutado**. Salida esperada por servicio: una clase por tabla solicitada más `ScaffoldContext.cs`; nada más. Comprobar tras cada ejecución que `git status` no muestra archivos nuevos en el repositorio.

## Reglas de integración (Dec, Plan; research D-05)

Sin sobrescribir personalizaciones; cada paso se revisa a mano:

1. Para cada entidad del servicio, comparar la clase generada con lo que exista en `Acity.Domain/Entities`; si el archivo no existe, crearlo con la clase; si existe, fusionar a mano (no reemplazar).
2. **Quitar de las clases** las propiedades de navegación (referencias y colecciones), los `partial` y los `using` sin uso; conservar las propiedades de columna (todas las de la tabla).
3. **Convertir el mapeo del contexto generado** en una clase `IEntityTypeConfiguration<T>` por entidad en `Acity.Infrastructure/Persistence/Configurations`, conservando `ToTable`, `HasKey` (sin nombre de restricción), longitud, `IsUnicode(false)`, `IsFixedLength`, precisión, obligatoriedad, `ValueGeneratedNever` (`Rol.Id`) y la collation de `CodigoProducto`; **omitir** `HasName`, `HasConstraintName`, `HasIndex`, filtros y valores por defecto SQL.
4. Declarar relaciones solo entre entidades del mismo servicio con `HasOne<T>().WithMany().HasForeignKey(...)` (sin navegación); una clave foránea hacia una tabla no mapeada queda como propiedad de clave.
5. No copiar `ScaffoldContext` ni sus `DbSet`; el `AppDbContext` del servicio no cambia.
6. Compilar el servicio por su ruta (0 advertencias) y descartar la carpeta temporal.

## Comprobación manual de conexión y lectura (Dec, Plan, Verif; research D-06)

Un archivo `.cs` por servicio, **fuera del repositorio**, ejecutado con `dotnet run <archivo>.cs` (aplicación de archivo único de .NET 10) con las directivas `#:sdk Microsoft.NET.Sdk.Web`, `#:project <ruta absoluta a ms-<servicio>/src/Acity.Infrastructure/Acity.Infrastructure.csproj>` y `#:property PublishAot=false`, y `ConnectionStrings__Default` en el entorno. No añade endpoints, no cambia `/health`, no modifica el repositorio ni la base.

| Paso | Comprobación | Resultado esperado |
|---|---|---|
| 1 | `AddInfrastructure(configuration)` y resolución de `AppDbContext` | Sin excepción |
| 2 | `Database.CanConnectAsync()` | `True` |
| 3 | `SELECT DB_NAME()` | `CargaMasivaApp` |
| 4 | Cantidad de entidades del modelo | La del servicio: 2, 3, 4 y 1 |
| 5 | Para cada entidad: lectura LINQ `Set<T>().AsNoTracking().Take(1)` y `Count()` | Sin error; `Rol` con 2 filas; las demás vacías en la base nueva |
| 6 | Volcado del modelo por entidad (tabla, columna, tipo de almacén, nulabilidad, longitud, clave, generación de valores, claves foráneas) frente al catálogo de `CargaMasivaApp` (consulta de `sys.columns`, `sys.foreign_keys`, `sys.indexes`) | 0 diferencias sin registrar |

Verificado en esta etapa solo el mecanismo con el contexto actual de Control (sin entidades): conexión `True`, `DB_NAME=CargaMasivaApp`, 0 entidades. Los pasos 4 a 6 se ejecutan en la implementación. **Comparación LINQ de `CodigoProducto`** (research D-07): en una base temporal `CargaMasivaApp_*` creada por el runner, insertar por SQL filas de prueba y comprobar con LINQ que "p0117" encuentra "P0117" y "á" no iguala "a"; eliminar la base temporal al terminar.

## Fallo de conexión (Spec FR-006)

Con el manejo global existente, una excepción de conexión se responde como `Response<T>` de error genérico sin cadena, contraseña ni traza. Sin endpoint de datos no puede ejercitarse por HTTP; se comprueba en la implementación solo que la cadena no aparece en el log del servicio, y el cuerpo real queda como no ejecutado hasta que exista un punto de ejecución.

## Límite de validación (Dec)

Motor amd64 emulado en Apple silicon, sin soporte oficial de Microsoft: lo comprobado vale solo para este equipo; el equipo de evaluación queda sin validar.
