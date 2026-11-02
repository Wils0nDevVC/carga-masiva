# Data Model: Persistencia y reglas de integridad para la carga masiva

Modelo lógico/físico propuesto por el plan; `IntentosProceso`, `PeriodoReservado`, el hash y la condición de la collation están aceptados por el usuario. No es SQL ni configuración de EF Core. Procedencia: **Req** = requisito del README; **Dec** = decisión del usuario; **Prop** = propuesta de este plan. Ninguna longitud se deduce del Excel de ejemplo. Fechas `DATETIME2` en UTC.

## Tablas y justificación (8)

| Tabla | Para qué es necesaria | Requisito |
|---|---|---|
| Rol | Roles persistidos con nombre único | Dec: roles Administrador y Operador |
| Usuario | Identidad propia, responsable de cada carga, hash de contraseña | Dec; Req (README §3 auditoría de quién subió) |
| CargaArchivo | Trazabilidad del archivo, responsable, fecha, estado, referencia al archivo | Req (README §2, §5) |
| CargaEstadoHistorial | Historial de cambios de estado (conserva cada transición) | Dec; spec FR-035, FR-040 |
| CargaPeriodo | Resultado por periodo separado del estado de la carga; base de la reserva y de la protección de concurrencia | Dec; spec FR-008, FR-014 |
| Producto | Productos aceptados con unicidad global de código | Req (README §3) |
| Incidencia | Fallidos, existentes y errores técnicos persistentes | Req (README §3); spec FR-033, FR-034 |
| SchemaMigraciones | Registro de scripts aplicados (único responsable) | Req (README §4 migraciones); Dec |

Cambio frente a la versión anterior del plan: **se elimina `UsuarioRol`**. Ningún requisito exige varios roles por usuario; `Usuario.RolId` basta para "persistir usuarios, roles y su asociación". Si se aprueba más de un rol por usuario, se restituye una tabla puente.

## Relaciones

```text
Rol 1──N Usuario 1──N CargaArchivo 1──N CargaEstadoHistorial
CargaArchivo 1──N CargaPeriodo 1──N Producto
CargaArchivo 1──N Incidencia ; CargaPeriodo 1──N Incidencia (opcional)
SchemaMigraciones (técnica; sin relaciones)
```

## Campos

| Campo | Tipo | Null | Justificación | Procedencia |
|---|---|---|---|---|
| **Rol.Id** | SMALLINT | No | Catálogo mínimo (2 roles); PK | Prop |
| Rol.Nombre | NVARCHAR(50) UNIQUE | No | Nombre legible; "Administrador", "Operador" | Dec |
| **Usuario.Id** | INT IDENTITY | No | PK | Prop |
| Usuario.Email | NVARCHAR(256) UNIQUE | No | Identificador de acceso; el README usa un correo como usuario; 256 como cota práctica de un correo | Req (mensaje README §3) / Prop longitud |
| Usuario.PasswordHash | VARCHAR(255) | No | Una sola cadena autodescriptiva generada por `PasswordHasher` de ASP.NET Core Identity en ms-autenticacion; ASCII; la longitud se confirma allí | Dec: hash; Prop: tipo y longitud |
| Usuario.RolId | SMALLINT FK → Rol | No | Asociación usuario–rol | Dec / Prop (1 rol) |
| **CargaArchivo.Id** (`idCarga`) | BIGINT IDENTITY | No | PK; identifica la carga | Req |
| CargaArchivo.NombreArchivo | NVARCHAR(260) | No | Nombre original del archivo; 260 = longitud máxima de ruta clásica de Windows | Req (README §5) / Prop longitud |
| CargaArchivo.UsuarioId | INT FK → Usuario | No | Quién subió el archivo | Req |
| CargaArchivo.FechaRecepcion | DATETIME2 | No | Cuándo | Req |
| CargaArchivo.RutaArchivo | NVARCHAR(500) | No | Referencia a SeaweedFS; no se guarda binario | Dec / Prop longitud |
| CargaArchivo.Estado | VARCHAR(20) CHECK ∈ 6 estados | No | Estado general; sin tabla de catálogo (valores fijos) | Req + Dec |
| CargaArchivo.FechaFin | DATETIME2 | Sí | Se fija al llegar a Finalizado | Req (fechaFin del mensaje) |
| CargaArchivo.IntentosProceso | INT DEFAULT 0 | No | Cuenta las ejecuciones iniciadas para acotar los reintentos de recuperación (el límite lo fija la feature de Carga Masiva); sin otra responsabilidad | Dec (aceptada); ver research R-02 |
| **CargaEstadoHistorial.Id** | BIGINT IDENTITY | No | PK | Prop |
| CargaEstadoHistorial.CargaId | BIGINT FK | No | | Dec |
| CargaEstadoHistorial.Estado | VARCHAR(20) mismo CHECK | No | Estado alcanzado | Dec |
| CargaEstadoHistorial.FechaCambio | DATETIME2 | No | Orden y momento | Dec |
| **CargaPeriodo.Id** | BIGINT IDENTITY | No | PK | Prop |
| CargaPeriodo.CargaId | BIGINT FK | No | Asociación de periodos con la carga | Dec |
| CargaPeriodo.Periodo | CHAR(7) CHECK formato AAAA-MM | No | Periodo ya validado; longitud fija del formato aprobado | Dec |
| CargaPeriodo.Resultado | VARCHAR(20) CHECK ∈ {Admitido, Completado, Rechazado, Bloqueado, Fallido, NoProcesado} | No | Resultado por periodo, distinto del estado de la carga. `NoProcesado` = periodo pendiente cuando la carga se interrumpió; no afirma que se procesó y falló | Dec |
| CargaPeriodo.PeriodoReservado | CHAR(7) | Sí | Igual a Periodo mientras Resultado ∈ {Admitido, Completado}; NULL en otro caso. Existe porque el índice filtrado solo admite comparaciones simples (ver abajo) | Dec (aceptada) |
| CargaPeriodo.FechaResultado | DATETIME2 | No | Momento del resultado | Prop |
| **Producto.Id** | BIGINT IDENTITY | No | PK | Prop |
| Producto.CodigoProducto | NVARCHAR(100), collation CI sin equivalencia de acentos ni ancho | No | Código con `Trim()`; único global; 100 es una cota propuesta, no deducida del Excel; si el código recortado excede 100, la fila se omite (Dec) | Req + Dec / Prop longitud |
| Producto.NombreProducto | NVARCHAR(200) | No | Con valor por defecto aplicado por la aplicación; cota propuesta; si excede 200, la fila se omite (Dec) | Req / Prop longitud |
| Producto.Precio | DECIMAL(18,2) | No | Aprobado; 0 si ausente lo aplica la aplicación; un precio no representable exactamente omite la fila (Dec); la base no redondea para protegerlo, lo valida la aplicación | Dec |
| Producto.CargaPeriodoId | BIGINT FK | No | Da la carga y el periodo de origen | Dec |
| **Incidencia.Id** | BIGINT IDENTITY | No | PK | Prop |
| Incidencia.CargaId | BIGINT FK | No | Vinculada a la carga | Req |
| Incidencia.CargaPeriodoId | BIGINT FK | Sí | Nulo si el periodo es ausente o inválido | Prop |
| Incidencia.Tipo | VARCHAR(30) CHECK ∈ {PeriodoRechazado, PeriodoBloqueado, FilaOmitida, Existente, NoProcesado, ErrorTecnico, ErrorNotificacion} | No | Distingue grupo, fila y técnica; `NoProcesado` reporta periodos o filas que no llegaron a procesarse (motivo: interrupción de la carga o ganador no persistido) | Dec (FR-034, FR-048, FR-049) |
| Incidencia.NumeroFila | INT | Sí | Fila del Excel | Prop |
| Incidencia.CodigoProducto | NVARCHAR(100) | Sí | Código afectado (limpio); si excede la longitud se guarda truncado y el Motivo lo indica | Prop |
| Incidencia.PeriodoTexto | NVARCHAR(50) | Sí | Valor original cuando falta o es inválido; truncado explícito si excede | Prop |
| Incidencia.Motivo | NVARCHAR(500) | No | Texto generado por el sistema, acotado | Req (motivo) / Prop longitud |
| Incidencia.FechaRegistro | DATETIME2 | No | | Prop |
| SchemaMigraciones.Script | NVARCHAR(200) PK | No | Nombre versionado del script | Req / Prop |
| SchemaMigraciones.Checksum | CHAR(64) | No | Detecta scripts aplicados que cambiaron | Prop |
| SchemaMigraciones.FechaAplicado | DATETIME2 | No | | Prop |

Sin tamaños ilimitados (`MAX`): ningún campo lo necesita. Sin columna binaria del Excel.

## Restricciones e índices

- `Usuario.Email` y `Rol.Nombre` únicos.
- `CargaArchivo(UsuarioId, FechaRecepcion)`: consulta del Operador sobre sus cargas.
- `CargaEstadoHistorial(CargaId, Id)`; `Incidencia(CargaId, Id)`.
- `CargaPeriodo`: UNIQUE `(CargaId, Periodo)` (un resultado por periodo y carga; idempotencia).
- **Índice único filtrado** `CargaPeriodo(PeriodoReservado) WHERE PeriodoReservado IS NOT NULL`, más un CHECK que ata el campo al resultado: `(Resultado IN ('Admitido','Completado') AND PeriodoReservado = Periodo) OR (Resultado NOT IN ('Admitido','Completado') AND PeriodoReservado IS NULL)`.
  - Protege exactamente los resultados que reservan el periodo: solo Admitido y Completado tienen valor; Rechazado, Bloqueado, Fallido y NoProcesado tienen NULL y quedan fuera del índice.
  - Por qué no `WHERE Resultado IN (...)`: la documentación oficial indica que los índices filtrados admiten solo comparaciones simples y que no pueden usar columnas calculadas en el filtro; no se pudo confirmar `IN`/`OR`, así que se evita. `IS NOT NULL` es una comparación simple admitida. Los filtros pueden aplicarse a índices con la propiedad UNIQUE, pero no a restricciones PK/UNIQUE (por eso es un índice, no una restricción).
  - Opciones `SET` de sesión: el filtrado exige las opciones de `CREATE INDEX` (la documentación remite a ellas). `sqlcmd` (ODBC) deja `QUOTED_IDENTIFIER` en OFF por defecto: los scripts MUST iniciar con `SET QUOTED_IDENTIFIER ON` (o usar `-I`). EF Core/SqlClient lo dejan en ON por defecto (no verificado en esta sesión).
  - Transacciones: la reserva se confirma en su propia transacción corta, antes de la del grupo. El paso Admitido → Completado dentro de la transacción del grupo no cambia la clave del índice, por lo que no genera contención ni nueva colisión.
- **`Producto.CodigoProducto`**: índice único global, sin llave compuesta con periodo; collation explícita `Latin1_General_100_CI_AS_WS` (propuesta; nombre exacto por verificar al escribir el script): insensible a mayúsculas, **sensible a acentos** y al ancho de caracteres, para no introducir equivalencias adicionales a la aprobada.
  - La aplicación aplica `Trim()` de .NET antes de comprobar longitud, comparar o guardar; la base compara sin distinguir mayúsculas. SQL Server ignora los espacios finales al comparar texto; tras `Trim()` no hay efecto práctico, y no afecta a espacios internos.
  - En memoria se compara con `OrdinalIgnoreCase`; puede diferir de la collation en caracteres excepcionales. La base es la autoridad: una discrepancia aparece como colisión y se trata como Existente (research R-06).

## Reglas y transiciones

Estados de la carga (tabla de transiciones aplicada por `usp_CambiarEstadoCarga`):

| Desde \ Hacia | En proceso | Cargado | Finalizado | Notificado | Fallido |
|---|---|---|---|---|---|
| Pendiente | ✔ | ✘ | ✘ | ✘ | ✔ |
| En proceso | (repetición = sin cambio) | ✔ | ✘ | ✘ | ✔ |
| Cargado | ✘ | (sin cambio) | ✔ | ✘ | ✘ |
| Finalizado | ✘ | ✘ | (sin cambio) | ✔ | ✘ |
| Notificado | terminal | | | | |
| Fallido | terminal | | | | |

Cada transición aplicada inserta una fila de historial (incluido el estado inicial Pendiente, que inserta la aplicación al crear la carga en la misma transacción). Una transición no permitida o repetida no escribe nada. Origen: decisión del usuario (matriz), salvo que la repetición idempotente es propuesta.

Resultados de periodo: (nuevo) → Admitido → Completado | Fallido | NoProcesado; (nuevo) → Rechazado | Bloqueado. Fallido = el periodo en que ocurrió el fallo terminal; NoProcesado = Admitido que quedó pendiente al interrumpirse la carga. Nunca cambian por sí solos el estado de la carga.

## Trazabilidad con la spec

| Requisito | Estructura |
|---|---|
| FR-001–005 | Rol, Usuario |
| FR-006–008 | CargaArchivo, CargaPeriodo |
| FR-009–015 | CargaPeriodo (índice filtrado) + `usp_ReservarPeriodo` |
| FR-016–018, FR-022, FR-046 | Transacción EF por grupo, Producto, CargaPeriodo.Resultado |
| FR-019–023, FR-044–045 | Producto (UNIQUE global, collation) + algoritmo de orden (research R-05) |
| FR-024–028, FR-047 | Incidencia (FilaOmitida); tipos y longitudes |
| FR-048–049 | CargaPeriodo.Resultado (Fallido, NoProcesado), Incidencia (NoProcesado, Existente), `usp_CambiarEstadoCarga` |
| FR-029–031, FR-035–037, FR-040–041 | CargaArchivo.Estado, CargaEstadoHistorial, `usp_CambiarEstadoCarga` |
| FR-033–034, FR-038–039 | Incidencia |
| FR-042–043 | Cerrojo de sesión y recuperación (research R-02) |
