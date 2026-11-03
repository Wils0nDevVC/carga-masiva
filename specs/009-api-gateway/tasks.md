# Tasks: API Gateway mínimo con Ocelot

**Input**: `specs/009-api-gateway/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Alcance**:
- Se tocan `api-gateway/` y, en `compose.yaml`, solo el servicio `api-gateway` y el volumen `logs-gateway`.
- La verificación se hace fuera del repositorio.
- No se tocan Autenticación, Control ni los demás servicios, el esquema, `.env`, `referencias/` ni la constitución.
- No hay pruebas automatizadas (constitución §11).

**Reglas de ejecución**
- **Datos de prueba**: solo en `CargaMasivaApp_F009`, con toda la pila apuntando a esa base.
  - No se tocan `CargaMasiva`, `CargaMasivaDb` ni volúmenes ajenos.
  - Nunca se usa `docker compose down -v`.
- **Secretos**: solo por entorno. De `.env` se leen únicamente las variables necesarias (`ADMIN_*`, `OPERADOR_*` y `JWT_*`). No se imprimen tokens ni contraseñas.
- **Exclusiones** (constitución §9): no se añaden Rate Limiting, QoS, caché, descubrimiento de servicios, Swagger, cabeceras propias ni comprobación de rol.
- **Marcado**: `[x]` solo para lo ejecutado y verificado.
- **Cierre**: entregar el comando de commit en español, sin `Co-Authored-By`.

## Phase 1: Setup

- [x] T001 Crear `api-gateway/src/Acity.Api/Acity.Api.csproj` (`Microsoft.NET.Sdk.Web`, `net10.0`, `Nullable` y `ImplicitUsings` activados, `NoDefaultLaunchSettingsFile`):
  - Paquetes `Ocelot` 25.0.2, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14 y `Serilog.AspNetCore` 10.0.0.
  - `ocelot.json` con `CopyToOutputDirectory=PreserveNewest`.
  - Crear también `api-gateway/src/Acity.Gateway.slnx` con ese único proyecto (research D-01, D-02).
- [x] T002 [P] Crear `api-gateway/Dockerfile`, copia del de `ms-autenticacion/Dockerfile`: SDK 10.0 → aspnet 10.0, `/app/logs` propiedad de `$APP_UID`, `ASPNETCORE_HTTP_PORTS=8080` y `ENTRYPOINT dotnet Acity.Api.dll`.
- [x] T003 [P] Crear `api-gateway/src/Acity.Api/appsettings.json`, copia del de ms-autenticacion, con estos cambios:
  - `Properties.Application` = `api-gateway`.
  - Archivo `logs/gateway-.log`.
  - Override `"Ocelot": "Warning"`.
  - Sin registro de solicitudes ni de cabeceras (research D-07).

## Phase 2: Foundational

- [x] T004 [P] Crear `api-gateway/src/Acity.Api/Helpers/Response.cs`, copia literal del `record Response<T>` de `ms-autenticacion/src/Acity.Common/Helpers/Response.cs`, con el namespace `Acity.Api.Helpers` (research D-02).
- [x] T005 [P] Crear `api-gateway/src/Acity.Api/Options/JwtOptions.cs`, copia de `ms-control/src/Acity.Api/Options/JwtOptions.cs`: `Clave` de al menos 32 bytes, `Emisor` y `Audiencia` obligatorios.
- [x] T006 [P] Crear `api-gateway/src/Acity.Api/Controllers/HealthController.cs`: `GET /health` → `Ok(Response<string>.Ok("Healthy"))`, igual que en los servicios.
- [x] T007 Crear `api-gateway/src/Acity.Api/Program.cs` (research D-04 a D-06):
  - **Arranque**: bootstrap logger, `UseSerilog` desde la configuración y `try/catch/finally` con `Log.Fatal` y código de salida 1, igual que en los servicios.
  - **Configuración**: `AddJsonFile("ocelot.json", optional: false)`.
  - **Opciones**: `AddOptions<JwtOptions>().Bind("Jwt").Validate(EsValida, mensaje sin valores).ValidateOnStart()`.
  - **JWT**: `AddAuthentication().AddJwtBearer("Bearer", …)` con `TokenValidationParameters` idénticos a los de `ms-control/src/Acity.Api/Program.cs`:
    - `MapInboundClaims = false`;
    - emisor, audiencia, vigencia y `RequireExpirationTime`;
    - `ValidAlgorithms = [HS256]`, `ClockSkew = 0`, `NameClaimType = "sub"` y `RoleClaimType = "role"`;
    - `OnTokenValidated`, que falla si `sub` no es un entero > 0.
  - **Servicios**: `AddControllers()` y `AddOcelot()`.
  - **Pipeline**, en este orden:
    1. `UseExceptionHandler`, que escribe un 500 `Response<object>.Fail(500, "Error interno del servidor")` sin detalles.
    2. `UseStatusCodePages`, que escribe `Response<object>.Fail(code, mensaje)` con 401 "No autorizado", 404 "No encontrado", 502 "Servicio no disponible" y, en cualquier otro caso, "Error".
    3. `UseRouting()`.
    4. `UseEndpoints(e => e.MapControllers())`.
    5. `UseAuthentication()`.
    6. `await app.UseOcelot()`.
    7. `await app.RunAsync()`.

**Checkpoint**: `dotnet build api-gateway/src/Acity.Gateway.slnx` sin advertencias ni errores.

## Phase 3: US1 - Subir una carga a través del Gateway (P1) 🎯 MVP

**Objetivo**: `POST /cargas` con JWT válido llega a Control; un token inválido da 401 `Response<T>` sin reenviar la solicitud.

**Prueba independiente**: quickstart, escenarios 1 y 3 a 9 (el token se obtiene directamente de :8081).

- [x] T008 [US1] Crear `api-gateway/src/Acity.Api/ocelot.json` con la ruta `/cargas` (research D-03):
  - `UpstreamPathTemplate` `/cargas`, `UpstreamHttpMethod` `["Post"]`.
  - `DownstreamScheme` `http`, `DownstreamHostAndPorts` `[{ "Host": "ms-control", "Port": 8080 }]` y `DownstreamPathTemplate` `/cargas`.
  - `AuthenticationOptions.AuthenticationProviderKeys` `["Bearer"]`.
  - `GlobalConfiguration.BaseUrl` `http://localhost:8080`.
  - Sin `RouteClaimsRequirement`, `RateLimitOptions`, `QoSOptions`, `FileCacheOptions` ni `ServiceDiscoveryProvider`.
- [x] T009 [US1] Añadir a `compose.yaml` el servicio `api-gateway` y el volumen `logs-gateway`:
  - build `./api-gateway`, imagen `carga-masiva-api-gateway:local`.
  - `depends_on` `ms-autenticacion` y `ms-control` con `condition: service_healthy`.
  - `environment`: `ASPNETCORE_ENVIRONMENT: Production`, `Jwt__Clave: ${JWT_CLAVE:?…}`, `Jwt__Emisor: ${JWT_EMISOR:-ms-autenticacion}` y `Jwt__Audiencia: ${JWT_AUDIENCIA:-carga-masiva}`.
  - `ports` `"8080:8080"`, `volumes` `logs-gateway:/app/logs`, `healthcheck: *app-healthcheck` y `restart: "no"`.
  - Comprobar con `docker compose config -q`.
- [x] T010 [US1] Verificar los escenarios 1, 3, 4, 5, 6, 7 y 8 del quickstart sobre `CargaMasivaApp_F009`:
  - **Abierto 1**: el Excel de ejemplo y un archivo mayor de 10 MiB (413 de Control) atraviesan el Gateway.
  - **Abierto 2**: los 401, 404 y 502 salen en `Response<T>`.
  - **Abierto 3**: `/health` responde 200 sin pasar por Ocelot.
  - Registrar los resultados en `specs/009-api-gateway/quickstart.md`.

## Phase 4: US2 - Autenticarse a través del Gateway (P1)

**Objetivo**: el login y el refresh funcionan a través del Gateway sin token de acceso.

**Prueba independiente**: quickstart, escenario 2.

- [x] T011 [US2] Añadir a `api-gateway/src/Acity.Api/ocelot.json` dos rutas `POST`, ambas hacia `ms-autenticacion:8080` y sin `AuthenticationOptions`:
  - `/auth/login` → `/auth/login`.
  - `/auth/refresh` → `/auth/refresh`.
- [x] T012 [US2] Verificar el escenario 2 del quickstart:
  - Login correcto (200), login erróneo (401 genérico de Autenticación, sin volver a envolverlo) y refresh (200).
  - Comparar estado y cuerpo con :8081, sin imprimir los tokens.
  - Repetir el flujo completo (login → `/cargas`) solo contra :8080, hasta Notificado.
  - Registrar los resultados.

## Phase 5: Polish

- [x] T013 Escenario 9: buscar con `grep -F` los tokens emitidos, `JWT_CLAVE` y `Bearer ` en los logs del Gateway (`docker compose logs api-gateway` y el volumen `logs-gateway`). El resultado esperado es 0 coincidencias.
- [x] T014 Cierre:
  - Borrar solo los archivos de SeaweedFS y los correos de Mailpit de esta prueba.
  - Eliminar `CargaMasivaApp_F009`.
  - Volver al arranque normal sobre `CargaMasivaApp` y comprobar que todo está `healthy`, incluido `api-gateway`.
  - Completar "Resultados" en `quickstart.md`, separando lo ejecutado de lo no ejecutado.
  - Entregar el comando de commit.

## Estado de la implementación (2026-11-03)

- **14 de 14 tareas hechas.** Compilación: 0 advertencias, 0 errores. Escenarios 1–9 ejecutados sobre `CargaMasivaApp_F009`; el detalle está en `quickstart.md`.
- **Ajuste respecto al plan**: `UseEndpoints` explícito, con la advertencia del analizador ASP0014 suprimida y justificada en el código. Con rutas de nivel superior, `/health` se ejecutaría después de Ocelot, que es terminal.
- **Orden**: T011 (rutas de autenticación) se completó antes de cerrar T010, porque el script de verificación necesitaba el login a través del Gateway. El primer intento sin esas rutas sirvió además como evidencia del 404 `Response<T>`.

## Dependencias y orden

- Setup (T001 a T003) → Foundational (T004 a T007) → US1 (T008 a T010) → US2 (T011 y T012) → Polish (T013 y T014).
- T007 depende de T004 y T005. T008 y T011 editan el mismo archivo (`ocelot.json`), así que van en secuencia.
- US2 no depende de US1 para su lógica, pero comparte `ocelot.json` y el servicio de Compose de T009.

## Paralelismo

- Setup: T002 y T003 en paralelo, después de T001.
- Foundational: T004, T005 y T006 en paralelo; después, T007.

## Estrategia

- **MVP**: US1, la subida con JWT a través del Gateway. Es el flujo del README §3, pasos 1 y 2.
- US2 completa el punto de entrada único: login y refresh sin conocer la dirección de Autenticación.
