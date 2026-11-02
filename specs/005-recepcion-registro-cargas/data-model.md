# Data Model: Recepción y registro de cargas en ms-control

Sin tablas, columnas ni estados nuevos. Esquema según `database/scripts/V001__esquema_inicial.sql`; el único cambio de base es el procedimiento (V003, ver `contracts/usp-cambiar-estado-carga-v003.md`).

## Entidades de Control tras esta feature

| Entidad | Origen | Columnas mapeadas | Escribe | Lee |
|---|---|---|---|---|
| `CargaArchivo` | F004 (sin cambios de columnas) | todas | inserta al registrar | — |
| `CargaEstadoHistorial` | F004 | todas | inserta Pendiente al registrar (Fallido lo inserta el procedimiento) | — |
| `Incidencia` | F004 | todas | inserta `ErrorTecnico` si el procedimiento aplicó Fallido | — |
| `Usuario` | **nueva en Control** (ingeniería inversa, D-03) | solo `Id` INT IDENTITY PK, `Email` NVARCHAR(256) NOT NULL | nunca | por `Id` (`sub`) |

Relación nueva entre entidades mapeadas del mismo servicio: `CargaArchivo.UsuarioId` INT NOT NULL → `Usuario.Id` (`HasOne<Usuario>().WithMany().HasForeignKey(e => e.UsuarioId)`), sin navegaciones. `PasswordHash` y `RolId` no se mapean (excepción a la regla de F004 justificada por spec FR-009).

## Valores que Control escribe

**CargaArchivo** (al registrar):

| Columna | Restricción del esquema | Valor |
|---|---|---|
| `Id` | BIGINT IDENTITY | generado → `idCarga` |
| `NombreArchivo` | NVARCHAR(260) NOT NULL | `Path.GetFileName` del archivo recibido, sin truncar (> 260 ya rechazado con 400) |
| `UsuarioId` | INT NOT NULL, FK `Usuario` | `sub` del token (usuario comprobado) |
| `FechaRecepcion` | DATETIME2 NOT NULL | `DateTime.UtcNow` al registrar |
| `RutaArchivo` | NVARCHAR(500) NOT NULL | `seaweed://cargas/{AAAA}/{MM}/{guid}.xlsx` |
| `Estado` | VARCHAR(20) NOT NULL, CHECK de los seis estados | `Pendiente` |
| `FechaFin` | DATETIME2 NULL | NULL |
| `IntentosProceso` | INT NOT NULL | 0 (explícito; no se depende del DEFAULT) |

**CargaEstadoHistorial** (misma transacción): `CargaId` = `Id` de la carga, `Estado` = `Pendiente`, `FechaCambio` = la misma `FechaRecepcion`.

**Incidencia** (solo si `usp_CambiarEstadoCarga` devolvió `Cambiado = 1` al pasar a Fallido):

| Columna | Restricción | Valor |
|---|---|---|
| `CargaId` | BIGINT NOT NULL | `idCarga` |
| `CargaPeriodoId` | BIGINT NULL | NULL |
| `Tipo` | VARCHAR(30) CHECK ∈ {…, `ErrorTecnico`, …} | `ErrorTecnico` |
| `NumeroFila`, `CodigoProducto`, `PeriodoTexto` | NULL | NULL |
| `Motivo` | NVARCHAR(500) NOT NULL | "No se pudo confirmar la publicación del mensaje de procesamiento." |
| `FechaRegistro` | DATETIME2 NOT NULL | `DateTime.UtcNow` |

## Estados que Control puede producir

| Situación | Estado final | Historial | Incidencia |
|---|---|---|---|
| Rechazo previo (rate limit, 401, 403, 400, 413, usuario inexistente) | sin carga | — | — |
| Fallo definitivo de subida | sin carga | — | — |
| Fallo del registro | sin carga (archivo huérfano en SeaweedFS) | — | — |
| Publicación confirmada | Pendiente | Pendiente | — |
| Fallo definitivo de publicación, carga aún Pendiente | Fallido | Pendiente, Fallido | `ErrorTecnico` |
| Fallo definitivo de publicación, carga ya fuera de Pendiente | el que tenga (lo fijó el consumidor) | sin entrada de Control | — |
| Fallo también al registrar Fallido | Pendiente | Pendiente | — (solo log) |

Transición usada: Pendiente → Fallido, permitida por la matriz [Constitución §8] y condicionada a `@EstadoEsperado = 'Pendiente'`.

## Opciones de configuración (no persistidas)

| Opción | Variable de entorno | Predeterminado | Validación al arrancar |
|---|---|---|---|
| `CargaArchivo.TamanoMaximoBytes` | `CargaArchivo__TamanoMaximoBytes` | 10 485 760 | > 0 |
| `Jwt.Clave` / `Emisor` / `Audiencia` | `Jwt__Clave`, `Jwt__Emisor`, `Jwt__Audiencia` | — (obligatorias) | clave ≥ 32 bytes; no vacías |
| `RabbitMq.Host` / `Puerto` / `Usuario` / `Clave` | `RabbitMq__*` | puerto 5672 | no vacías |
| `SeaweedFs.FilerUrl` | `SeaweedFs__FilerUrl` | — (obligatoria) | URI absoluta |
| `RateLimiting.LimitePorVentana` / `VentanaSegundos` | `RateLimiting__*` | 10 / 60 | > 0 |
