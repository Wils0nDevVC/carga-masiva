# Data Model: tablas mapeadas por servicio y reglas de mapeo

El esquema, sus reglas y los procedimientos son los de la Feature 001 y de `referencias/V001__esquema_inicial.sql` / `V002__procedimientos.sql` (ya en `database/scripts/`, aplicados por el runner a `CargaMasivaApp`). **No se cambian tablas, columnas ni reglas.** Aquí solo se fija qué tablas y relaciones modela cada servicio en EF Core y cómo deben corresponder al esquema. Procedencia: `Dec`, `Plan`, `Obs` (leído de `V001`) como en `research.md`.

## Matriz de tablas por servicio (Dec, Spec)

| Tabla | ms-autenticacion | ms-control | ms-carga-masiva | ms-notificaciones |
|---|---|---|---|---|
| `Rol` | Sí | — | — | — |
| `Usuario` | Sí | — (diferido) | — | — |
| `CargaArchivo` | — | Sí | Sí | — (acceso por procedimiento) |
| `CargaEstadoHistorial` | — | Sí | — | — |
| `CargaPeriodo` | — | — (diferido) | Sí | — |
| `Producto` | — | — (diferido) | Sí | — |
| `Incidencia` | — | Sí | Sí | Sí |

Totales: 2 + 3 + 4 + 1 = 10 mapeos de 7 tablas. Ningún servicio mapea las siete; ninguna entidad se comparte como clase entre servicios (cada uno tiene la suya).

## Columnas por tabla (Obs: `V001`; tipo CLR esperado del mapeo)

Cada entidad mapea **todas las columnas de su tabla** (research D-05). Los tipos de almacén deben coincidir con los de la tabla.

| Tabla | Columna | Tipo SQL (`V001`) | Nulo | CLR esperado | Notas de mapeo |
|---|---|---|---|---|---|
| `Rol` | `Id` | SMALLINT, PK | No | `short` | **Sin identidad**: `ValueGeneratedNever` |
| | `Nombre` | NVARCHAR(50) | No | `string` | longitud 50 |
| `Usuario` | `Id` | INT IDENTITY, PK | No | `int` | identidad |
| | `Email` | NVARCHAR(256) | No | `string` | longitud 256 |
| | `PasswordHash` | VARCHAR(255) | No | `string` | **no Unicode** (`IsUnicode(false)`), longitud 255 |
| | `RolId` | SMALLINT, FK → `Rol` | No | `short` | |
| `CargaArchivo` | `Id` | BIGINT IDENTITY, PK | No | `long` | identidad |
| | `NombreArchivo` | NVARCHAR(260) | No | `string` | |
| | `UsuarioId` | INT, FK → `Usuario` | No | `int` | columna de clave (sin `Usuario` en Control ni en Carga Masiva) |
| | `FechaRecepcion` | DATETIME2 | No | `DateTime` | |
| | `RutaArchivo` | NVARCHAR(500) | No | `string` | |
| | `Estado` | VARCHAR(20) | No | `string` | no Unicode, longitud 20 |
| | `FechaFin` | DATETIME2 | Sí | `DateTime?` | |
| | `IntentosProceso` | INT, DEFAULT 0 | No | `int` | el valor por defecto del esquema no se replica en el modelo |
| `CargaEstadoHistorial` | `Id` | BIGINT IDENTITY, PK | No | `long` | |
| | `CargaId` | BIGINT, FK → `CargaArchivo` | No | `long` | |
| | `Estado` | VARCHAR(20) | No | `string` | no Unicode |
| | `FechaCambio` | DATETIME2 | No | `DateTime` | |
| `CargaPeriodo` | `Id` | BIGINT IDENTITY, PK | No | `long` | |
| | `CargaId` | BIGINT, FK → `CargaArchivo` | No | `long` | |
| | `Periodo` | CHAR(7) | No | `string` | longitud fija 7 (`IsFixedLength`, no Unicode) |
| | `Resultado` | VARCHAR(20) | No | `string` | no Unicode |
| | `PeriodoReservado` | CHAR(7) | Sí | `string?` | longitud fija 7 |
| | `FechaResultado` | DATETIME2 | No | `DateTime` | |
| `Producto` | `Id` | BIGINT IDENTITY, PK | No | `long` | |
| | `CodigoProducto` | NVARCHAR(100) **COLLATE Latin1_General_100_CI_AS_WS** | No | `string` | respeta la collation de la columna (D-07) |
| | `NombreProducto` | NVARCHAR(200) | No | `string` | |
| | `Precio` | DECIMAL(18,2) | No | `decimal` | precisión 18, escala 2 |
| | `CargaPeriodoId` | BIGINT, FK → `CargaPeriodo` | No | `long` | |
| `Incidencia` | `Id` | BIGINT IDENTITY, PK | No | `long` | |
| | `CargaId` | BIGINT, FK → `CargaArchivo` | No | `long` | |
| | `CargaPeriodoId` | BIGINT, FK → `CargaPeriodo` | Sí | `long?` | columna de clave donde `CargaPeriodo` no se mapea |
| | `Tipo` | VARCHAR(30) | No | `string` | no Unicode |
| | `NumeroFila` | INT | Sí | `int?` | |
| | `CodigoProducto` | NVARCHAR(100) | Sí | `string?` | |
| | `PeriodoTexto` | NVARCHAR(50) | Sí | `string?` | |
| | `Motivo` | NVARCHAR(500) | No | `string` | |
| | `FechaRegistro` | DATETIME2 | No | `DateTime` | |

## Relaciones por servicio (Plan)

Se declaran solo entre entidades mapeadas **en el mismo servicio**, por clave, sin propiedades de navegación (F002 y la entidad de la referencia). Una clave foránea hacia una tabla no mapeada queda como columna.

| Servicio | Relaciones declaradas | Columnas de clave sin relación (tabla no mapeada) |
|---|---|---|
| ms-autenticacion | `Usuario.RolId → Rol` | — |
| ms-control | `CargaEstadoHistorial.CargaId → CargaArchivo`; `Incidencia.CargaId → CargaArchivo` | `CargaArchivo.UsuarioId` (Usuario); `Incidencia.CargaPeriodoId` (CargaPeriodo) |
| ms-carga-masiva | `CargaPeriodo.CargaId → CargaArchivo`; `Producto.CargaPeriodoId → CargaPeriodo`; `Incidencia.CargaId → CargaArchivo`; `Incidencia.CargaPeriodoId → CargaPeriodo` | `CargaArchivo.UsuarioId` (Usuario) |
| ms-notificaciones | — | `Incidencia.CargaId` (CargaArchivo); `Incidencia.CargaPeriodoId` (CargaPeriodo) |

## Reglas de mapeo (Dec, Plan)

- **Es acceso, no esquema**: sin datos base sembrados, sin `HasIndex`, filtros, nombres de restricciones ni valores por defecto SQL, sin restricciones CHECK (las del esquema las aplica la base).
- **Sí se conserva** lo que afecta al acceso: `ToTable`, `HasKey`, longitudes, `IsUnicode(false)` en `varchar`, `IsFixedLength` en `char`, precisión de `decimal`, obligatoriedad, generación de valores (`Rol.Id` sin identidad; los demás `Id` con identidad) y la collation de `CodigoProducto`.
- **Ubicación**: entidad en `Acity.Domain/Entities`, un `IEntityTypeConfiguration<T>` por entidad en `Acity.Infrastructure/Persistence/Configurations`; el `AppDbContext` no cambia (`ApplyConfigurationsFromAssembly` los registra). Sin `DbSet`.
- **Procedimientos y `SchemaMigraciones`**: no son entidades.

## Verificación de correspondencia (en la implementación)

Por entidad y servicio: contrastar el volcado del modelo (tabla, columna, tipo de almacén, nulabilidad, longitud, clave, generación de valores y claves foráneas) con el catálogo de `CargaMasivaApp`, con 0 diferencias sin registrar. Ver `contracts/ingenieria-inversa-y-comprobacion.md` y `quickstart.md`.
