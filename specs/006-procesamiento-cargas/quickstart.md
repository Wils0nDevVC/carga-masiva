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
