# Tasks: Autenticación con JWT y refresh tokens en ms-autenticacion

**Input**: `specs/008-autenticacion-jwt/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Alcance**: `ms-autenticacion/`, `database/scripts/V005__refresh_token.sql`, `compose.yaml` (solo el servicio ms-autenticacion) y `.env.example`. La verificación se hace fuera del repositorio.

No se tocan Control ni los demás servicios, `referencias/` ni la constitución. No hay pruebas automatizadas (constitución §11).

**Reglas de ejecución**
- **Valores de `.env`**: `ADMIN_EMAIL`, `ADMIN_CLAVE`, `OPERADOR_EMAIL` y `OPERADOR_CLAVE` los define el usuario. El agente no escribe esos valores sin autorización expresa.
- **Ingeniería inversa manual** (T005): la ejecuta el usuario, o Claude con autorización expresa. La salida queda fuera del repositorio.
- **Datos de prueba**: solo en `CargaMasivaApp_F008`.
  - No se tocan `CargaMasiva`, `CargaMasivaDb` ni volúmenes ajenos.
  - Nunca se ejecuta `docker compose down -v`.
  - Los secretos solo se pasan por entorno y nunca se imprimen.
- **Marcado**: `[x]` solo para lo ejecutado y verificado.
- **Cierre**: entregar el comando de commit en español, sin `Co-Authored-By`.

## Phase 1: Setup

- [ ] T001 Añadir a `ms-autenticacion/src/Acity.Infrastructure/Acity.Infrastructure.csproj` el paquete `Microsoft.IdentityModel.JsonWebTokens` 8.19.2 y `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. Comprobar con `dotnet build ms-autenticacion/src/Acity.Autenticacion.slnx` (research D-01).
- [ ] T002 [P] Crear `database/scripts/V005__refresh_token.sql`, copiado literalmente de `contracts/v005-refresh-token.md`:
  - Cabecera de comentario como la de V004, con `SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;`.
  - `dbo.RefreshToken` con `Id BIGINT IDENTITY PK`, `UsuarioId INT NOT NULL FK Usuario(Id)`, `TokenHash BINARY(32) NOT NULL UNIQUE (UQ_RefreshToken_TokenHash)`, `FechaCreacion DATETIME2 NOT NULL`, `FechaExpiracion DATETIME2 NOT NULL`, `FechaUso DATETIME2 NULL` y `CK_RefreshToken_Fechas CHECK (FechaExpiracion > FechaCreacion)`.
  - Sin datos, procedimientos ni índices adicionales.
- [ ] T003 [P] En `compose.yaml`, añadir al `environment` de `ms-autenticacion`:
  - `Jwt__Clave: ${JWT_CLAVE:?...}`, `Jwt__Emisor: ${JWT_EMISOR:-ms-autenticacion}` y `Jwt__Audiencia: ${JWT_AUDIENCIA:-carga-masiva}`, igual que en ms-control.
  - `Jwt__VigenciaAccesoMinutos: ${JWT_VIGENCIA_ACCESO_MINUTOS:-60}` y `Jwt__VigenciaRefreshMinutos: ${JWT_VIGENCIA_REFRESH_MINUTOS:-60}`.
  - `UsuariosIniciales__Administrador__Email: ${ADMIN_EMAIL:?...}`, `UsuariosIniciales__Administrador__Clave: ${ADMIN_CLAVE:?...}`, `UsuariosIniciales__Operador__Email: ${OPERADOR_EMAIL:?...}` y `UsuariosIniciales__Operador__Clave: ${OPERADOR_CLAVE:?...}`.
  - Comprobar con `docker compose config -q`, sin imprimir valores (research D-06).
- [ ] T004 [P] Documentar en `.env.example` las variables `JWT_VIGENCIA_ACCESO_MINUTOS=60`, `JWT_VIGENCIA_REFRESH_MINUTOS=60`, `ADMIN_EMAIL=`, `ADMIN_CLAVE=`, `OPERADOR_EMAIL=` y `OPERADOR_CLAVE=`. Los valores van vacíos y con un comentario de que son obligatorias y las define el usuario. Sin secretos.

## Phase 2: Foundational (bloquea todas las historias)

- [ ] T005 **Manual**: aplicar V005 en una base temporal `CargaMasivaApp_F008` con el runner (`DB_NAME=CargaMasivaApp_F008 docker compose up database-runner`). Comprobar que `SchemaMigraciones` tiene 5 registros. Hacer la ingeniería inversa de `dbo.RefreshToken` (`dotnet ef dbcontext scaffold … --table RefreshToken`) con la salida fuera del repositorio, y contrastarla con data-model.md.
- [ ] T006 [P] Crear `ms-autenticacion/src/Acity.Domain/Entities/RefreshToken.cs`, a partir de T005, con `long Id`, `int UsuarioId`, `byte[] TokenHash`, `DateTime FechaCreacion`, `DateTime FechaExpiracion` y `DateTime? FechaUso`.
- [ ] T007 Crear `ms-autenticacion/src/Acity.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs` con `ToTable("RefreshToken")`, la clave `Id`, `TokenHash` como `binary(32)` (`IsFixedLength`, `HasMaxLength(32)`) con índice único y la FK a `Usuario` sin navegación, igual que `UsuarioConfiguration`.
- [ ] T008 [P] Crear `ms-autenticacion/src/Acity.Infrastructure/Security/JwtOptions.cs`, copiada de `ms-control/src/Acity.Api/Options/JwtOptions.cs`:
  - Añade `int VigenciaAccesoMinutos = 60` y `int VigenciaRefreshMinutos = 60`.
  - `EsValida` exige además que ambos valores sean > 0.
- [ ] T009 [P] Crear las interfaces en `ms-autenticacion/src/Acity.Application/Interfaces/Services/`:
  - `IEmisorTokens.cs`:
    - `(string Token, DateTime ExpiraEn) CrearTokenAcceso(int usuarioId, string rol)`.
    - `(string Valor, byte[] Hash, DateTime ExpiraEn) CrearRefreshToken()`.
    - `byte[] CalcularHash(string valor)`.
  - `IHashContrasena.cs`:
    - `string Generar(Usuario usuario, string contrasena)`.
    - `bool Verificar(Usuario usuario, string hash, string contrasena)`.
- [ ] T010 [P] Crear `ms-autenticacion/src/Acity.Application/Interfaces/Repositories/IRefreshTokenRepository.cs` con `Task<bool> MarcarUsadoAsync(long id, DateTime ahora, CancellationToken ct)`.
- [ ] T011 Crear `ms-autenticacion/src/Acity.Infrastructure/Security/EmisorTokens.cs`, que implementa `IEmisorTokens` (research D-02 y D-03):
  - Token de acceso con `JsonWebTokenHandler.CreateToken`:
    - Claims `sub` = `usuarioId.ToString()` y `role` = `rol`.
    - `Issuer`, `Audience`, `IssuedAt` y `NotBefore` = `DateTime.UtcNow`; `Expires` = ahora + `VigenciaAccesoMinutos`.
    - `SigningCredentials` HS256 con la clave en UTF-8.
  - Refresh token: 32 bytes de `RandomNumberGenerator` en `Base64Url`; hash `SHA256.HashData(UTF8(valor))`; expira a ahora + `VigenciaRefreshMinutos`.
- [ ] T012 [P] Crear `ms-autenticacion/src/Acity.Infrastructure/Security/HashContrasena.cs`, que implementa `IHashContrasena` con `PasswordHasher<Usuario>`. `Verificar` devuelve true para `Success` y para `SuccessRehashNeeded`, sin regrabar el hash.
- [ ] T013 Crear `ms-autenticacion/src/Acity.Infrastructure/Repositories/RefreshTokenRepository.cs`. `MarcarUsadoAsync` hace `ExecuteUpdateAsync` sobre `RefreshToken` con `Id == id && FechaUso == null` y asigna `FechaUso = ahora`. Devuelve `filas == 1`.
- [ ] T014 Actualizar `ms-autenticacion/src/Acity.Infrastructure/DependencyInjection.cs`:
  - `AddOptions<JwtOptions>().Bind(section "Jwt").Validate(EsValida, mensaje sin valores).ValidateOnStart()`.
  - Registrar `IEmisorTokens` → `EmisorTokens` (singleton), `IHashContrasena` → `HashContrasena` (singleton) e `IRefreshTokenRepository` → `RefreshTokenRepository` (scoped).
- [ ] T015 Crear `ms-autenticacion/src/Acity.Application/Autenticacion/Dtos/TokensDto.cs`: `record TokensDto(string AccessToken, DateTime ExpiraEn, string RefreshToken, DateTime RefreshExpiraEn)`, según `contracts/auth-api.md`.
- [ ] T016 En `ms-autenticacion/src/Acity.Api/Program.cs`, `AddControllers().ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = …)`. Debe devolver `BadRequest(Response<object>.Fail(400, "Error de validación", errores))` con los mensajes del `ModelState`, sin `ProblemDetails` (research D-04).

**Checkpoint**: `dotnet build` sin advertencias ni errores.

## Phase 3: US3 - Usuario inicial por rol (P1)

**Objetivo**: al arrancar existen un Administrador y un Operador. Es requisito previo para probar US1 y US2.

**Prueba independiente**: quickstart, escenarios 1 a 3.

- [ ] T017 [P] [US3] Crear `ms-autenticacion/src/Acity.Api/Options/UsuariosInicialesOptions.cs`:
  - Secciones `Administrador` y `Operador`, cada una con `Email` y `Clave`.
  - `EsValida` exige los cuatro valores no vacíos, sin longitud mínima.
  - Constante `Seccion = "UsuariosIniciales"`.
- [ ] T018 [US3] Crear `ms-autenticacion/src/Acity.Application/Autenticacion/Commands/CrearUsuariosIniciales/{CrearUsuariosInicialesCommand,CrearUsuariosInicialesCommandHandler}.cs` (research D-05):
  - El comando es `record(IReadOnlyList<(string Rol, string Email, string Clave)> Usuarios)`. Para cada usuario:
    - Buscar el `Rol` por `Nombre`; si no existe, lanzar una excepción ("rol no sembrado").
    - Si existe un `Usuario` con ese `Email`: no se modifica. Si su `RolId` es distinto, registrar una advertencia; si no, registrar "ya existe" con `UsuarioId`.
    - Si no existe: crear un `Usuario` con `PasswordHash = IHashContrasena.Generar(...)`, guardar y registrar "creado" con `Rol` y `UsuarioId`.
  - Una `DbUpdateException` por `UQ_Usuario_Email` se trata como "ya existe". Como el handler no ve EF, se detecta volviendo a consultar el correo tras la excepción.
  - Nunca se registra la clave ni el hash.
- [ ] T019 [US3] En `ms-autenticacion/src/Acity.Api/Program.cs`:
  - `AddOptions<UsuariosInicialesOptions>().Bind(...).Validate(...).ValidateOnStart()`.
  - Tras `Build()` y antes de `Run()`: `await using var scope = app.Services.CreateAsyncScope()`, enviar `CrearUsuariosInicialesCommand` con Administrador y Operador desde las opciones, y cambiar a `await app.RunAsync()`.
  - Un error deja que el `catch` existente registre un `Fatal` y salga con código 1.
- [ ] T020 [US3] Verificar los escenarios 1 a 3 del quickstart sobre `CargaMasivaApp_F008`:
  - 2 usuarios con hash en formato `PasswordHasher` (empieza por `AQAAAA`).
  - Tras 2 reinicios, los hashes no cambian.
  - Con `OPERADOR_CLAVE` cambiada, el hash sigue igual.
  - Sin una variable, no arranca.
  - Registrar los resultados en `quickstart.md`.

## Phase 4: US1 - Iniciar sesión y usar el token en Control (P1) 🎯 MVP

**Objetivo**: login con JWT aceptado por Control y 401 genérico.

**Prueba independiente**: quickstart, escenarios 4 a 7.

- [ ] T021 [P] [US1] Crear `ms-autenticacion/src/Acity.Application/Autenticacion/Commands/IniciarSesion/IniciarSesionCommand.cs` (`record(string Email, string Password) : IRequest<Response<TokensDto>>`) e `IniciarSesionCommandValidator.cs` (`Email` y `Password` `NotEmpty`, sin otras reglas).
- [ ] T022 [US1] Crear `ms-autenticacion/src/Acity.Application/Autenticacion/Commands/IniciarSesion/IniciarSesionCommandHandler.cs` (research D-04):
  - Buscar el usuario por `Email` y el nombre de su rol con `AsNoTracking`, uniendo `Usuario` y `Rol`.
  - Si no existe, o si `Verificar` falla: `Response<TokensDto>.Fail(401, "Credenciales inválidas")` y log Warning "Login rechazado", sin propiedades.
  - Si es correcto:
    - `CrearTokenAcceso(Id, rol)` y `CrearRefreshToken()`.
    - Insertar un `RefreshToken` con `UsuarioId`, `TokenHash`, `FechaCreacion` = ahora UTC, `FechaExpiracion` y `FechaUso` = null.
    - `SaveChangesAsync`, log Information "Login correcto" con `UsuarioId` y `Response.Ok(TokensDto)`.
- [ ] T023 [US1] Crear `ms-autenticacion/src/Acity.Api/Controllers/AuthController.cs` (`[ApiController]`, `[Route("auth")]`, sin `[Authorize]`):
  - `[HttpPost("login")] Login([FromBody] IniciarSesionCommand)` devuelve `StatusCode(r.Code, r)`, igual que en `CargasController`.
  - Los nombres JSON son `email` y `password` (camelCase).
- [ ] T024 [US1] Verificar los escenarios 4 a 7 del quickstart:
  - Decodificar el JWT sin imprimirlo completo.
  - `POST /cargas` en Control debe devolver 202.
  - Los dos 401 deben dar cuerpos idénticos.
  - Los 400 deben venir sin `ProblemDetails`.
  - Registrar los resultados, incluido Abierto 1 (mensajes del modelo), en `quickstart.md`.

## Phase 5: US2 - Renovar el token de acceso (P2)

**Objetivo**: `POST /auth/refresh` con rotación.

**Prueba independiente**: quickstart, escenarios 8 a 12.

- [ ] T025 [P] [US2] Crear `ms-autenticacion/src/Acity.Application/Autenticacion/Commands/RenovarToken/RenovarTokenCommand.cs` (`record(string RefreshToken) : IRequest<Response<TokensDto>>`) y `RenovarTokenCommandValidator.cs` (`RefreshToken` `NotEmpty`).
- [ ] T026 [US2] Crear `ms-autenticacion/src/Acity.Application/Autenticacion/Commands/RenovarToken/RenovarTokenCommandHandler.cs` (research D-03):
  - Calcular `hash = CalcularHash(valor)` y buscar el `RefreshToken` por `TokenHash` con `AsNoTracking`. Si no existe, el `Motivo` es Desconocido.
  - `FechaUso != null` → Usado; `FechaExpiracion <= ahora` → Vencido.
  - Buscar el usuario y su rol; si no existe → UsuarioInexistente.
  - `MarcarUsadoAsync` = false → Concurrente.
  - En cualquier rechazo: `Fail(401, "Refresh token inválido")` y log Warning con `Motivo`.
  - Si todo es correcto: emitir un par nuevo, insertar un `RefreshToken` nuevo, `SaveChangesAsync`, log Information con `UsuarioId` y `Ok(TokensDto)`.
- [ ] T027 [US2] Añadir a `ms-autenticacion/src/Acity.Api/Controllers/AuthController.cs` el endpoint `[HttpPost("refresh")] Refresh([FromBody] RenovarTokenCommand)` → `StatusCode(r.Code, r)`. El JSON es `refreshToken`.
- [ ] T028 [US2] Verificar los escenarios 8 a 12 del quickstart:
  - 8: renovación con 200 y el nuevo token aceptado por Control (202).
  - 9: la reutilización da 401.
  - 10: refresh alterado, token de acceso enviado como refresh y refresh enviado a Control dan 401 en los tres casos.
  - 11: vencimiento con `JWT_VIGENCIA_REFRESH_MINUTOS=1`.
  - 12: revisar el código.
  - Registrar los resultados en `quickstart.md`.

## Phase 6: Polish

- [ ] T029 Escenario 13 del quickstart: buscar con `grep -F` las contraseñas iniciales, los tokens emitidos en la prueba y `JWT_CLAVE` en los logs de ms-autenticacion (volumen `logs-autenticacion` y `docker compose logs`). El resultado esperado es 0 coincidencias.
- [ ] T030 Cierre:
  - Eliminar `CargaMasivaApp_F008`.
  - Arranque normal sobre `CargaMasivaApp`: el runner aplica V005 y `SchemaMigraciones` queda en 5. Requiere las variables `ADMIN_*` y `OPERADOR_*` en `.env`.
  - Comprobar que todos los servicios están `healthy`.
  - Completar "Resultados" en `quickstart.md`, separando lo ejecutado de lo no ejecutado.
  - Entregar el comando de commit.

## Dependencias y orden

- Setup (T001 a T004) → Foundational (T005 a T016) → US3 (T017 a T020) → US1 (T021 a T024) → US2 (T025 a T028) → Polish.
- T005 (V005 aplicado e ingeniería inversa) bloquea T006 y T007.
- T008 y T009 bloquean T011 y T012.
- US1 necesita a US3 para tener usuarios con los que probar.
- US2 necesita el login de US1 para obtener un refresh token.

## Paralelismo

- Setup: T002, T003 y T004 en paralelo; T001 por separado.
- Foundational: T006, T008, T009 y T010 en paralelo; después, T011 y T012 en paralelo.
- US3: T017 en paralelo con T018. US1: T021 antes de T022. US2: T025 antes de T026.

## Estrategia

- **MVP**: US3 + US1. Login funcional con los usuarios iniciales y token aceptado por Control; cumple lo exigido por el README.
- US2 (refresh) se añade después. Es la funcionalidad opcional autorizada por el usuario.
