# Quickstart: validación manual de la recepción de cargas (ms-control)

Guía de verificación manual (sin pruebas automatizadas, constitución §11). Detalles de contrato en `contracts/`; columnas y estados en `data-model.md`. Ningún escenario se ha ejecutado todavía.

## Reglas de la verificación

- Las filas de prueba (usuarios de prueba, cargas) van a una **base temporal** `CargaMasivaApp_F005` creada por el runner; `CargaMasivaApp` solo recibe V003 con el arranque normal. `CargaMasiva`, `CargaMasivaDb` y los volúmenes ajenos no se tocan; nunca `docker compose down -v`.
- Secretos solo por entorno (`.env` no versionado o variables del shell): `MSSQL_SA_PASSWORD`, `RABBITMQ_USER`, `RABBITMQ_PASSWORD`, `JWT_CLAVE`. No imprimirlos.
- Los tokens de prueba se generan con un script `.cs` **fuera del repositorio** (`dotnet run gen-token.cs`), firmado con `JWT_CLAVE`, `JWT_EMISOR` y `JWT_AUDIENCIA` y con los claims del contrato (`sub`, `role`, `exp`). No es código de producción ni un modo que omita la validación.
- Validado solo en este equipo (SQL Server amd64 emulado); el equipo de evaluación queda sin validar.

## Preparación

1. `.env` con los cuatro secretos y `JWT_EMISOR=ms-autenticacion`, `JWT_AUDIENCIA=carga-masiva`.
2. `docker compose build database-runner ms-control` (la imagen del runner incluye V003).
3. Base temporal: `docker compose run --rm --no-deps -e DB_NAME=CargaMasivaApp_F005 database-runner` → aplica V001, V002 y V003.
4. Usuarios de prueba en la base temporal (por `sqlcmd` en el contenedor `sqlserver`): `admin.prueba@local` (RolId 1) y `operador.prueba@local` (RolId 2) con `PasswordHash` = `'sin-credencial-prueba'` (no es una contraseña; no permiten iniciar sesión). Anotar sus `Id`.
5. Levantar Control sobre la base temporal: `DB_NAME=CargaMasivaApp_F005 docker compose up -d --build ms-control` (arranca también `sqlserver`, el runner, `rabbitmq` y `seaweedfs`).
6. Tokens: Administrador, Operador, rol `Invitado`, sin `role`, `sub` = `abc`, `sub` inexistente (p. ej. 999999), caducado, firmado con otra clave, otro emisor y otra audiencia.
7. Archivos: el Excel de ejemplo (`referencias/carga_masiva_productos (1).xlsx`), `.xlsx` de 0 bytes, de 10 485 760, de 10 485 761 y de 12 000 000 bytes, `.xls`, `.csv`, sin extensión, `.XLSX` y un nombre de 261 caracteres.

## Escenarios

| # | Escenario | Pasos | Resultado esperado | Spec |
|---|---|---|---|---|
| 1 | Arranque y configuración | `docker compose ps`; `GET /health` en 8082; arrancar Control con `CargaArchivo__TamanoMaximoBytes=0` y sin `Jwt__Clave` | healthy y 200; con configuración inválida Control no arranca y el log indica la opción sin mostrar valores | Edge Cases |
| 2 | V003 del procedimiento | En la base temporal: carga de prueba Pendiente y otra En proceso (insertadas por SQL); llamar sin `@EstadoEsperado` para cada transición de la matriz; con `@EstadoEsperado='Pendiente'` sobre la En proceso y sobre la Pendiente | sin parámetro = mismo resultado que V002; En proceso → `@Cambiado=0`, sin cambios en carga, historial ni periodos; Pendiente → Fallido con historial | SC-008, FR-032 |
| 3 | Envío válido por rol | `POST /cargas` con el Excel de ejemplo y token de Operador; repetir con Administrador | 202, `data.idCarga`, `data.estado="Pendiente"`, `code` 202; cinco campos camelCase | US1-1/2, SC-001 |
| 4 | Persistencia | Consultar `CargaArchivo` e historial del `idCarga` | nombre original, `UsuarioId` = `sub`, `FechaRecepcion`, `RutaArchivo` `seaweed://cargas/…`, Pendiente, una entrada de historial Pendiente; sin binario | US1-3 |
| 5 | Archivo almacenado | `GET http://localhost:8888/cargas/…` (ruta tras `seaweed://`) y comparar SHA-256 con el enviado | idéntico | US1-4 |
| 6 | Mensaje | En la gestión de RabbitMQ (15672), `Get messages` en `carga_masiva` (sin consumidor) | un mensaje persistente por carga con `idCarga`, `rutaArchivo` = la registrada y `usuario` = correo de la base | US1-5 |
| 7 | Mismo archivo dos veces | Enviar dos veces | dos cargas, dos archivos, dos mensajes | US1-6 |
| 8 | Identidad | Sin token; firma inválida; caducado; otro emisor; otra audiencia; `sub=abc`; `sub` inexistente | 401 con `Response<T>`; sin carga, archivo ni mensaje nuevos | US2-1/2/4, SC-002, SC-004 |
| 9 | Permiso | Token `Invitado` y token sin `role` | 403 sin efectos | US2-3 |
| 10 | Validación | Sin archivo; dos archivos; JSON en lugar de multipart; 0 bytes; `.xls`; `.csv`; sin extensión; 261 caracteres | 400 con `errors`; sin efectos | US3-1 a US3-6 |
| 11 | `.XLSX` | Enviar `.XLSX` | 202 | US3-5 |
| 12 | Límite de tamaño | 10 485 760 → 202; 10 485 761 → 400; 12 000 000 → 413; con `CargaArchivo__TamanoMaximoBytes` = 2 000 000: 1 999 999 y 2 000 000 aceptados, 2 000 001 rechazado | según esperado, todos con `Response<T>` | US1-7, US3-7/8, SC-006 |
| 13 | Rate Limiting | 11 envíos válidos en < 60 s con el mismo token; `GET /health` durante el bloqueo | el 11.º → 429 sin efectos; `/health` 200 | US4, FR-027 |
| 14 | SeaweedFS caído | `docker compose stop seaweedfs`; enviar; arrancar de nuevo | 500 tras los reintentos; sin carga ni mensaje | US5-1 |
| 15 | Base caída tras subir | Enviar un archivo grande y detener `sqlserver` durante la subida (o simular el fallo del registro) | 500; sin mensaje; log de error con la ruta del archivo sin carga | US5-2 |
| 16 | RabbitMQ caído | `docker compose stop rabbitmq`; enviar; arrancar | 500; la carga queda Fallido con historial Pendiente → Fallido e incidencia `ErrorTecnico` | US5-3, SC-005 |
| 17 | Transitorio | Detener `seaweedfs` y arrancarlo dentro de la ventana de reintentos | 202; una carga, un archivo, un mensaje | US5-7 |
| 18 | Carga ya tomada | Cubierto en el escenario 2 a nivel de procedimiento; revisar en el código que `Cambiado = 0` conduce a 202 sin incidencia | sin Fallido ni incidencia sobre una carga En proceso | US5-5, FR-023 |
| 19 | Logs | `grep -F` del token, `JWT_CLAVE`, `RABBITMQ_PASSWORD`, `MSSQL_SA_PASSWORD` y `Password=` en `docker logs ms-control` y en el volumen de logs | 0 coincidencias; constan `IdCarga`, `UsuarioId`, `RutaArchivo` | SC-007 |
| 20 | Sin efectos colaterales | Huella de `CargaMasiva`, `CargaMasivaDb` y volúmenes ajenos antes y después | idéntica | Reglas |

## Cierre

- Eliminar `CargaMasivaApp_F005` y, si se desea, los mensajes de prueba de la cola (purga de `carga_masiva`) y los archivos de prueba de `/cargas/` en SeaweedFS.
- `docker compose up -d --build` sin `DB_NAME` temporal: el runner aplica V003 a `CargaMasivaApp` (único cambio de esquema; comprobar `SchemaMigraciones` = 3) y los servicios vuelven a `healthy`.
- Registrar en la sección "Resultados" (a crear en la implementación) lo ejecutado y lo no ejecutado.
