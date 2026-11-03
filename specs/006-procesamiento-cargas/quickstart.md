# Quickstart: validación manual del procesamiento de cargas (ms-carga-masiva)

Guía de verificación manual (sin pruebas automatizadas, constitución §11). Contratos en `contracts/`; tipos, recorridos e incidencias en `data-model.md`. **Ningún escenario se ha ejecutado todavía.**

## Reglas de la verificación

- Todas las cargas y productos de prueba van a una **base temporal** `CargaMasivaApp_F006` creada por el runner (V001–V004). `CargaMasivaApp` solo recibe V004 con el arranque normal. `CargaMasiva`, `CargaMasivaDb` y los volúmenes ajenos no se tocan; nunca `docker compose down -v`.
- Secretos solo por entorno; cargar de `.env` solo `RABBITMQ_*` y `JWT_*` (no `MSSQL_SA_PASSWORD`, que en este equipo difiere del volumen; ver 005).
- Tokens de prueba con un generador **fuera del repositorio** (el `gen-token.cs` de la 005 o uno equivalente si el scratchpad ya no existe).
- Se usan Control (subida real) y ms-carga-masiva, ambos con `DB_NAME=CargaMasivaApp_F006`. Sin consumidor de `notificaciones`: los mensajes se inspeccionan en la gestión de RabbitMQ (15672).
- Validado solo en este equipo (SQL Server amd64 emulado).

## Preparación

1. `docker compose build database-runner ms-control ms-carga-masiva`.
2. `docker compose run --rm --no-deps -e DB_NAME=CargaMasivaApp_F006 database-runner` (V001–V004; `SchemaMigraciones` = 4).
3. Usuarios de prueba en la base temporal (Administrador y Operador, `PasswordHash` = `'sin-credencial-prueba'`).
4. `DB_NAME=CargaMasivaApp_F006 docker compose up -d --build ms-control ms-carga-masiva`.
5. Archivos de prueba (fuera del repositorio): el Excel de ejemplo; una variante con las filas de US1-5; uno con periodos duplicados intercalados (US1-4); uno corrupto (bytes aleatorios con `.xlsx`); uno sin la columna `Precio`; uno con solo encabezados; uno grande (≈ 50 000 filas, para provocar caídas y carreras con la carga en curso).

## Escenarios

| # | Escenario | Pasos | Resultado esperado | Spec |
|---|---|---|---|---|
| 1 | V004 | En la base temporal: insertar `ArchivoInvalido` y un tipo inexistente | el primero se acepta, el segundo lo rechaza el `CHECK` | FR-008a |
| 2 | Flujo completo | Subir el Excel de ejemplo | historial Pendiente → En proceso → Cargado → Finalizado; 3 periodos Completado; productos = códigos distintos; `FechaFin`; 1 mensaje en `notificaciones` con `idCarga`, `usuario`, `fechaFin`; precios iguales a los del Excel (p. ej. 418.27) | US1-1, SC-001 |
| 3 | Rechazo y bloqueo | Volver a subir el ejemplo (periodos Completado) y una carga con un periodo marcado Admitido por SQL en otra carga | Rechazado / Bloqueado con su incidencia; Finalizado; notificación | US1-2, US1-6 |
| 4 | Existente y orden | Archivo con códigos ya cargados y duplicados intercalados entre periodos | Existente para los de otras cargas y para las duplicadas; gana la fila menor; precios sin cambios | US1-3, US1-4 |
| 5 | Ausentes e inválidos | Archivo de US1-5 | cada fila con el resultado de la tabla de reglas; sin redondeos ni truncamientos | US1-5 |
| 6 | Archivo inválido | Subir el corrupto y el que no tiene `Precio` | `ArchivoInvalido`, 0 periodos, 0 productos, historial hasta Finalizado, notificación, sin reintentos en el log | US4-7, SC-009 |
| 7 | Solo encabezados | Subir el archivo vacío de datos | Finalizado sin periodos ni productos; notificación | Edge Cases |
| 8 | Archivo inexistente | Borrar del filer el archivo de una carga Pendiente antes de que se consuma (consumidor detenido) y arrancarlo | Fallido con `ErrorTecnico`, sin reintentos de descarga en el log, sin notificación | US4-6, SC-009 |
| 9 | SeaweedFS caído | Con el consumidor detenido, subir un archivo (queda Pendiente en cola); detener `seaweedfs` y arrancar el consumidor | 3 reintentos de descarga en el log y luego Fallido con `ErrorTecnico`, sin notificación | US4-1 |
| 10 | Reentrega y duplicado | Publicar a mano (gestión 15672) el mensaje de una carga Finalizado y dos veces el de una Pendiente | la Finalizado no cambia; la Pendiente se procesa una vez | US2-2, US2-3 |
| 11 | Mensaje inválido | Publicar `{}` y un `idCarga` inexistente | se descartan con log de error | US2-4 |
| 12 | Dos instancias | `docker compose run` de una segunda instancia de ms-carga-masiva | la segunda registra que no obtuvo el cerrojo y no consume | US2-1, SC-004 |
| 13 | Caída a mitad | Subir el archivo grande, esperar a que haya ≥ 1 periodo Completado y `docker kill` del consumidor; arrancarlo | conserva los Completado, termina Finalizado, una notificación, sin duplicados; `IntentosProceso` = 2 | US3-1, US3-3, SC-005 |
| 14 | Recuperación de Cargado | Dejar por SQL una carga de prueba en Cargado y reiniciar | Finalizado y una notificación, sin tocar productos | US3-2 |
| 15 | Intentos agotados | Dejar una carga En proceso con `IntentosProceso` = 3 y reiniciar | Fallido con `ErrorTecnico`, Admitido → NoProcesado, Completado conservados | US3-4 |
| 16 | Pendiente intacta | Carga Pendiente sin mensaje en cola; reiniciar | sigue Pendiente | US3-5 |
| 17 | Fallo terminal en un periodo | Durante el archivo grande, provocar un error técnico persistente en el segundo grupo (p. ej. trigger temporal que falla en `Producto` para un código concreto) | P1 Completado, P2 Fallido, resto NoProcesado, `ErrorTecnico`, Fallido, sin notificación | US4-2, SC-006 |
| 18 | Colisión | Archivo con "ss" (fila 2) y "ß" (fila 3) en el mismo periodo | se completa: "ss" producto, "ß" Existente (modo fila a fila en el log) | US4-4 |
| 19 | Transitorio | Desconectar `seaweedfs` de la red 2 s durante una descarga | mismo resultado que sin fallo | US4-3 |
| 20 | Notificación sin confirmación | Detener `rabbitmq` justo tras Cargado (archivo grande) o con la publicación bloqueada | Finalizado, `ErrorTecnico` de falta de confirmación; tras reiniciar no se republica | US4-8, SC-009 |
| 21 | Carrera 005-1 | Con el archivo grande En proceso, ejecutar la llamada de Control: `usp_CambiarEstadoCarga(id, 'Fallido', @EstadoEsperado='Pendiente')` | `Cambiado = 0`; la carga termina Finalizado sin historial Fallido ni `ErrorTecnico` | US5-1 |
| 22 | Carrera 005-2 | Con el consumidor detenido, una subida con RabbitMQ caído deja la carga Fallido en Control; publicar después su mensaje a mano y arrancar el consumidor | se descarta; sigue Fallido sin cambios | US5-2 |
| 23 | Carrera 005-3 | Con el consumidor detenido, crear 20 cargas Pendiente y sus mensajes; arrancar el consumidor y lanzar en paralelo la llamada de Control sobre las 20 | cada carga termina o Fallido sin procesar o Finalizado sin Fallido de Control; ninguna en ambos | US5-3, SC-007 |
| 24 | Logs | `grep -F` de secretos y del contenido de celdas de prueba en los logs | 0 coincidencias | SC-008 |

## Cierre

- Eliminar triggers temporales, purgar `carga_masiva` y `notificaciones`, borrar `/cargas/` en SeaweedFS y la base `CargaMasivaApp_F006`.
- `docker compose up -d --build` sin `DB_NAME`: el runner aplica V004 a `CargaMasivaApp` (`SchemaMigraciones` = 4) y todo queda `healthy`.
- Huella de `CargaMasiva`, `CargaMasivaDb` y volúmenes igual a la inicial.
- Registrar en "Resultados" lo ejecutado y lo no ejecutado.

## Resultados (implementación, 2026-11-03; este equipo: Apple silicon, Docker Desktop, SQL Server 2022 amd64 emulado)

### Línea base (T001)

- `git status` limpio (HEAD `bc92b91`); `dotnet build ms-carga-masiva/src/Acity.CargaMasiva.slnx`: 0 advertencias, 0 errores; los siete contenedores del proyecto en ejecución (`seaweedfs` sin healthcheck; el resto `healthy`).

| Base | Objetos de usuario | Última modificación (UTC) | Registro |
|---|---|---|---|
| `CargaMasiva` | 35 | 2026-11-02 13:22:39.517 | `SchemaMigraciones` = 2 |
| `CargaMasivaDb` | 32 | 2026-11-02 14:48:54.303 | `__EFMigrationsHistory` = 2 |
| `CargaMasivaApp` | 35 | 2026-11-03 00:51:13.063 | `SchemaMigraciones` = 3; 0 cargas |

- Volúmenes: `carga-masiva_sqlserver-data`, `carga-masiva-sqlserver-data` (ajeno), `carga-masiva_logs-{autenticacion,carga-masiva,control,notificaciones}`, `carga-masiva_rabbitmq-data`, `carga-masiva_seaweedfs-data`.

### Setup (T002 a T007)

- **Paquetes**: ClosedXML 0.105.1, RabbitMQ.Client 7.2.2, Polly.Core 8.9.0 y `FrameworkReference Microsoft.AspNetCore.App` en Infrastructure; Logging.Abstractions 10.0.14 en Application; compilación con 0 advertencias. **Abierto 1 resuelto**: `SixLabors.Fonts` resuelto a **1.0.0**, licencia **Apache-2.0** (nuspec del paquete); DocumentFormat.OpenXml 3.1.1, ClosedXML.Parser 2.0.0.
- **Compose**: `ms-carga-masiva` con `depends_on` de `rabbitmq` (healthy) y `seaweedfs` (started) y variables `RabbitMq__*` y `SeaweedFs__FilerUrl`; `docker compose config -q` correcto. `appsettings.json`: `Procesamiento:MaxIntentos` = 3, `RabbitMq:Puerto` = 5672.
- **Escenario 1 (V004)**: **Ejecutado**. `CargaMasivaApp_F006` creada por el runner con V001–V004 (`SchemaMigraciones` = 4). Los ocho tipos (los siete anteriores y `ArchivoInvalido`) se aceptan; `Otro` → error 547 de `CK_Incidencia_Tipo`. Filas de prueba eliminadas.

### Fase 2 y US1 (T008 a T028)

- **T008 (ejecutado por el usuario)**: scaffold de `dbo.Usuario` fuera del repositorio; aviso esperado `Skipping foreign key 'FK_Usuario_Rol'`. Integrados `Id` y `Email` y la relación `CargaArchivo.UsuarioId → Usuario`.
- **Desviaciones de implementación**: clasificadas en `research.md` (tabla D1–D5). Decisiones del usuario (2026-11-03): D1 aceptada como ajuste técnico mínimo; D2 aprobada (H2); D3 y D4 mantienen las reglas existentes; D5 → H3, circuit breaker propio para la transacción de un periodo, **implementación y verificación pendientes** (T049, T050).
- **Entorno (T020)**: usuarios de prueba Id 2 (Administrador) e Id 3 (Operador) en `CargaMasivaApp_F006`; `ms-control` y `ms-carga-masiva` healthy; el consumidor registró "Cerrojo del consumidor obtenido", "Recuperación terminada: 0 carga(s)" y "Consumiendo la cola carga_masiva". Archivos de prueba generados con ClosedXML fuera del repositorio (`…/scratchpad/f006/arch/`).
- **Escenario 2 (flujo completo)**: **Ejecutado** con el Excel de ejemplo (carga 2): historial Pendiente, En proceso, Cargado, Finalizado; 2025-01, 2025-02 y 2025-03 Completado; 116 productos (= códigos distintos) y 84 `Existente` (= filas duplicadas; 116 + 84 = 200), cada `Existente` en la fila esperada; los 116 productos coinciden con la primera aparición en el Excel en precio, periodo y nombre; `IntentosProceso` = 1; `FechaFin` con valor; un mensaje en `notificaciones`: `{"idCarga":2,"usuario":"operador.prueba@local","fechaFin":"2026-11-03T04:25:46.8857742Z"}`.
- **Abierto 4 (precios)**: P0117 → 418.27. El Excel guarda P0047 como `75.51000000000001` (mismo `double` que 75.51, bits `4052E0A3D70A3D71`); con la representación más corta se almacenó 75.51 sin omitir la fila. **Conforme a la decisión del usuario H1** (2026-11-03; constitución §7 v3.2.1), tomada después de esta ejecución con la evidencia de la revisión de cierre.
- **Escenario 3**: **Ejecutado**. Re-subida del ejemplo (carga 3): tres periodos Rechazado con "Periodo AAAA-MM ya completado por otra carga.", 0 productos, Finalizado y notificación. Bloqueo (carga 5, con 2035-01 Admitido por SQL en una carga ficticia 4 En proceso): 2035-01 Bloqueado ("en proceso por otra carga"), 2035-02 Completado, Finalizado y notificación.
- **Escenario 4**: **Ejecutado** (carga 6, 2032-01/2032-02 intercalados): D001 gana en la fila 2 (2032-02) y su duplicada de la fila 5 (2032-01) queda `Existente`; D002 gana en la fila 3 y la fila 4 queda `Existente`; P0117 (de la carga 2) queda `Existente` "ya registrado por otra carga".
- **Escenario 5**: **Ejecutado** (carga 7): fila 3 vacía sin incidencia; Periodo ausente (fila 4) y "2031-13" (fila 9, `PeriodoTexto` = "2031-13") como `FilaOmitida` sin periodo; código ausente (filas 5 y 18, solo blancos); precio "abc" y "1,5" no numéricos; 1.005 no representable; código de 101 caracteres (truncado a 100 en la auditoría con "(valor truncado)"); nombre de 201; " r001 " → `Existente` de R001; productos: R001 10.50, R006 "Producto R006", R007 precio 0.00, "ab c" (espacio interno conservado), R014 12.34 (precio en texto), R017 -5.25.
- **Escenario 7**: **Ejecutado** (carga 8, solo encabezados): Finalizado sin periodos, productos ni incidencias; notificación publicada.

### US2 (T029 a T031)

- **Escenarios 10 y 11**: **Ejecutados** con el consumidor detenido y cinco mensajes publicados a mano tras la subida de la carga 9 (cuyo mensaje publicó Control): el de la carga 9 (duplicado), el de la carga 2 (Finalizado), `{}`, `{"idCarga":999999}` y un texto no JSON. Al arrancar: la carga 9 se procesó una vez (intento 1) y el duplicado se descartó ("la carga 9 está Finalizado"); la carga 2 quedó sin cambios ("está Finalizado"); `{}` y el texto no JSON → "no cumple el contrato de carga_masiva" (error); 999999 → "la carga 999999 no existe" (error); +1 notificación (solo la de la carga 9).
- **Escenario 8 por recuperación (adelantado)**: la carga ficticia 4 (En proceso por SQL, ruta inexistente) se recuperó al arrancar: `ErrorTecnico` "No se pudo recuperar el archivo: no existe en el almacenamiento.", Fallido, sin reintentos de descarga en el log, su periodo 2035-01 `NoProcesado` con reserva liberada (la carga 9 pudo admitir 2035-01). **Abierto 3 resuelto**: el filer responde 404 a un archivo inexistente.
- **Escenario 12**: **Ejecutado**. Segunda instancia con `docker compose run`: "Otra instancia tiene el cerrojo del consumidor: no se consume ni se recupera" dos veces en 25 s (reintento cada 15 s); no consumió ni recuperó. **Abierto 2 (parcial)**: `sp_getapplock` de sesión con `@LockTimeout = 0` devuelve ocupado a la segunda instancia; la detección de una conexión del cerrojo caída se observa en US3.

### US3 (T032 a T035) y pérdida del cerrojo

- **Escenario 13 (caída a mitad)**: **Ejecutado**. Archivo de 50 000 filas en 5 periodos (carga 10): `docker kill` con 2040-01 Completado (10 000 productos) y el resto Admitido; al rearrancar se recuperó: los 5 periodos Completado, 50 000 productos con 50 000 códigos distintos (sin duplicados), `IntentosProceso` = 2, historial Pendiente, En proceso, Cargado, Finalizado (sin Fallido) y exactamente 1 notificación.
- **Escenario 14 (Cargado)**: **Ejecutado** (carga 11, dejada en Cargado por SQL): Finalizado y 1 notificación, sin tocar productos ni periodos.
- **Escenario 15 (intentos agotados)**: **Ejecutado** (carga 12, En proceso con `IntentosProceso` = 3, 2050-01 Completado y 2050-02 Admitido): `IntentosProceso` = 4, `ErrorTecnico` "Intentos de procesamiento agotados (3 de 3).", Fallido; 2050-01 conservado Completado; 2050-02 `NoProcesado` con "No procesado por interrupción de la carga" y reserva liberada; sin notificación.
- **Escenario 16 (Pendiente)**: **Ejecutado** (carga 13, Pendiente sin mensaje): no se tocó.
- **Pérdida del cerrojo (US2-5; abierto 2 resuelto)**: con `KILL` de la sesión que sostenía el cerrojo, en la siguiente carga (15, 50 000 filas) `ComprobarVigenteAsync` lanzó `CerrojoPerdidoException` antes del primer periodo; el mensaje no se confirmó; a los 10 s el consumidor obtuvo de nuevo el cerrojo, recuperó la carga 15 (intento 2) y la terminó (5 periodos Completado, 50 000 productos, Finalizado); el mensaje reentregado se descartó. (Un primer intento con el archivo de la carga 10 no sirvió: todos sus periodos ya estaban Completado y quedaron Rechazado, sin periodos que persistir.)

### US4 (T036 a T040)

- **Defecto encontrado y corregido (consumidor)**: si el canal de RabbitMQ se cerraba durante un mensaje, `ConsumirAsync` volvía en el acto aunque el procesamiento siguiera; el bucle liberaba el cerrojo y la recuperación posterior podía solaparse con esa ejecución. Corrección mínima en `ConsumidorRabbitMq`: un `SemaphoreSlim` del mensaje en curso; al terminar (fallo del canal o parada) se espera a que acabe antes de volver. Comprobado en el escenario 20 ("El consumidor se detuvo" aparece después de terminar la carga en curso).
- **Escenario 6 (archivo inválido)**: **Ejecutado**. Bytes aleatorios con `.xlsx` (carga 16) → `ArchivoInvalido` "No es un Excel legible."; sin la columna `Precio` (carga 17) → `ArchivoInvalido` "Faltan encabezados: Precio."; ambas: 0 periodos, 0 productos, historial Pendiente, En proceso, Cargado, Finalizado, notificación publicada y sin reintentos en el log.
- **Escenario 8 (archivo inexistente)**: **Ejecutado** (carga 19): consumidor detenido, archivo borrado del filer (204), al arrancar → `ErrorTecnico` "No se pudo recuperar el archivo: no existe en el almacenamiento.", Fallido, 0 reintentos de descarga, sin notificación.
- **Escenario 9 (SeaweedFS caído)**: **Ejecutado** (carga 20): 3 reintentos de descarga (`HttpRequestException`) y Fallido con `ErrorTecnico` "Fallo técnico durante recuperación del archivo: HttpRequestException.", sin notificación.
- **Escenario 17 (fallo terminal en un periodo)**: **Ejecutado** (carga 23) con una restricción CHECK temporal en `Producto` de la base temporal que rechaza el código KFAIL de 2042-02 (eliminada después; 0 restantes): 2042-01 Completado (K1); 2042-02 Fallido sin productos ni incidencias de negocio; `ErrorTecnico` en 2042-02 "Fallo técnico durante persistencia del periodo 2042-02: DbUpdateException."; 2042-03 `NoProcesado` "No procesado por interrupción de la carga"; la fila 4 (K2 en 2042-01, duplicada de la ganadora fila 3 en 2042-02) `NoProcesado` "la fila ganadora del código no se persistió (fila 3)" y no `Existente`; 0 reservas activas; Fallido; 0 notificaciones; sin reintentos (error no transitorio).
- **Escenario 18 (colisión)**: **Ejecutado** (carga 18): "ss" (fila 2) y "ß" (fila 3) en 2034-01; la collation los iguala; log "Colisión de código de producto … se repite producto a producto"; resultado: producto "ss" y `Existente` "ß" "detectado al confirmar"; periodo Completado; Finalizado. **Abierto 5 resuelto**: EF Core usó puntos de guardado (el producto "ss" se conservó) y el mensaje del error 2601 nombra `UX_Producto_CodigoProducto`.
- **Escenario 19 (transitorio)**: **Ejecutado parcialmente**. Con el filer pausado ~3 s mientras se descargaba (carga 22), la descarga esperó y terminó: Finalizado, sin cambio de estado, sin reintentos ni Fallido. No se observó "reintento seguido de éxito": la pausa no produce errores y desconectar la red cambió la IP de `seaweedfs` (el `weed server` siguió ligado a la anterior y rechazó conexiones; carga 21 → 3 reintentos y Fallido, comportamiento correcto ante un fallo persistente; el contenedor se reinició y conservó sus archivos).
- **Escenario 20 (notificación sin confirmación)**: **Ejecutado** (carga 24, 50 000 filas): `rabbitmq` detenido con 2043-01 Completado; la carga llegó a Finalizado; la publicación falló (tiempo de espera y `BrokerUnreachableException`, 2 reintentos) → `ErrorTecnico` "No se obtuvo confirmación del intermediario para el mensaje de notificación; no se garantiza que se haya publicado ni que se notifique después."; al volver RabbitMQ el mensaje de `carga_masiva` se reentregó y se descartó ("está Finalizado"); tras reiniciar el servicio la recuperación no la tocó y no hay ningún mensaje de la carga 24 en `notificaciones`.

### US5 (T041 a T043): carrera documentada en la feature 005

- **Escenario 21**: **Ejecutado** (carga 25, 50 000 filas): con la carga En proceso, la llamada de Control `usp_CambiarEstadoCarga(25, 'Fallido', @EstadoEsperado = 'Pendiente')` devolvió `Cambiado = 0`; la carga terminó Finalizado con historial Pendiente, En proceso, Cargado, Finalizado, sin Fallido ni `ErrorTecnico`.
- **Escenario 22**: **Ejecutado** (carga 26): con `ms-carga-masiva` y `rabbitmq` detenidos, Control respondió 500 y dejó la carga Fallido (historial Pendiente, Fallido; `ErrorTecnico`); con RabbitMQ de nuevo arriba se publicó a mano su mensaje y se arrancó el consumidor → "Mensaje descartado: la carga 26 está Fallido"; la carga quedó sin cambios (mismo resumen antes y después).
- **Escenario 23**: **Ejecutado** en cuatro rondas de 20 cargas: (a) con el consumidor arrancando, las 20 llamadas de Control ganaron (20 Fallido sin periodos ni incidencias; mensajes descartados); (b) y (c) con el consumidor en marcha, el consumidor ganó las 20 (20 Finalizado; Control `Cambiado = 0`); (d) con los mensajes en cola, el consumidor procesando en orden ascendente y Control llamando en orden descendente: **mezcla real**, 4 Fallido por Control (sin periodos ni incidencias; sus mensajes descartados "está Fallido") y 16 Finalizado por el consumidor (Control `Cambiado = 0`). En las 80 cargas: 0 con Fallido y En proceso a la vez en el historial, 0 Fallido con periodos o incidencias, 0 Finalizado con Fallido en el historial.

### Cierre (T044 a T048)

- **Escenario 24 (logs)**: **Ejecutado**. En `docker logs ms-carga-masiva` y en el volumen de logs: 0 coincidencias de `RABBITMQ_PASSWORD`, `MSSQL_SA_PASSWORD`, `JWT_CLAVE`, `Password=`, `User Id=` y de valores de celdas de prueba ("Nombre R001", "Producto P0117", "Espacio interno", "418.27", "KFAIL").
- **Revisión del diff (T045)**: cambios solo en `ms-carga-masiva/`, `database/scripts/V004__incidencia_archivo_invalido.sql`, `compose.yaml` y documentos de `specs/006-…` (más la nota en `specs/005-…/quickstart.md`); V001–V003, ms-control, ms-autenticacion, ms-notificaciones, `api-gateway/`, `referencias/` y la constitución sin cambios; único controlador `HealthController`; sin proyectos Worker ni de pruebas.
- **Limpieza y arranque normal (T046)**: 0 restricciones de prueba restantes; colas `carga_masiva` y `notificaciones` purgadas; `/cargas/` borrado en SeaweedFS; `CargaMasivaApp_F006` eliminada (ninguna `CargaMasivaApp_*`). `docker compose up -d --build`: el runner aplicó `V004__incidencia_archivo_invalido.sql` a `CargaMasivaApp` (`SchemaMigraciones` = 4; 35 objetos; 0 cargas); todos los servicios `healthy`; el consumidor obtuvo el cerrojo, recuperó 0 cargas y consume `carga_masiva`. `CargaMasiva` (35, 2026-11-02 13:22:39.517, 2) y `CargaMasivaDb` (32, 2026-11-02 14:48:54.303, 2) idénticas a T001; volúmenes sin cambios.
- **Scripts y archivos de verificación (fuera del repositorio)**: `/private/tmp/claude-501/-Users-wils0ndev-examenes-tecnicos-acity-carga-masiva/bb0c3a1b-8027-4598-aea8-a9d3ab12cea6/scratchpad/f006/` (`env.sh`, `gen-xlsx.cs` y variantes, `arch/`); el scratchpad es de la sesión.

**No ejecutado o parcial**: escenario 19 sin "reintento seguido de éxito" (ver arriba); el 202 de Control ante `Cambiado = 0` tras una publicación sin confirmación (005); errores SQL transitorios no provocados. **Validación en el equipo de evaluación**: no realizada.

**Limitaciones vigentes**: SQL Server amd64 emulado en Apple silicon (sin soporte oficial; resultados solo de este equipo). Una carga Finalizado cuya notificación no se confirmó no se notifica después (limitación aceptada, constitución §8). Si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`.

## Clasificación de las verificaciones (revisión de cierre, 2026-11-03)

Ninguna casilla `[x]` de `tasks.md` se usa como evidencia; solo cuenta lo registrado arriba.

| Estado | Escenarios / verificaciones |
|---|---|
| **Completas** | 1 (V004); 2 (flujo completo y comparación fila a fila con el Excel); 3 (rechazo y bloqueo); 4 (orden original y duplicados intercalados); 5 (ausentes, inválidos, normalización y longitudes); 6 (archivo corrupto y sin `Precio`); 7 (solo encabezados); 8 (archivo inexistente, por mensaje y por recuperación); 9 (SeaweedFS caído: 3 reintentos y Fallido); 10 y 11 (reentrega, duplicado, mensajes inválidos); 12 (segunda instancia); 13 (caída a mitad); 14 (Cargado); 15 (intentos agotados); 16 (Pendiente intacta); pérdida del cerrojo (US2-5); 17 (fallo terminal y "ganador no persistido"); 18 (colisión "ss"/"ß"); 20 (notificación sin confirmación, sin republicación); 21, 22 y 23 (carrera de la 005, con mezcla real en la cuarta ronda); 24 (logs) |
| **Completas, conformes a decisiones posteriores** | 2 y 5 en lo relativo a precios: P0047 → 75.51 y "1,5" → no numérico se ejecutaron con el comportamiento que el usuario aprobó después (H1, H2); no se repitieron |
| **Parciales** | 19 (transitorio): se comprobó que una espera de ~3 s no cambia el estado, pero no "reintento seguido de éxito"; FR-016 en la transacción de un periodo: reintentos implementados pero no provocados (sin errores transitorios), circuit breaker **decidido (H3) pero no implementado** (T049) |
| **No ejecutadas** | T050 (circuit breaker de la transacción de un periodo, tras T049); errores SQL transitorios (1205, -2, …) y su reintento; duplicado de una notificación por reintento tras confirmación perdida; apertura del circuit breaker; validación en el equipo de evaluación; de la 005, el 202 de Control ante `Cambiado = 0` tras una publicación sin confirmación y el `nack`/`return` del broker |
