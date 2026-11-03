# Research: Recepción y registro de cargas en ms-control

**Procedencia**: `Dec` = decisión del usuario o de la constitución v3.1.0; `Spec` = requisito de `spec.md`; `F001/F002/F004` = features anteriores; `Plan` = decisión técnica de este plan (alternativa más sencilla compatible); `Verif` = comprobado en esta etapa (NuGet, código fuente o documentación); `Sin verif` = conocido pero no comprobado en esta sesión (se comprueba en la implementación). Las decisiones `Plan` no requieren aprobación individual (indicación del usuario).

## D-01 Paquetes y versiones

| Paquete | Versión | Proyecto | Licencia | Comprobación |
|---|---|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.14 | `Acity.Api` | MIT | Verif (NuGet: estable más reciente, net10.0, publicado 2026-10-25) |
| `RabbitMQ.Client` | 7.2.2 | `Acity.Infrastructure` | Apache-2.0 OR MPL-2.0 | Verif (NuGet: estable más reciente, net8.0/netstandard2.0, publicado 2026-08-05) |
| `Polly.Core` | 8.9.0 | `Acity.Infrastructure` | BSD-3-Clause | Verif (NuGet: estable más reciente, net10.0, publicado 2026-10-28) |

- **Sin paquete** para Rate Limiting (`Microsoft.AspNetCore.RateLimiting` es parte del framework compartido de ASP.NET Core) ni para SeaweedFS (API HTTP del filer con `HttpClient`). [Plan] [Dec §12 "sin frameworks nuevos"]
- **No se usan** `Microsoft.Extensions.Http.Resilience` ni `Polly.Extensions` (envoltorios adicionales); `Polly.Core` basta. Tampoco `EnableRetryOnFailure` de EF Core (no es Polly y no admite transacciones iniciadas por el usuario). [Plan]
- **Imágenes**: RabbitMQ oficial con plugin de gestión (`rabbitmq:<4.x>-management`) y `chrislusf/seaweedfs:<3.x>`. Docker Hub **no fue accesible** desde esta sesión: la etiqueta exacta (nunca `latest`) se fija y comprueba en la implementación. [Sin verif]

## D-02 Validación del JWT en Control (Q1)

- JwtBearer con clave simétrica (HS256). Configuración por entorno: `Jwt__Clave` (obligatoria, ≥ 32 bytes; la biblioteca rechaza claves HS256 más cortas), `Jwt__Emisor`, `Jwt__Audiencia`. Se valida firma, emisor, audiencia y vigencia; `ClockSkew` = 0 (un token caducado se rechaza en el acto, verificable). [Dec Q1] [Plan]
- `MapInboundClaims = false`, `NameClaimType = "sub"`, `RoleClaimType = "role"`: los claims conservan los nombres del contrato (`sub`, `role`) sin traducirse a URIs. [Plan]
- `OnTokenValidated`: si `sub` no es un entero positivo, `context.Fail(...)` → 401. [Spec FR-007]
- Autorización con `[Authorize(Roles = "Administrador,Operador")]`: sin token → 401 (desafío); rol ausente o distinto → 403. Sin políticas personalizadas. [Spec FR-007] [Plan]
- Los cuerpos de 401/403 los genera la `UseStatusCodePages` existente (el desafío y la prohibición no escriben cuerpo): se añaden sus mensajes. No se crea otro formato. [Dec §5] [F002]
- La misma clave, emisor y audiencia se darán a Autenticación (emisión) y al Gateway (validación) en sus features. [Dec Q1]
- Opciones validadas al arrancar (`ValidateOnStart`): sin clave válida, Control no arranca. [Plan]

## D-03 Usuario en Control (FR-008, FR-009)

- Se incorpora la entidad `Usuario` con **solo `Id` y `Email`**: Control no necesita `RolId` (el rol viene del token) y no debe leer `PasswordHash` (mínimo privilegio). Es una excepción justificada a la regla de F004 "todas las columnas", amparada por spec FR-009: EF Core solo consulta las columnas mapeadas y Control nunca inserta ni actualiza `Usuario`. [Spec] [Plan]
- Se obtiene con el procedimiento de ingeniería inversa de F004 (`--table dbo.Usuario`, salida fuera del repositorio) y se integra a mano; relación `CargaArchivo.UsuarioId → Usuario` con `HasOne<Usuario>().WithMany().HasForeignKey(...)`, sin navegaciones. [F004] [Plan]
- Lectura por `IGenericRepository<Usuario>.FindAsync(id)`. [Plan]

## D-04 Validaciones del archivo (FR-010 a FR-016)

- **Recepción**: el controlador lee `Request.ReadFormAsync` solo si `Request.HasFormContentType`; si no, se trata como "sin archivo" (400, no 415 ni 500). Se cuentan **todos** los archivos del formulario, con cualquier nombre de campo. [Plan]
- **Validador FluentValidation** del comando: cantidad = 1; longitud > 0; extensión `.xlsx` con `StringComparison.OrdinalIgnoreCase`; nombre (`Path.GetFileName`) no vacío y ≤ 260; longitud ≤ máximo. Mensajes en `Errors`. Se reutiliza `ValidationBehavior` (400). [Spec] [F002]
- **Tamaño máximo**: opción `CargaArchivo__TamanoMaximoBytes`, predeterminada 10 485 760 en `appsettings.json`; validada al arrancar (> 0; un valor no numérico falla el enlace) → Control no arranca. [Dec Q2] [Plan]

## D-05 Límites HTTP para el multipart (FR-015)

- Valores predeterminados de ASP.NET Core [Sin verif en documentación en esta sesión; conocidos]: `KestrelServerOptions.Limits.MaxRequestBodySize` = 30 000 000 bytes; `FormOptions.MultipartBodyLengthLimit` = 134 217 728. Con el máximo predeterminado (10 MiB) ambos bastan.
- Para no depender de ellos si se configura otro máximo, se fijan ambos a **máximo + 1 MiB** de margen (fronteras y cabeceras del multipart son de pocos cientos de bytes). [Dec Q2] [Plan]
- Comportamiento resultante: archivo ≤ máximo → aceptado; entre máximo y máximo + margen → 400 del validador; por encima → el servidor corta la lectura: `BadHttpRequestException` (413) o `InvalidDataException` del lector multipart. El controlador captura `InvalidDataException` de `ReadFormAsync` y el manejador global traduce `BadHttpRequestException` a su `StatusCode`; ambos responden `Response<T>` con `code` 413. [Spec FR-014, FR-004] [Plan]
- Se comprueba en `quickstart.md` con archivos de 10 485 760, 10 485 761 y > 11 534 336 bytes.

## D-06 SeaweedFS (FR-015, FR-018)

- Un contenedor `weed server -dir=/data -filer` (maestro, volumen y filer); volumen de Docker `seaweedfs-data`. [Plan] [Dec §11]
- **Subida**: `PUT {SeaweedFs__FilerUrl}/cargas/{AAAA}/{MM}/{guid}.xlsx` con el archivo como cuerpo. La documentación del filer describe POST/PUT a una ruta como "crear o sobrescribir" [Verif: wiki "Filer Server API"]: reintentar la misma ruta es idempotente. El éxito es un estado 2xx (el código exacto no consta en la documentación; se comprueba en la implementación). [Plan]
- **Referencia registrada** (`RutaArchivo` y `rutaArchivo`): `seaweed://cargas/{AAAA}/{MM}/{guid}.xlsx`, con el esquema del ejemplo del README; Carga Masiva la traduce a la URL del filer con su propia configuración. Longitud ≈ 64 caracteres (≤ 500). El nombre original no forma parte de la ruta (evita caracteres problemáticos y colisiones). [README §3] [Plan]
- El `guid` se genera **antes** de subir; los reintentos reutilizan la misma ruta y vuelven a abrir el flujo del `IFormFile`.

## D-07 RabbitMQ y confirmación de publicación (FR-021, FR-022)

- Topología declarada por Control de forma idempotente antes de publicar: intercambio **directo** durable `carga_masiva`, cola durable `carga_masiva` y enlace con clave de enrutamiento `carga_masiva`. Mensaje persistente (`DeliveryMode` persistente), `ContentType` `application/json`, `mandatory = true`. [README §4 "intercambio directo"] [Plan]
- **Confirmación**: canal creado con `CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true)`. Con seguimiento activo, la biblioteca correlaciona confirmaciones y `basic.return`, y señala rechazos con `PublishException` (`IsReturn` distingue un mensaje no enrutable). [Verif: código fuente `CreateChannelOptions.cs` y `PublishException.cs` de rabbitmq-dotnet-client] Que `BasicPublishAsync` espere la confirmación antes de completar con el seguimiento activo **se comprueba en la implementación** [Sin verif].
- Tiempo de espera por intento (Polly) de 5 s: si vence, el resultado es **desconocido** (no se afirma que el mensaje no llegó). [Dec]
- Conexión única (`IConnection`, creada de forma diferida y reutilizada; se recrea si se cerró) y un canal con confirmaciones **por publicación** (los canales no admiten publicaciones concurrentes). La conexión no se abre al arrancar: `/health` no depende de RabbitMQ. [Plan]
- **Duplicados**: un reintento tras un tiempo de espera puede publicar dos veces. Se acepta porque el consumidor descarta cualquier mensaje cuya carga no esté Pendiente [F001 R-03]; no se añade identificador de deduplicación ni Outbox. [Dec]

## D-08 Script V003 y uso desde Control (FR-023, FR-032)

- `database/scripts/V003__estado_origen_esperado.sql`: `ALTER PROCEDURE dbo.usp_CambiarEstadoCarga` con el mismo cuerpo de V002 y un parámetro nuevo **al final** `@EstadoEsperado VARCHAR(20) = NULL` (después de `@Cambiado OUTPUT`, para no alterar llamadas posicionales). [Dec opción A] [Plan]
- Tras leer `@actual` con `UPDLOCK, ROWLOCK` dentro de la transacción del procedimiento: si `@EstadoEsperado IS NOT NULL AND @actual <> @EstadoEsperado` → `ROLLBACK`, `RETURN` con `@Cambiado = 0`. Un `@EstadoEsperado` fuera de los seis estados → `THROW 50023`. Lo demás (matriz, terminales, `@@TRANCOUNT`, historial, periodos) queda idéntico. [Spec FR-032]
- El runner lo aplica en su propia transacción y lo registra en `SchemaMigraciones`; V001 y V002 no cambian (su checksum se conserva). La imagen del runner copia `scripts/` al construirse: hace falta `--build`. [F003] [Verif: `run-scripts.sh`, `database/Dockerfile`]
- Control lo invoca con EF Core (`Database.ExecuteSqlRawAsync` con `SqlParameter` de salida para `@Cambiado`), fuera de cualquier transacción abierta, con parámetros con nombre. Sin Dapper. [Dec §6]

## D-09 Flujo del caso de uso y fallos (FR-017 a FR-025)

Orden en el handler `RegistrarCargaCommandHandler`:
1. (Antes del handler) Rate Limiting → autenticación → autorización → validador.
2. Leer `Usuario` por `sub`; inexistente → `Response.Fail(401, "Usuario no reconocido")`, sin efectos.
3. Generar la ruta y **subir** a SeaweedFS (Polly). Fallo definitivo → la excepción llega al manejador global (500). Sin carga ni mensaje.
4. **Registrar** en una transacción (`IUnitOfWork.ExecuteInTransactionAsync`): `CargaArchivo` (Pendiente, `FechaRecepcion` UTC, `IntentosProceso` 0) → `SaveChanges` → `CargaEstadoHistorial` (Pendiente, misma fecha) → `SaveChanges` → commit. Fallo → log de error con la ruta del archivo sin carga y la excepción sube (500). Sin mensaje. Sin limpieza del archivo (no se intenta borrarlo: un borrado fallido añadiría otro caso sin valor).
5. **Publicar** (Polly). Éxito → 202.
6. Fallo definitivo de publicación → `usp_CambiarEstadoCarga(CargaId, 'Fallido', @EstadoEsperado = 'Pendiente')` (Polly):
   - `Cambiado = 1` → insertar `Incidencia` (`Tipo` = `ErrorTecnico`, `CargaPeriodoId` NULL, `Motivo` = "No se pudo confirmar la publicación del mensaje de procesamiento.", `FechaRegistro` UTC) → la excepción original sube (500).
   - `Cambiado = 0` → la carga salió de Pendiente: el mensaje llegó → log de advertencia y 202 (spec US5-5, FR-024).
   - Si el procedimiento o la incidencia fallan → log de error con `idCarga` y ambas causas; la excepción original sube (500). Limitación documentada.
- Desde el paso 3 se usa `CancellationToken.None`: una desconexión del cliente no debe interrumpir el flujo a mitad y dejar estados intermedios evitables. [Plan]
- **Mínima ampliación de `IUnitOfWork`**: `Task ExecuteInTransactionAsync(Func<CancellationToken, Task> trabajo, CancellationToken ct)`. Hace falta porque la entrada de historial necesita el `Id` generado de la carga y F004 prohíbe navegaciones. Alternativas descartadas: navegaciones (contrarias a F004); procedimiento nuevo para crear la carga (amplía `database/` sin decisión). [Plan] [Dec §4 "Repository y UoW con las operaciones necesarias"]

## D-10 Polly por operación (FR-026)

| Operación | Reintentos | Espera | Errores reintentables | Tiempo por intento | Circuit Breaker | Riesgo de duplicación |
|---|---|---|---|---|---|---|
| Subida a SeaweedFS (`PUT` ruta fija) | 3 | exponencial 1 s, 2 s, 4 s con jitter | `HttpRequestException`, tiempo de espera de Polly, 408, 429, 5xx | 30 s | ratio 0,5; mínimo 5 llamadas; muestreo 30 s; apertura 30 s | Ninguno: sobrescribe la misma ruta |
| Publicación en RabbitMQ | 2 | exponencial 1 s, 2 s | `BrokerUnreachableException`, `AlreadyClosedException`, `OperationInterruptedException`, `PublishException` (no `IsReturn`), tiempo de espera de Polly | 5 s | ratio 0,5; mínimo 5; muestreo 30 s; apertura 30 s | Tolerado: el consumidor descarta cargas fuera de Pendiente |
| `usp_CambiarEstadoCarga` (Fallido desde Pendiente) | 3 | exponencial 1 s, 2 s, 4 s | `SqlException` transitoria (números 1205, -2, 53, 10053, 10054, 10060, 40613, 40197, 40501) y `TimeoutException` | 15 s | ratio 0,5; mínimo 5; muestreo 30 s; apertura 30 s | Ninguno: idempotente por `@EstadoEsperado` y la matriz |
| Registro de carga + historial | **0** | — | — | — | — | Alto: un commit ambiguo reintentado crearía otra carga Pendiente sin mensaje |
| Inserción de la incidencia `ErrorTecnico` | **0** | — | — | — | — | Medio: incidencia duplicada |
| Lectura de `Usuario` | **0** | — | — | — | — | Ninguno, pero ocurre antes de cualquier efecto: un fallo responde 500 sin consecuencias |

- **Nunca** se reintentan errores de validación, autenticación ni autorización: ocurren antes del handler o se devuelven como `Response` sin excepción. Un circuito abierto (`BrokenCircuitException`) no se reintenta y cuenta como fallo definitivo. [Dec]
- Los números de error SQL transitorios y la clasificación exacta de excepciones de RabbitMQ 7.x se comprueban en la implementación [Sin verif]. Los parámetros se fijan como constantes del pipeline (no configurables): ajustarlos no está pedido. [Plan]
- Cada pipeline es un `ResiliencePipeline` singleton construido en `Acity.Infrastructure/DependencyInjection.cs` y usado solo por su implementación (SeaweedFS, RabbitMQ, procedimiento). La aplicación no depende de Polly. [Dec §4] [Plan]

## D-11 Rate Limiting (FR-027)

- Middleware integrado: política con nombre `carga`, ventana fija particionada por `sub` (o por IP remota si no hay usuario autenticado), `PermitLimit` 10 por 60 s, `QueueLimit` 0, `RejectionStatusCode` 429. Configurable con `RateLimiting__LimitePorVentana` y `RateLimiting__VentanaSegundos`. Aplicada con `[EnableRateLimiting("carga")]` solo al endpoint de carga. [Dec] [Plan]
- Orden: `UseExceptionHandler` → `UseStatusCodePages` → `UseAuthentication` → `UseRateLimiter` → `UseAuthorization` → controladores. Así la partición conoce `sub` y una solicitud rechazada por frecuencia no llega al handler. [Plan]
- El cuerpo del 429 lo genera `UseStatusCodePages` (se añade el mensaje). No se configura en el Gateway. [Dec]

## D-12 Endpoint y respuestas (FR-001 a FR-004)

- `POST /cargas`, `multipart/form-data`, campo `archivo` (documental: se cuentan todos los archivos). Sin prefijo `/api` (como `/health`; el Gateway, en su feature, enrutará). [Plan] [F002]
- El controlador construye el comando con el `sub`, envía con `IMediator.Send` (como la referencia) y devuelve `StatusCode(response.Code, response)`. El handler devuelve `Response<RegistrarCargaResponse>` (`record` con `IdCarga` y `Estado`). [Ref TodoController] [Dec §4, §5]
- Serialización: la predeterminada de ASP.NET Core (camelCase, nulos incluidos) ya cumple FR-004, tanto en controladores como en `WriteAsJsonAsync`; no se configura nada, solo se comprueba. [Dec §5] [Plan]
- `GlobalExceptionHandler`: se añade `BadHttpRequestException` → su `StatusCode` (413 con "El archivo excede el tamaño permitido"; otros, "Solicitud no válida"). `UseStatusCodePages`: mensajes para 401 ("No autenticado"), 403 ("Sin permiso"), 413 y 429 ("Demasiadas solicitudes"). [Plan]

## D-13 Logs (FR-029)

- Información: carga aceptada (`IdCarga`, `UsuarioId`, `NombreArchivo`, `Longitud`, `RutaArchivo`). Advertencia: publicación no confirmada con carga ya fuera de Pendiente (`IdCarga`). Error: subida, registro (con `RutaArchivo` del archivo sin carga), publicación, cambio a Fallido o incidencia fallidos, con la excepción.
- Nunca: el token, la clave JWT, contraseñas de RabbitMQ, cadenas de conexión ni el contenido del archivo. No se registra el correo (no es necesario para la trazabilidad; se identifica por `UsuarioId`). [Dec §10]

## D-14 Docker Compose y configuración (FR-031)

- Servicios nuevos: `rabbitmq` (healthcheck `rabbitmq-diagnostics -q ping`; volumen `rabbitmq-data`; puerto de gestión 15672 publicado para la verificación manual) y `seaweedfs` (volumen `seaweedfs-data`; puerto 8888 publicado para la verificación). Sin directorios nuevos (no requieren archivos de configuración). [Dec §11]
- `ms-control`: `depends_on` añade `rabbitmq` (`service_healthy`) y `seaweedfs` (`service_started`); variables `Jwt__*`, `RabbitMq__Host/Puerto/Usuario/Clave`, `SeaweedFs__FilerUrl`. Los demás servicios no cambian. [Spec FR-031]
- `.env.example`: `RABBITMQ_USER`, `RABBITMQ_PASSWORD`, `JWT_CLAVE` (vacíos, obligatorios con `:?`), `JWT_EMISOR` (`ms-autenticacion`), `JWT_AUDIENCIA` (`carga-masiva`). Sin valores secretos en el repositorio. [Dec §6, §10]

## Abiertos (estado tras la implementación; detalle en `quickstart.md`, "Resultados")

1. **Resuelto**: `rabbitmq:4.1.8-management` y `chrislusf/seaweedfs:3.97` (arm64 nativas en este equipo).
2. **Parcial**: con seguimiento activo, `BasicPublishAsync` completó solo tras la confirmación (los mensajes estaban en la cola al responder 202). No se provocaron `nack` ni `return` (requieren un broker que rechace o un intercambio sin cola enlazada).
3. **Resuelto**: el filer en un solo contenedor (`server -filer`) tarda unos 20 s en responder; `PUT` → 201 y repetir el `PUT` sobrescribe.
4. **Resuelto**: con los límites fijados a 11 534 336, el servidor corta por encima y la respuesta es 413 con `Response<T>`. Hallazgo: el enlace de modelos de `[ApiController]` leía antes el formulario y devolvía un `ProblemDetails`; se corrigió con el filtro `SinEnlaceDeFormularioAttribute`.
5. **Parcial**: observadas `BrokerUnreachableException` (RabbitMQ detenido) y `HttpRequestException` sin código (SeaweedFS detenido o sin red), ambas reintentadas. Los números SQL transitorios no se provocaron.
6. **Pendiente**: validación en el equipo de evaluación no realizada (motor amd64 emulado en este equipo).

**Desviaciones de implementación** (mínimas, sin cambiar el diseño): `Microsoft.Extensions.Logging.Abstractions` 10.0.14 en Application (logs del handler, FR-029); `FrameworkReference Microsoft.AspNetCore.App` en Infrastructure (enlace de opciones e `IHttpClientFactory`; no es paquete NuGet); `CargaArchivoOptions` se inyecta al validador como singleton (sin `IOptions` en Application); filtro `SinEnlaceDeFormularioAttribute` (D-05).
