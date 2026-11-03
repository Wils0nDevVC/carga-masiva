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

## Resultados (implementación, 2026-11-02; este equipo: Apple silicon, Docker Desktop, SQL Server 2022 amd64 emulado)

### Línea base (T001)

- `git status`: solo `specs/005-recepcion-registro-cargas/tasks.md` sin versionar; `dotnet build ms-control/src/Acity.Control.slnx`: 0 advertencias, 0 errores; los cinco contenedores del proyecto `healthy`.
- Huella de solo lectura:

| Base | Objetos de usuario | Última modificación (UTC) | Registro |
|---|---|---|---|
| `CargaMasiva` | 35 | 2026-11-02 13:22:39.517 | `SchemaMigraciones` = 2 |
| `CargaMasivaDb` | 32 | 2026-11-02 14:48:54.303 | `__EFMigrationsHistory` = 2 |
| `CargaMasivaApp` | 35 | 2026-11-02 18:06:59.020 | `SchemaMigraciones` = 2 |

- Volúmenes: `carga-masiva_sqlserver-data`, `carga-masiva-sqlserver-data` (ajeno), `carga-masiva_logs-{autenticacion,carga-masiva,control,notificaciones}`.

### Infraestructura y V003 (T002 a T009)

- **Imágenes (T002, resuelve el abierto 1)**: `rabbitmq:4.1.8-management` (RabbitMQ 4.1.8) y `chrislusf/seaweedfs:3.97` (`version 30GB 3.97 76452ab59`); ambas con variante arm64 nativa en este equipo.
- **Secretos**: `RABBITMQ_USER`, `RABBITMQ_PASSWORD` y `JWT_CLAVE` (48 caracteres) generados de forma aleatoria en `.env` (no versionado), sin mostrarse. `docker compose config -q` correcto.
- **Paquetes (T006, T007)**: JwtBearer 10.0.14, RabbitMQ.Client 7.2.2 y Polly.Core 8.9.0 restaurados; compilación con 0 advertencias.
- **Escenario 2 (V003, SC-008)**: **Ejecutado** en `CargaMasivaApp_F005` (creada por el runner con V001, V002 y V003; `SchemaMigraciones` = 3). V003 difiere de V002 solo en el parámetro final, la validación 50023 y la comprobación del origen (diff revisado). Resultados: matriz completa de 36 transiciones **sin** `@EstadoEsperado` con 0 discrepancias (las válidas cambian el estado con una fila de historial; las inválidas y las repeticiones devuelven 0 sin escribir); llamada posicional de cuatro argumentos (Pendiente → En proceso) correcta; En proceso con periodo Admitido y `@EstadoEsperado='Pendiente'` → `Cambiado=0`, estado, historial, incidencias y periodo (Admitido con reserva) sin cambios; Pendiente con `@EstadoEsperado='Pendiente'` → Fallido con su historial; repetición sobre Fallido → 0; `@EstadoEsperado='Otro'` → error 50023. Filas de prueba eliminadas.

### Usuario en Control y entorno de prueba (T010 a T018)

- **T010 (ejecutado por el usuario)**: scaffold de `dbo.Usuario` a una carpeta temporal fuera del repositorio; salida = `Usuario.cs` + `ScaffoldContext.cs`; aviso esperado `Skipping foreign key 'FK_Usuario_Rol' … 'dbo.Rol' was not found in the model`. `git status` sin archivos del scaffold. Se integró solo `Id` y `Email` (`HasMaxLength(256)`); `PasswordHash` (VARCHAR(255)) y `RolId` no se mapean; relación `CargaArchivo.UsuarioId → Usuario` sin navegaciones. Compilación con 0 advertencias.
- **Generador de tokens (T017)**: `/private/tmp/claude-501/-Users-wils0ndev-examenes-tecnicos-acity-carga-masiva/bb0c3a1b-8027-4598-aea8-a9d3ab12cea6/scratchpad/f005/gen-token.cs` (`#:package Microsoft.IdentityModel.JsonWebTokens@8.19.2`); uso con las variables de `.env` cargadas en el shell: `dotnet run gen-token.cs -- --sub 3 --role Operador`.
- **Datos de prueba (T018)**: en `CargaMasivaApp_F005`, `admin.prueba@local` = Id 2 (RolId 1) y `operador.prueba@local` = Id 3 (RolId 2), con `PasswordHash` = `'sin-credencial-prueba'`. `DB_NAME=CargaMasivaApp_F005 docker compose up -d --build ms-control`: `rabbitmq` healthy, `seaweedfs` en ejecución, runner "Sin scripts pendientes", `ms-control` healthy y `GET /health` = 200.
- **Filer de SeaweedFS (abierto 3)**: tarda unos 20 s en responder tras arrancar; `PUT` de un cuerpo binario → **201** con `{"name","size"}`; un segundo `PUT` a la misma ruta sobrescribe (201) y el `GET` devuelve el nuevo contenido; `DELETE` → 204. Prueba eliminada.

### US1 y US2 (T019 a T031)

- **Desviaciones mínimas de compilación (documentadas)**: `Acity.Application` necesita `ILogger<T>` para los logs del handler exigidos por FR-029 → se añadió `Microsoft.Extensions.Logging.Abstractions` 10.0.14 (MIT; abstracción ya presente en el runtime de ASP.NET Core). `Acity.Infrastructure` necesita el enlace de opciones y `IHttpClientFactory` → `FrameworkReference Microsoft.AspNetCore.App` (framework compartido del host, no es un paquete NuGet).
- **Escenarios 3 a 7**: **Ejecutados**. Operador (Id 3) → 202 `{"data":{"idCarga":39,"estado":"Pendiente"},"isSuccess":true,"message":"Carga registrada","code":202,"errors":null}`; Administrador (Id 2) → 202 con `idCarga` 40 (cinco campos en camelCase). En la base: `NombreArchivo` = `ejemplo.xlsx`, `UsuarioId` = `sub`, `FechaRecepcion` UTC, `RutaArchivo` = `seaweed://cargas/2026/11/<guid>.xlsx`, `Estado` Pendiente, `FechaFin` NULL, `IntentosProceso` 0 e historial = una entrada Pendiente. Archivos descargados del filer con SHA-256 idéntico al enviado. Cola `carga_masiva`: dos mensajes del intercambio `carga_masiva`, `delivery_mode` 2 (persistente), `application/json`, cuerpos `{"idCarga":39,"rutaArchivo":"seaweed://…","usuario":"operador.prueba@local"}` y el equivalente de la carga 40 con `admin.prueba@local`. Mismo archivo dos veces = dos cargas, dos archivos y dos mensajes.
- **Confirmación de publicación (abierto 2, parcial)**: con `publisherConfirmationTrackingEnabled`, `BasicPublishAsync` completó solo tras la confirmación (mensajes presentes en la cola al responder 202). El caso de `nack`/`return` se observa en US5.
- **Escenarios 8 y 9**: **Ejecutados**. Sin token, firma con otra clave, caducado, otro emisor, otra audiencia y `sub=abc` → 401 "No autenticado"; `sub=999999` → 401 "Usuario no reconocido"; `role=Invitado` y sin `role` → 403 "Sin permiso". Todos con `Response<T>` (`data` null, `code` = HTTP). Antes y después: 2 cargas, 2 archivos, 2 mensajes (0 efectos). 0 coincidencias de los nueve tokens en `docker logs`.
- **Nota del entorno**: cargar todo `.env` en el shell sustituye `MSSQL_SA_PASSWORD` por el valor de `.env`, que no es el del volumen (falla el login de `sa`); los scripts de verificación cargan solo las variables de la Feature 005.

### US3 (T032 a T035)

- **Defecto encontrado y corregido**: con `[ApiController]`, el enlace de modelos leía el formulario antes de la acción; un cuerpo de 12 MB devolvía un `ProblemDetails` 400 del framework ("Failed to read the request form. Request body too large. The max request body size is 11534336 bytes."), un segundo formato de error. Solución mínima (patrón documentado de ASP.NET Core para subidas): filtro de recurso `SinEnlaceDeFormularioAttribute` en la acción, que quita los proveedores de valores de formulario; el formulario lo lee solo el controlador. El validador recibe `CargaArchivoOptions` como singleton (sin depender de `IOptions` en Application).
- **Límites (abierto 4)**: `MaxRequestBodySize` y `MultipartBodyLengthLimit` = 11 534 336 (10 MiB + 1 MiB), confirmado por el mensaje del servidor. Por encima, la respuesta es 413 con `Response<T>` sin entrada de error en el log.
- **Escenarios 10 a 12**: **Ejecutados**. Formulario sin archivo, dos archivos y cuerpo JSON → 400 "Debe enviarse exactamente un archivo."; 0 bytes → 400 "El archivo está vacío."; `.xls`, `.csv` y sin extensión → 400 "Solo se admiten archivos .xlsx."; nombre de 261 caracteres → 400 "El nombre del archivo supera 260 caracteres." (sin truncar); todos sin efectos (contadores iguales antes y después). `.XLSX` → 202; nombre de 260 → 202; 10 485 760 bytes → 202; 10 485 761 → 400 "El archivo excede el tamaño máximo de 10485760 bytes."; 12 000 000 y 20 000 000 → 413 "El archivo excede el tamaño permitido"; contadores coherentes (5 cargas, 5 archivos, 5 mensajes). Con `CargaArchivo__TamanoMaximoBytes=2000000` (contenedor aparte): 1 999 999 y 2 000 000 → 202, 2 000 001 → 400.
- **Escenario 1 (configuración inválida)**: **Ejecutado**. `CargaArchivo__TamanoMaximoBytes=0`, `diez` y `Jwt__Clave` vacía → el servicio no arranca (código de salida 1, log `[FTL]` con la opción afectada y sin valores). 0 coincidencias de `JWT_CLAVE` en los logs.
- **Avisos observados**: al registrar la autenticación aparecen avisos de DataProtection (claves en el contenedor); no afectan al JWT (no usa DataProtection) y no se cambia nada.

### US4 (T036, T037)

- **Escenario 13**: **Ejecutado**. Con el token de Administrador, 11 envíos seguidos: del 1.º al 10.º → 202, el 11.º → 429 `{"data":null,"isSuccess":false,"message":"Demasiadas solicitudes","code":429,"errors":null}` sin efectos; `GET /health` = 200 durante el bloqueo; el Operador (otra partición por `sub`) → 202. Contadores: de 7 a 18 cargas, archivos y mensajes (10 + 1 aceptados).

### US5 (T038 a T043)

- **Compilación**: pipelines de Polly.Core como singletons con clave (`AddKeyedSingleton` + `[FromKeyedServices]`), orden reintento → circuit breaker → tiempo de espera por intento; registro del reintento y de la apertura del circuito en el log (solo el tipo de excepción). 0 advertencias.
- **Escenario 14 (SeaweedFS detenido)**: **Ejecutado**. 3 reintentos registrados (`Reintento n de seaweedfs tras HttpRequestException`) y 500 `Response<T>` genérico en 6,4 s; sin carga ni mensaje nuevos.
- **Escenario 15 (fallo del registro)**: **Ejecutado** con un trigger `INSTEAD OF INSERT` temporal en `CargaMasivaApp_F005` (eliminado a continuación; 0 triggers al terminar). 500; 0 cargas y 0 mensajes nuevos; +1 archivo en SeaweedFS sin carga y log de error `No se pudo registrar la carga; el archivo seaweed://cargas/2026/11/7b2f…a673.xlsx quedó almacenado sin carga`. Los logs de EF muestran los parámetros enmascarados (`'?'`).
- **Escenario 16 (RabbitMQ detenido)**: **Ejecutado**. 2 reintentos (`BrokerUnreachableException`) y 500; la carga 57 queda **Fallido** con historial `Pendiente,Fallido` e incidencia `ErrorTecnico: No se pudo confirmar la publicación del mensaje de procesamiento.`; sin mensaje. Tras volver a arrancar RabbitMQ, el publicador recrea la conexión (escenario 17 publicó).
- **Escenario 17 (transitorio)**: **Ejecutado** desconectando `seaweedfs` de la red 2 s: 2 reintentos y 202 (`idCarga` 58) en 2,2 s; exactamente +1 carga, +1 archivo y +1 mensaje.
- **Escenario 18 (carga ya tomada)**: el procedimiento se comprobó en el escenario 2 (`Cambiado=0` sin escribir nada sobre una carga En proceso). En el código, `Cambiado=0` registra la advertencia y responde 202 sin insertar incidencia (`RegistrarFalloDePublicacionAsync` devuelve `false`). No se provocó de punta a punta: requiere que el broker reciba el mensaje sin confirmarlo y que un consumidor (inexistente en esta feature) lo tome.
- **Abierto 5 (clasificación)**: con RabbitMQ detenido la excepción es `BrokerUnreachableException` (reintentada); con SeaweedFS detenido o desconectado, `HttpRequestException` sin código de estado (reintentada). Los números SQL transitorios no se provocaron.

### Cierre (T044 a T048)

- **Escenario 19 (logs)**: **Ejecutado**. En `docker logs ms-control` y en el volumen `logs-control`: 0 coincidencias de `JWT_CLAVE`, `RABBITMQ_PASSWORD`, `MSSQL_SA_PASSWORD`, `Password=`, `User Id=` y de los diez tokens de prueba. Constan 19 mensajes "Carga N aceptada: usuario U, archivo …, en seaweed://…" (`IdCarga`, `UsuarioId`, `NombreArchivo`, `Longitud`, `RutaArchivo`) y el error del archivo sin carga.
- **Revisión del diff (T045)**: cambios solo en `ms-control/`, `database/scripts/V003__estado_origen_esperado.sql`, `compose.yaml`, `.env.example` y `specs/005-…`; V001, V002, ms-autenticacion, ms-carga-masiva, ms-notificaciones, `api-gateway/`, `referencias/` y la constitución sin cambios; controladores: `HealthController` y `CargasController`; 0 consumidores; 0 `DbSet`/`HasIndex`/`HasData`; 0 proyectos de pruebas; `.env`, `bin/`, `obj/` y `logs/` ignorados.
- **Limpieza y arranque normal (T046)**: cola `carga_masiva` purgada; `/cargas/` borrado en SeaweedFS; `CargaMasivaApp_F005` eliminada (no queda ninguna `CargaMasivaApp_*`). `docker compose up -d --build`: el runner aplicó `V003__estado_origen_esperado.sql` a `CargaMasivaApp` (`SchemaMigraciones` = 3; 35 objetos; es el único cambio de esquema; 0 cargas); todos los servicios `healthy`; `/health` de Control = 200.
- **Escenario 20**: **Ejecutado**. `CargaMasiva` (35 objetos, 2026-11-02 13:22:39.517, `SchemaMigraciones` = 2) y `CargaMasivaDb` (32 objetos, 2026-11-02 14:48:54.303, `__EFMigrationsHistory` = 2) idénticas a T001; volúmenes previos conservados y dos nuevos de esta feature (`carga-masiva_rabbitmq-data`, `carga-masiva_seaweedfs-data`).
- **Scripts de verificación conservados fuera del repositorio**: `/private/tmp/claude-501/-Users-wils0ndev-examenes-tecnicos-acity-carga-masiva/bb0c3a1b-8027-4598-aea8-a9d3ab12cea6/scratchpad/f005/` (`gen-token.cs`, `env.sh`, `us2.sh`, `us3.sh`, `us3b.sh`, `us4.sh`, `us5.sh`); el scratchpad es de la sesión y puede no persistir.

**No ejecutado**: escenario 18 de punta a punta (sin consumidor); `nack`/`return` del broker; errores SQL transitorios. **Validación en el equipo de evaluación**: no realizada.

**Limitación vigente**: SQL Server amd64 corre emulado en Apple silicon, sin soporte oficial de Microsoft; resultados obtenidos **solo en este equipo**. Si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`.
