# Research: Procesamiento de cargas en ms-carga-masiva

**Procedencia**: `Dec` = decisión del usuario o de la constitución v3.2.0; `Spec` = requisito de `spec.md`; `F001/F004/F005` = features anteriores; `Plan` = decisión técnica de este plan, la alternativa más sencilla compatible (sin aprobación individual, por indicación del usuario); `Verif` = comprobado en esta etapa; `Sin verif` = conocido pero se comprueba en la implementación.

## D-01 Paquetes y versiones

| Paquete | Versión | Proyecto | Licencia | Comprobación |
|---|---|---|---|---|
| `ClosedXML` | 0.105.1 | Infrastructure | MIT | Verif (NuGet: estable más reciente, netstandard2.0/2.1, publicado 2026-07-25) |
| `RabbitMQ.Client` | 7.2.2 | Infrastructure | Apache-2.0 OR MPL-2.0 | Verif (mismo que la 005) |
| `Polly.Core` | 8.9.0 | Infrastructure | BSD-3-Clause | Verif (mismo que la 005) |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.14 | Application | MIT | Verif (mismo criterio que la 005) |

- Infrastructure recibe `FrameworkReference Microsoft.AspNetCore.App` (enlace de opciones e `IHttpClientFactory`), como en la 005. Sin otros paquetes. [Plan] [F005]
- **Dependencia transitiva a vigilar**: ClosedXML depende de `SixLabors.Fonts` `[1.0.0, 3.0.0)`. NuGet resuelve la versión mínima (1.0.0); las versiones 1.x se publicaron con Apache-2.0 y las 2.x con la licencia "Six Labors Split". **Comprobar en la implementación** la versión resuelta (`dotnet list package --include-transitive`) y no forzar una 2.x. [Sin verif]

## D-02 Estructura del consumidor

- `Acity.Api/Consumers/ConsumidorCargasMasivas.cs`: `BackgroundService` con un bucle de vida: (1) obtener el cerrojo (D-03); (2) recuperar (D-08); (3) abrir canal, declarar la topología, `BasicQos(prefetchCount: 1)` y consumir con confirmación manual; (4) ante pérdida del cerrojo, error de conexión o excepción no controlada al procesar: cerrar el canal sin confirmar el mensaje en curso, esperar 10 s y volver a (1). Al detenerse el host, cierra todo. [Spec FR-001–003] [Plan]
- Cada mensaje se procesa en un ámbito de DI nuevo, enviando `ProcesarCargaCommand` por MediatR (como la referencia). La recuperación envía `RecuperarCargasCommand`. El `BackgroundService` no contiene reglas de negocio. [Dec §3, §4] [Plan]
- Una segunda instancia sin cerrojo reintenta obtenerlo cada 15 s y no consume ni recupera mientras no lo tenga. Es un reintento de un recurso, no un servicio de barrido. [Spec US2-1] [Plan]
- `/health` sigue indicando solo que el host arrancó (no depende del cerrojo ni de RabbitMQ). [F002]

## D-03 Cerrojo de exclusividad

- Conexión dedicada `SqlConnection` con la misma cadena y `Pooling=false` (`SqlConnectionStringBuilder`), abierta durante toda la vida del consumidor. `EXEC @r = sp_getapplock @Resource = N'carga-masiva:consumidor-cargas', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0` → `@r >= 0` = obtenido; `-1` = otra instancia lo tiene. [F001 R-02, contrato "Cerrojo"] [Verif en F001: semántica de Session/timeout 0]
- **Vigencia antes de cada grupo** (pendiente en F001): `SELECT APPLOCK_MODE('public', N'carga-masiva:consumidor-cargas', 'Session')` sobre la conexión dedicada debe devolver `Exclusive`; si la conexión no está abierta o la consulta falla, el cerrojo se considera perdido → excepción `CerrojoPerdidoException` que detiene el procesamiento sin confirmar. [Plan] [Sin verif: comportamiento de `APPLOCK_MODE`]
- Interfaz `ICerrojoConsumidor` en Application (`IntentarObtenerAsync`, `ComprobarVigenteAsync`, `Liberar`); implementación en Infrastructure. [Dec §4]

## D-04 Mensaje de entrada y descarte

- Cuerpo JSON `{ idCarga, rutaArchivo, usuario }` (F005). Se deserializa con `System.Text.Json` (web). JSON inválido, `idCarga` ≤ 0 o carga inexistente → log de error, confirmar y descartar. [Spec FR-004]
- **Fuente de verdad**: `rutaArchivo` y el correo se toman de la base (`CargaArchivo.RutaArchivo`, `Usuario.Email`), igual que en la recuperación, que no tiene mensaje; si el mensaje difiere, log de advertencia. [Spec FR-021] [Plan]
- Carga en un estado distinto de Pendiente → log informativo y descarte (reentrega, duplicado de Control, mensaje tardío de Fallido). [Spec FR-004, FR-023]

## D-05 Flujo de procesamiento (`ProcesarCargaCommandHandler`)

Fases para una carga nueva (las de recuperación entran por la fase 2):

1. **Tomar**: `usp_CambiarEstadoCarga(Id, 'En proceso', @EstadoEsperado = 'Pendiente')`. `Cambiado = 0` → descartar (Control la dejó Fallido o ya se tomó). [Spec FR-005] [F005 V003]
2. **Intento**: `UPDATE CargaArchivo SET IntentosProceso = IntentosProceso + 1 WHERE Id = @id` (una sentencia, `ExecuteUpdateAsync`). Si el nuevo valor supera `Procesamiento:MaxIntentos` (predeterminado **3**) → fallo terminal "Intentos de procesamiento agotados". [F001 R-02 punto 5] [Spec FR-018] [Plan]
3. **Descargar** de SeaweedFS (`GET` del filer, D-06). 404 → `ArchivoNoEncontradoException` (sin reintentos) → fallo terminal. [Spec FR-008b]
4. **Leer** con ClosedXML (D-07). Archivo inválido → fase 9 con `ArchivoInvalido`. [Spec FR-008]
5. **Validar filas** y registrar las omisiones sin periodo válido (D-09).
6. **Reservar** cada periodo válido distinto, en orden de primera aparición, con `usp_ReservarPeriodo` (idempotente). [Spec FR-010]
7. **Decidir ganadores** sobre todo el archivo (D-10).
8. **Persistir** cada periodo Admitido en su transacción (D-11), comprobando antes el cerrojo.
9. **Cerrar**: `usp_CambiarEstadoCarga(Cargado)` y `(Finalizado)` (fija `FechaFin`). [Spec FR-015]
10. **Notificar** (D-13).
11. **Confirmar** el mensaje (lo hace el consumidor cuando el handler termina sin excepción).

Desde la fase 1 se usa `CancellationToken.None` para las escrituras (una parada del host no corta una transacción a mitad; el cierre espera al mensaje en curso con el tiempo de apagado del host). [Plan]

## D-06 Descarga desde SeaweedFS

- `GET {SeaweedFs__FilerUrl}/<ruta tras seaweed://>` a memoria (`MemoryStream`; el archivo es ≤ 10 MiB por Control). [F005 contrato] [Plan]
- **Ausencia confirmada**: respuesta 404 del filer → `ArchivoNoEncontradoException`, **no** se reintenta. **Transitorio**: `HttpRequestException` sin código, 408, 429, 5xx y tiempo de espera → reintento (D-12). Otro 4xx → error técnico sin reintento. [Dec Q1] [Spec FR-008b]
- Que el filer responda 404 a un archivo inexistente **se comprueba en la implementación** [Sin verif].

## D-07 Lectura del Excel con ClosedXML

- `new XLWorkbook(stream)`; cualquier excepción al abrir → archivo inválido ("No es un Excel legible"). Sin hojas → inválido. Se usa `Worksheets.First()`. [Dec Q1] [Spec FR-007, FR-008]
- **Encabezados**: fila 1; se buscan `Periodo`, `CodigoProducto`, `NombreProducto`, `Precio` por texto con `Trim()` y `OrdinalIgnoreCase`; falta alguno → inválido ("Faltan encabezados: …"). Columnas adicionales se ignoran. [Spec Assumptions]
- **Filas**: de la 2 a `LastRowUsed()`; el número de fila del Excel es el que se reporta. Fila completamente vacía (las cuatro celdas vacías o en blanco) → se omite sin incidencia. [README §3] [Spec FR-007, FR-009]
- **Texto de celda**: `GetFormattedString()` para Periodo, Código y Nombre (el texto mostrado; una fecha no se reinterpreta como periodo).
- **Precio**: celda numérica → `double` → `decimal.Parse(valor.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)` (representación decimal más corta, evita el ruido binario, F001 R-08); celda de texto → `decimal.TryParse` con `NumberStyles.Number` e `InvariantCulture`; error de fórmula o texto no numérico → inválido. [F001 R-08] [Plan]
- Comprobar con el Excel de ejemplo que 418.27 se lee como 418.27 [Sin verif].

## D-08 Recuperación (`RecuperarCargasCommandHandler`)

- Con el cerrojo, antes de consumir: cargas `En proceso` (por `Id`) → se reanudan desde la fase 2 (cuenta un intento); cargas `Cargado` → fases 9 (solo Finalizado) y 10. Las Pendiente no se tocan. [Spec FR-018] [F001 R-02]
- La reanudación es determinista: los periodos Completado se omiten y sus productos y sus `Existente` ya están confirmados (D-10); `usp_ReservarPeriodo` devuelve el resultado ya registrado.
- Idempotencia de las incidencias sin periodo: las `FilaOmitida` sin periodo y la `ArchivoInvalido` se insertan juntas en una transacción; si ya existen para la carga, no se vuelven a insertar. [Plan]
- **Limitación** (Dec Q2): una carga que llegó a Finalizado y cayó antes de publicar la notificación no se recupera (no se republican cargas Finalizado).

## D-09 Validación de filas (aplica F001 sin cambios)

Orden por fila: vacía → omitir; **Periodo**: `Trim()`; vacío → FilaOmitida "Periodo ausente"; no cumple `^\d{4}-(0[1-9]|1[0-2])$` → FilaOmitida "Periodo inválido"; **Código**: `Trim()`; vacío → "Código ausente"; > 100 → "Código supera 100 caracteres"; **Nombre**: vacío o en blanco → "Producto " + código; > 200 → "Nombre supera 200 caracteres"; **Precio**: vacío → 0; no numérico → "Precio no numérico"; más de 2 decimales con valor o más de 16 dígitos enteros → "Precio no representable en DECIMAL(18,2)". El nombre no se recorta (la constitución solo prescribe `Trim()` para el código). [Constitución §7] [F001 FR-024–028, FR-044, FR-047, R-07, R-08]

- Las omisiones de filas **sin periodo válido** no pertenecen a ningún grupo: se registran con `CargaPeriodoId` NULL en la transacción de "incidencias sin periodo" (D-08). Las omisiones de filas con periodo válido se registran en la transacción de su periodo si está Admitido. Si el periodo quedó Rechazado o Bloqueado, el grupo termina ahí: solo existe la incidencia de grupo (no se registran incidencias de sus filas). [Constitución §7 "termina el procesamiento de ese grupo"] [Plan]
- Truncado explícito en auditoría: `Incidencia.CodigoProducto` (100) y `PeriodoTexto` (50) se truncan y el motivo añade "(valor truncado)". [F001 R-08]

## D-10 Decisión de ganadores (F001 R-05)

- Candidatas: filas válidas de periodos Admitido o Completado de esta carga, agrupadas por código con `StringComparer.OrdinalIgnoreCase`.
- **Existentes de otras cargas**: consulta LINQ por lotes de 1 000 códigos de `Producto` cuyo periodo pertenece a otra carga (`CargaPeriodo.CargaId <> @id`); la comparación la hace la base con su collation (autoridad, F001 R-07). Esas filas → `Existente` en el grupo de la fila.
- Para cada código restante: gana la fila de menor número. Sus duplicadas (en cualquier periodo Admitido) → `Existente` **insertadas en la transacción del ganador**, con `CargaPeriodoId` del periodo de la duplicada.
- **Recuperación**: si el periodo del ganador ya está Completado, su producto y los `Existente` de sus duplicadas ya están confirmados → esas filas se omiten. Así no se duplican incidencias. [F001 R-05 "Reentrega/recuperación"]

## D-11 Transacción por periodo y colisiones

- Por periodo Admitido, en orden de primera aparición: comprobar el cerrojo; abrir transacción; releer `CargaPeriodo.Resultado` (si ya es Completado, omitir: idempotencia ante un commit ambiguo); insertar productos ganadores, `FilaOmitida` del periodo, `Existente` (las del periodo frente a otras cargas y las de duplicadas de sus ganadores); `Resultado = 'Completado'`, `FechaResultado`; un `SaveChanges`; commit. [Spec FR-012] [F001 FR-016, R-09]
- **Colisión** (error 2601/2627 cuyo mensaje nombra `UX_Producto_CodigoProducto`; otro índice o restricción = técnico): revertir y repetir el grupo en **modo fila a fila**: misma transacción, un `SaveChanges` por producto en orden de fila; EF Core crea un punto de guardado por `SaveChanges` dentro de una transacción del usuario y vuelve a él si falla [Sin verif en esta sesión]; un producto que colisiona se separa del contexto y su fila pasa a `Existente` (del producto confirmado con el que chocó); el resto del grupo sigue. Esto también cubre la discrepancia entre `OrdinalIgnoreCase` y la collation dentro del mismo archivo (p. ej. "ß"/"ss"). [Spec FR-013] [F001 FR-046, R-06, R-07]
- Error SQL transitorio → reintento del grupo completo (D-12); el inicio de cada intento vuelve a comprobar si el grupo ya está Completado.
- **Fallo terminal** de un grupo: revertir; fase de fallo (D-14) con `PeriodoFallido` = el periodo.

## D-12 Polly por operación

| Operación | Reintentos | Espera | Reintentables | Tiempo por intento | Circuit Breaker | Duplicación |
|---|---|---|---|---|---|---|
| Descarga del filer | 3 | exponencial 1 s, jitter | `HttpRequestException` sin código, 408, 429, 5xx, tiempo de espera | 60 s | 0,5 / 5 / 30 s / 30 s | Ninguna (lectura) |
| Procedimientos (`usp_*`), incremento de intentos, consultas | 3 | exponencial 1 s | `SqlException` transitoria (1205, -2, 53, 10053, 10054, 10060, 40613, 40197, 40501), `TimeoutException` | 30 s | 0,5 / 5 / 30 s / 30 s | Ninguna: procedimientos idempotentes; el incremento duplicado solo adelanta el límite (aceptable) |
| Transacción de un periodo | 3 | exponencial 1 s | igual que la anterior | 120 s | — (comparte el circuito SQL) | Ninguna: comprueba Completado al empezar cada intento |
| Publicación en `notificaciones` | 2 | exponencial 1 s | igual que en la 005 (`BrokerUnreachableException`, `AlreadyClosedException`, `OperationInterruptedException`, `PublishException` no `return`, tiempo de espera) | 5 s | 0,5 / 5 / 30 s / 30 s | Tolerada (Notificaciones descartará por estado, en su feature) |
| Conexión del consumidor | — (bucle del `BackgroundService`, 10 s) | — | — | — | — | — |

Nunca se reintentan: archivo inválido, ausencia confirmada (404), colisión (tiene su propio tratamiento), validación. Circuito abierto = fallo definitivo de esa operación. [Dec §9] [Spec FR-016]

## D-13 Notificación

- Topología declarada por el publicador de forma idempotente: intercambio directo durable `notificaciones`, cola durable `notificaciones`, clave `notificaciones`; mensaje persistente JSON, `mandatory = true`, confirmación con seguimiento (igual que la 005). [README §2 Cola 2, §4] [Plan]
- Cuerpo: `{ "idCarga": <long>, "usuario": "<Usuario.Email>", "fechaFin": "<CargaArchivo.FechaFin en UTC, ISO 8601 con Z>" }`. [README §3 paso 3] [Plan]
- Sin confirmación tras los reintentos → incidencia `ErrorTecnico`, motivo "No se obtuvo confirmación del intermediario para el mensaje de notificación; no se garantiza que se haya publicado ni que se notifique después." La carga sigue Finalizado; el mensaje de `carga_masiva` se confirma. [Dec Q2] [Spec FR-022]

## D-14 Fallo técnico terminal

1. Revertir lo no confirmado (lo hace la transacción).
2. Insertar `ErrorTecnico` (motivo con el tipo de error y la operación, sin secretos; `CargaPeriodoId` del periodo si se conoce).
3. `usp_CambiarEstadoCarga(Id, 'Fallido', @PeriodoFallido)`: deja Fallido el periodo indicado, `NoProcesado` los demás Admitido con su incidencia, libera reservas.
4. Para duplicadas de periodos **Completado** cuyo ganador estaba en un periodo no persistido → `NoProcesado` "ganador no persistido" (F001 FR-049).
5. Confirmar el mensaje. Sin notificación.

Si el propio registro del fallo falla, la excepción sube: el consumidor no confirma, espera y la recuperación retoma la carga (cuenta un intento). [Spec FR-017] [F001 R-03]

## D-15 Script V004

`database/scripts/V004__incidencia_archivo_invalido.sql`: `ALTER TABLE dbo.Incidencia DROP CONSTRAINT CK_Incidencia_Tipo;` y `ADD CONSTRAINT CK_Incidencia_Tipo CHECK (Tipo IN ('PeriodoRechazado','PeriodoBloqueado','FilaOmitida','Existente','NoProcesado','ErrorTecnico','ErrorNotificacion','ArchivoInvalido'))`. Mismo nombre de restricción; `ArchivoInvalido` (15) cabe en VARCHAR(30). Lo aplica el runner en su transacción. [Dec Q1] [Spec FR-008a]

## D-16 Usuario en ms-carga-masiva

Ingeniería inversa de `dbo.Usuario` (procedimiento de F004, manual) e integración parcial `Id` + `Email` (mínimo privilegio, como en Control); se añade la relación `HasOne<Usuario>().WithMany().HasForeignKey(e => e.UsuarioId)` en `CargaArchivoConfiguration`, sin navegaciones, porque ambas tablas quedan mapeadas en el servicio (regla de F004). [F004] [F005 D-03]

## D-17 Compose

`ms-carga-masiva`: `depends_on` añade `rabbitmq: service_healthy` y `seaweedfs: service_started`; variables `RabbitMq__Host/Puerto/Usuario/Clave`, `SeaweedFs__FilerUrl`; `Procesamiento__MaxIntentos` con valor predeterminado en `appsettings.json` (3). Sin servicios ni volúmenes nuevos. [Spec FR-025]

## Abiertos (estado tras la implementación; detalle en `quickstart.md`, "Resultados")

1. **Resuelto**: `SixLabors.Fonts` resuelto a 1.0.0, licencia Apache-2.0.
2. **Resuelto**: `sp_getapplock` de sesión con espera 0 deja fuera a una segunda instancia; con la sesión del cerrojo terminada (`KILL`), `ComprobarVigenteAsync` lanza `CerrojoPerdidoException` antes del siguiente periodo y el consumidor se detiene sin confirmar.
3. **Resuelto**: el filer responde 404 a un archivo inexistente.
4. **Resuelto**: precios sin ruido (P0117 = 418.27; P0047 almacenado en el Excel como `75.51000000000001` → 75.51).
5. **Resuelto**: EF Core usa puntos de guardado en el modo producto a producto; el error 2601 nombra `UX_Producto_CodigoProducto` (caso "ss"/"ß").
6. **Pendiente**: validación en el equipo de evaluación no realizada (motor amd64 emulado).

**Desviaciones de implementación** (mínimas, sin cambiar el diseño ni las reglas):
- `IRepositorioProcesamiento` (Application) con implementación EF en Infrastructure: Application no referencia EF Core y necesita consultas asíncronas, el incremento de una sentencia (`UPDATE … OUTPUT`), la transacción por periodo y la detección de la colisión. En consecuencia se retiró `IUnitOfWork.ExecuteInTransactionAsync` (T011), que quedaba sin uso.
- Precio en texto sin separador de miles (`NumberStyles` sin `AllowThousands`): "1,5" es no numérico, no 15.
- `Cargado` → `Finalizado` y la notificación quedan fuera del tratamiento de fallo terminal: si falla un cambio de estado, la excepción sube sin confirmar y la recuperación lo completa (constitución §8: Cargado no pasa a Fallido).
- `ConsumidorRabbitMq` espera al mensaje en curso antes de volver cuando se cierra el canal o se detiene el host (evita que la recuperación se solape con una ejecución viva).
- El pipeline de la transacción de un periodo no tiene circuit breaker: D-12 preveía compartir el circuito SQL, pero un pipeline de Polly no comparte estado con otro; se dejó con sus 3 reintentos limitados y sin circuito propio.
