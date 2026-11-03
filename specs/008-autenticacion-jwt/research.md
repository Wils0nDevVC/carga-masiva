# Research: Autenticación con JWT y refresh tokens en ms-autenticacion

**Procedencia**: `Dec` = decisión del usuario o constitución v3.5.0; `Spec` = spec 008; `F004/F005` = features anteriores; `Plan` = decisión técnica de este plan (alternativa más sencilla; sin aprobación individual); `Verif` = comprobado en esta etapa; `Sin verif` = se comprueba en la implementación.

## D-01 Paquetes

| Paquete | Versión | Proyecto | Licencia | Comprobación |
|---|---|---|---|---|
| `Microsoft.IdentityModel.JsonWebTokens` | 8.19.2 | Infrastructure | MIT | Verif: es la misma versión que usa Control (transitiva de JwtBearer 10.0.14 en `project.assets.json`) y se publicó el 2026-07-14. Hoy ms-autenticacion la recibe transitivamente en 7.7.1 (vía SqlClient); la referencia explícita la sube a la versión de Control |
| `FrameworkReference Microsoft.AspNetCore.App` | .NET 10 | Infrastructure | MIT | Verif: el framework compartido incluye `Microsoft.Extensions.Identity.Core.dll`, que contiene `PasswordHasher<TUser>`. No se añade el paquete de Identity ni sus stores. Es el mismo patrón que en las features 005 a 007 |

No se añaden JwtBearer (este servicio emite tokens, no los valida), Polly, RabbitMQ ni ningún otro paquete. [Dec §12] [Plan]

## D-02 Token de acceso

**Decisión**:
- Se emite con `JsonWebTokenHandler.CreateToken(SecurityTokenDescriptor)`:
  - `Issuer = Jwt:Emisor`, `Audience = Jwt:Audiencia`.
  - `IssuedAt` y `NotBefore` = ahora (UTC); `Expires` = ahora + `Jwt:VigenciaAccesoMinutos` (60 por defecto).
  - Claims `sub` = `Usuario.Id.ToString()` y `role` = `Rol.Nombre`.
  - Firma `HmacSha256` con `Jwt:Clave` (UTF-8, al menos 32 bytes).
- `JwtOptions` replica la de Control (`Clave`, `Emisor`, `Audiencia`, `EsValida`) y añade `VigenciaAccesoMinutos` y `VigenciaRefreshMinutos` (> 0). Se valida con `ValidateOnStart`. [Spec FR-006, FR-007, FR-019] [F005]

**Por qué**:
- Es el mismo formato que el generador de tokens de prueba de la F005, que Control aceptó.
- Control usa `MapInboundClaims = false`, `NameClaimType = "sub"`, `RoleClaimType = "role"`, `ValidAlgorithms = [HS256]` y `ClockSkew = 0`. Un `sub` numérico y un `role` literal le bastan. [F005 Verif]

**Alternativas descartadas**:
- `JwtSecurityTokenHandler`: es el manejador heredado.
- Añadir `email` o `jti` como claim: no se solicitó (§9).

## D-03 Refresh token (Q1–Q3)

**Decisión**:
- **Formato.** Valor opaco aleatorio de 32 bytes (`RandomNumberGenerator`), en Base64Url (43 caracteres). No es un JWT. [Plan]
- **Almacenamiento.** En la base se guarda solo el `SHA256` del valor (`BINARY(32)`), con `UsuarioId`, `FechaCreacion`, `FechaExpiracion` = creación + `Jwt:VigenciaRefreshMinutos` (60 por defecto) y `FechaUso` (NULL hasta usarse). [Dec Q1, Q2]
- **Hash.** SHA-256 y no `PasswordHasher`, porque el valor tiene 256 bits de entropía y no necesita estiramiento. Además permite buscarlo por igualdad y un índice único. [Plan]
- **Rotación**, en `/auth/refresh` [Dec Q3]:
  1. Calcular el hash del valor recibido y buscar su registro.
  2. Si no existe, si ya se usó (`FechaUso` no NULL) o si venció (`FechaExpiracion <= ahora`), responder 401.
  3. Leer el usuario y su rol. Si el usuario no existe, responder 401.
  4. Marcarlo como usado con un `UPDATE` condicional (`ExecuteUpdateAsync … WHERE Id = @id AND FechaUso IS NULL`). Si afecta 0 filas, otra solicitud lo usó a la vez: responder 401.
  5. Emitir un nuevo token de acceso y un nuevo refresh token, insertar el nuevo registro y responder 200.

**Cumplimiento de FR-012 sin código adicional**:
- Control no puede leer el refresh token como JWT, así que lo rechaza con 401.
- Un token de acceso enviado a `/auth/refresh` no coincide con ningún hash guardado, así que da 401.

**Limitaciones aceptadas** [Plan]:
- Si la inserción del nuevo registro falla después de marcar el anterior como usado, se responde 500 y el cliente debe volver a iniciar sesión. No se usa una transacción explícita.
- Los registros vencidos o usados se conservan; no se limpian automáticamente.
- No hay detección de reutilización por familias (Dec Q3).

**Alternativas descartadas**:
- Refresh token como JWT: Control lo aceptaría como token de acceso, salvo que se cambiara su audiencia.
- Guardar el valor en claro (Dec Q2).
- Procedimiento almacenado: el `UPDATE` condicional de EF ya es atómico.

## D-04 Login

1. El validador exige `email` y `password` no vacíos; si falta alguno, 400 por `ValidationBehavior`. No se infieren longitudes ni formato. [Spec FR-005] [Dec §4]
2. Buscar el usuario por `Email` (igualdad; la intercalación de la base decide la capitalización) junto con el nombre de su rol.
3. Si no existe → 401 con el mensaje "Credenciales inválidas".
4. Si existe → `PasswordHasher<Usuario>.VerifyHashedPassword(usuario, hash, password)`:
   - `Failed` → 401 con el mismo mensaje.
   - `Success` o `SuccessRehashNeeded` → correcto. No se regraba el hash.
5. Emitir el token de acceso (D-02) y el refresh token (D-03), insertarlo, guardar y responder 200. [Spec FR-002 a FR-004]

**Cuerpo inválido**: un JSON mal formado o un cuerpo vacío normalmente generaría un `ProblemDetails` automático de `[ApiController]`. Para evitarlo, `ConfigureApiBehaviorOptions` responde con `Response<object>.Fail(400, "Error de validación", errores)`; así se mantiene un solo formato de error (constitución §5).
- La F005 tuvo el mismo problema con formularios, resuelto allí con otro mecanismo.
- *Sin verif*: los mensajes exactos que produce el modelo.

## D-05 Usuarios iniciales

- **Momento.** En `Program.cs`, después de `Build()` y antes de `Run()`: se abre un ámbito de DI y se envía `CrearUsuariosInicialesCommand` con las dos credenciales. Si lanza una excepción, el bloque `catch` existente registra un `Fatal` y el proceso termina con código 1, así que Compose ve que el servicio no arrancó. [Spec FR-016, FR-019]
- **Por cada rol** (Administrador y Operador, buscados por `Rol.Nombre`):
  - Si el correo ya existe, no se toca. Si su rol es otro, se registra una advertencia.
  - Si no existe, se inserta con `PasswordHasher.HashPassword` y se guarda.
  - Si la inserción choca con `UQ_Usuario_Email`, se trata como "ya existe".
  - [Spec FR-017, FR-018]
- **Configuración** (`UsuariosInicialesOptions` en Api, con `ValidateOnStart`): `Administrador:Email`, `Administrador:Clave`, `Operador:Email` y `Operador:Clave`, todos no vacíos. No se exige longitud mínima de contraseña. [Plan]
- **Sin reintentos.** Compose arranca este servicio cuando el runner terminó, así que SQL Server ya está disponible.

## D-06 Configuración y Compose

| Opción | Variable del contenedor | Origen en `.env` | Predeterminado |
|---|---|---|---|
| `Jwt.Clave` | `Jwt__Clave` | `JWT_CLAVE` (obligatoria, `:?`) | — |
| `Jwt.Emisor` | `Jwt__Emisor` | `JWT_EMISOR` | `ms-autenticacion` |
| `Jwt.Audiencia` | `Jwt__Audiencia` | `JWT_AUDIENCIA` | `carga-masiva` |
| `Jwt.VigenciaAccesoMinutos` | `Jwt__VigenciaAccesoMinutos` | `JWT_VIGENCIA_ACCESO_MINUTOS` | 60 |
| `Jwt.VigenciaRefreshMinutos` | `Jwt__VigenciaRefreshMinutos` | `JWT_VIGENCIA_REFRESH_MINUTOS` | 60 |
| `UsuariosIniciales.Administrador.Email` / `.Clave` | `UsuariosIniciales__Administrador__Email` / `__Clave` | `ADMIN_EMAIL` / `ADMIN_CLAVE` (obligatorias, `:?`) | — |
| `UsuariosIniciales.Operador.Email` / `.Clave` | `UsuariosIniciales__Operador__Email` / `__Clave` | `OPERADOR_EMAIL` / `OPERADOR_CLAVE` (obligatorias, `:?`) | — |

- `.env.example` documenta las variables nuevas sin valores secretos.
- Los valores reales de `.env` (contraseñas iniciales) los define el usuario. El agente no los escribe sin autorización expresa (lección de la 007).
- Control no cambia. [Spec FR-022]

## D-07 Logs

| Evento | Nivel | Propiedades |
|---|---|---|
| Login correcto | Information | `UsuarioId` |
| Login rechazado | Warning | ninguna (sin correo ni contraseña) |
| Refresh correcto | Information | `UsuarioId` |
| Refresh rechazado | Warning | `Motivo` (Desconocido, Usado, Vencido, UsuarioInexistente o Concurrente) |
| Usuario inicial | Information o Warning | creado o existente (con `Rol` y `UsuarioId`); advertencia si el rol difiere |

- Nunca se registran contraseñas, hashes, tokens ni la clave.
- EF Core ya tiene `Database.Command` en `Warning` y sin `EnableSensitiveDataLogging`, así que los parámetros SQL no se registran. [Spec FR-020] [Dec §10]

## Abiertos

1. Mensajes de error del modelo para un JSON inválido (D-04): sin verificar.
2. Validación en el equipo de evaluación: no realizada.
