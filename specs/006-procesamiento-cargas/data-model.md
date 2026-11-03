# Data Model: Procesamiento de cargas en ms-carga-masiva

Esquema según `database/scripts/V001`–`V003`; único cambio: `V004__incidencia_archivo_invalido.sql` amplía `CK_Incidencia_Tipo` con `ArchivoInvalido` (contrato en `contracts/v004-incidencia-archivo-invalido.md`). Sin tablas ni columnas nuevas.

## Entidades de ms-carga-masiva tras esta feature

| Entidad | Origen | Columnas mapeadas | Escribe | Lee |
|---|---|---|---|---|
| `CargaArchivo` | F004 | todas | `IntentosProceso` (incremento de una sentencia); el estado solo por procedimiento | estado, `RutaArchivo`, `UsuarioId`, `FechaFin`, `IntentosProceso` |
| `CargaPeriodo` | F004 | todas | `Resultado = 'Completado'` y `FechaResultado` en la transacción del grupo | resultados de la carga |
| `Producto` | F004 | todas | inserta ganadores | códigos de otras cargas (por lotes) |
| `Incidencia` | F004 | todas | inserta las de fila, archivo, técnicas y `Existente` | existencia de incidencias sin periodo (idempotencia) |
| `Usuario` | **nueva** (ingeniería inversa, D-16) | `Id` INT PK, `Email` NVARCHAR(256) NOT NULL | nunca | correo del responsable |

Relación nueva: `CargaArchivo.UsuarioId` → `Usuario.Id` (sin navegaciones). Las demás relaciones de F004 no cambian. `CargaEstadoHistorial` no se mapea: el historial lo escribe `usp_CambiarEstadoCarga`.

## Valores que escribe el consumidor

**Producto** (ganador): `CodigoProducto` = código tras `Trim()` (NVARCHAR(100), collation `Latin1_General_100_CI_AS_WS`), `NombreProducto` = nombre o "Producto " + código (NVARCHAR(200)), `Precio` DECIMAL(18,2) exacto (0 si ausente), `CargaPeriodoId` = periodo de la fila.

**Incidencia** (`Tipo` VARCHAR(30), `Motivo` NVARCHAR(500) NOT NULL, `FechaRegistro` UTC):

| Tipo | Cuándo | `CargaPeriodoId` | `NumeroFila` | `CodigoProducto` | `PeriodoTexto` | Transacción |
|---|---|---|---|---|---|---|
| `FilaOmitida` | fila con dato ausente obligatorio o inválido | periodo si es válido y Admitido; NULL si el periodo falta o es inválido | fila del Excel | código limpio si existe (truncado a 100 con aviso) | valor original si falta o es inválido (truncado a 50 con aviso) | la del periodo, o "incidencias sin periodo" |
| `Existente` | código confirmado por otra carga, o duplicada de un ganador | periodo de la fila | fila | código | — | la del periodo de la fila (otra carga) o la del ganador (duplicada) |
| `NoProcesado` | duplicada de un periodo Completado cuyo ganador no se persistió | periodo de la fila | fila | código | — | fase de fallo (D-14) |
| `ArchivoInvalido` | archivo corrupto, sin hojas o sin encabezados | NULL | NULL | NULL | NULL | "incidencias sin periodo" |
| `ErrorTecnico` | fallo terminal; notificación sin confirmación | periodo fallido o NULL | NULL | NULL | NULL | propia, tras revertir |
| `PeriodoRechazado`, `PeriodoBloqueado` | las inserta `usp_ReservarPeriodo` | — | — | — | — | del procedimiento |
| `NoProcesado` (de periodo) | las inserta `usp_CambiarEstadoCarga` al pasar a Fallido | — | — | — | — | del procedimiento |

## Recorridos de la carga en esta feature

| Situación | Estados (historial) | Periodos | Notificación |
|---|---|---|---|
| Procesamiento normal (con o sin omisiones de negocio) | Pendiente → En proceso → Cargado → Finalizado | Completado / Rechazado / Bloqueado | sí |
| Todos los periodos rechazados o bloqueados | igual | Rechazado / Bloqueado | sí |
| Archivo inválido | igual | ninguno | sí |
| Archivo inexistente o descarga agotada | Pendiente → En proceso → Fallido | ninguno | no |
| Fallo terminal en un periodo | … → En proceso → Fallido | Completado conservados; el fallido Fallido; resto NoProcesado | no |
| Intentos agotados en recuperación | … → En proceso → Fallido | Completado conservados; Admitido → NoProcesado | no |
| Notificación sin confirmación | … → Finalizado (se queda) | según el caso | no garantizada (`ErrorTecnico`) |
| Mensaje de carga no Pendiente | sin cambios | sin cambios | no |

Todas las transiciones las aplica `usp_CambiarEstadoCarga`; la toma usa `@EstadoEsperado = 'Pendiente'` (V003).

## Opciones de configuración

| Opción | Variable de entorno | Predeterminado | Validación al arrancar |
|---|---|---|---|
| `Procesamiento.MaxIntentos` | `Procesamiento__MaxIntentos` | 3 | > 0 |
| `RabbitMq.*` | `RabbitMq__Host/Puerto/Usuario/Clave` | puerto 5672 | no vacías |
| `SeaweedFs.FilerUrl` | `SeaweedFs__FilerUrl` | — | URI absoluta |
