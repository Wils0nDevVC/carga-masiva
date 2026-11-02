# Data Model: modelo EF Core de Control y esquema de la base compartida

El esquema, sus reglas y los procedimientos son los de la Feature 001 y de `referencias/V001__esquema_inicial.sql` y `V002__procedimientos.sql` (idénticos a los que verificó el runner anterior). Aquí solo se fija **cómo se mapean** en EF Core dentro de `ms-control`. No se cambian tablas, roles ni reglas. Confirmado: cada usuario tiene un solo rol mediante `Usuario.RolId`; no existe tabla intermedia. [Dec]

## Propietario y alcance

| Servicio | Modelo EF | Migraciones |
|---|---|---|
| ms-control | Las 7 entidades de abajo, en `Acity.Domain`; configuraciones en `Acity.Infrastructure/Persistence/Configurations` | Sí: únicas de la base |
| ms-autenticacion | `AppDbContext` sin entidades por ahora; sus features añadirán solo lo necesario (p. ej. `Usuario`, `Rol`) | No |
| ms-carga-masiva | `AppDbContext` sin entidades por ahora | No |
| ms-notificaciones | `AppDbContext` sin entidades por ahora | No |

Las entidades son clases sin propiedades de navegación (relaciones con `HasOne<T>().WithMany().HasForeignKey(...)`), salvo que una feature las necesite. Sin `BaseEntity`. `SchemaMigraciones` **no** se modela: el historial lo lleva EF Core (`__EFMigrationsHistory`) y no es parte del modelo de negocio. [Dec]

## Entidades y mapeo (equivalencia con V001)

Se usan nombres explícitos de restricciones e índices iguales a V001 (`PK_*`, `FK_*`, `CK_*`, `DF_*`, `UQ_*`, `UX_*`, `IX_*`) porque V002 los menciona al detectar colisiones.

| Entidad (tabla `dbo`) | Campos y mapeo | Restricciones e índices |
|---|---|---|
| **Rol** | `Id` smallint (sin identidad), `Nombre` nvarchar(50) | `PK_Rol`; único `UQ_Rol_Nombre`; datos base `HasData`: (1, Administrador), (2, Operador) |
| **Usuario** | `Id` int identity, `Email` nvarchar(256), `PasswordHash` varchar(255) (no Unicode), `RolId` smallint | `PK_Usuario`; único `UQ_Usuario_Email`; `FK_Usuario_Rol` |
| **CargaArchivo** | `Id` bigint identity, `NombreArchivo` nvarchar(260), `UsuarioId` int, `FechaRecepcion` datetime2, `RutaArchivo` nvarchar(500), `Estado` varchar(20), `FechaFin` datetime2 nulo, `IntentosProceso` int | `PK_CargaArchivo`; `FK_CargaArchivo_Usuario`; `CK_CargaArchivo_Estado` ∈ {Pendiente, En proceso, Cargado, Finalizado, Notificado, Fallido}; `DF_CargaArchivo_IntentosProceso` = 0; índice `IX_CargaArchivo_Usuario_FechaRecepcion` (`UsuarioId`, `FechaRecepcion`) |
| **CargaPeriodo** | `Id` bigint identity, `CargaId` bigint, `Periodo` char(7), `Resultado` varchar(20), `PeriodoReservado` char(7) nulo, `FechaResultado` datetime2 | `PK_CargaPeriodo`; `FK_CargaPeriodo_Carga`; único `UQ_CargaPeriodo_Carga_Periodo` (`CargaId`, `Periodo`); `CK_CargaPeriodo_Periodo` (formato AAAA-MM, mes 01–12); `CK_CargaPeriodo_Resultado` ∈ {Admitido, Completado, Rechazado, Bloqueado, Fallido, NoProcesado}; `CK_CargaPeriodo_Reserva` (Admitido/Completado ⇒ `PeriodoReservado IS NOT NULL AND = Periodo`; otro ⇒ NULL); **índice único filtrado** `UX_CargaPeriodo_PeriodoReservado` con filtro `[PeriodoReservado] IS NOT NULL` |
| **Producto** | `Id` bigint identity, `CodigoProducto` nvarchar(100) con collation `Latin1_General_100_CI_AS_WS`, `NombreProducto` nvarchar(200), `Precio` decimal(18,2), `CargaPeriodoId` bigint | `PK_Producto`; `FK_Producto_CargaPeriodo`; **índice único global** `UX_Producto_CodigoProducto` sobre `CodigoProducto` (sin llave compuesta con el periodo) |
| **Incidencia** | `Id` bigint identity, `CargaId` bigint, `CargaPeriodoId` bigint nulo, `Tipo` varchar(30), `NumeroFila` int nulo, `CodigoProducto` nvarchar(100) nulo, `PeriodoTexto` nvarchar(50) nulo, `Motivo` nvarchar(500), `FechaRegistro` datetime2 | `PK_Incidencia`; `FK_Incidencia_Carga`; `FK_Incidencia_CargaPeriodo`; `CK_Incidencia_Tipo` ∈ {PeriodoRechazado, PeriodoBloqueado, FilaOmitida, Existente, NoProcesado, ErrorTecnico, ErrorNotificacion}; índice `IX_Incidencia_Carga_Id` (`CargaId`, `Id`) |
| **CargaEstadoHistorial** | `Id` bigint identity, `CargaId` bigint, `Estado` varchar(20), `FechaCambio` datetime2 | `PK_CargaEstadoHistorial`; `FK_CargaEstadoHistorial_Carga`; `CK_CargaEstadoHistorial_Estado` (mismos seis estados); índice `IX_CargaEstadoHistorial_Carga_Id` (`CargaId`, `Id`) |

Sin columnas `MAX` ni binarias. Las tres columnas de texto con longitud propuesta conservan las de V001.

## Diferencias físicas aceptadas respecto de V001
- Las unicidades `UQ_Rol_Nombre`, `UQ_Usuario_Email` y `UQ_CargaPeriodo_Carga_Periodo` se materializan como **índices únicos** (EF Core no crea restricciones UNIQUE); la garantía es la misma. Un choque produce el error 2601 en lugar del 2627; `usp_ReservarPeriodo` ya trata ambos números y comprueba el nombre. [Plan]
- Sin cambios en los datos base ni en las reglas de los procedimientos.

## Procedimientos
`usp_ReservarPeriodo` y `usp_CambiarEstadoCarga`, con el mismo contrato que en la Feature 001 (`contracts/procedimientos-almacenados.md` de esa feature), se crean en la migración `StoredProcedures` con el SQL de `V002` (una llamada `Sql` por procedimiento). Sus mensajes de error y números (50010–50022) no cambian.

## Verificación de equivalencia (en la implementación)
Sobre una base nueva y vacía, comparar con consultas de catálogo el resultado migrado con la referencia: tablas, columnas y tipos, collation de `CodigoProducto`, nulabilidad, valores por defecto, restricciones CHECK y claves foráneas, índices (incluido el filtro), roles base y existencia de los dos procedimientos. Después repetir manualmente los escenarios de base de la Feature 001 (reserva, índice filtrado, comparación de códigos, matriz de estados, fallo terminal). Ver `quickstart.md`.
