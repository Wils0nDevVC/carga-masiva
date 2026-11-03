# Feature Specification: Autenticación con JWT y refresh tokens en ms-autenticacion

**Feature Branch**: `008-autenticacion-jwt` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v2: Q1–Q3 resueltas por el usuario, 2026-11-03)

**Input**: `POST /auth/login` que valida credenciales contra la base existente con `PasswordHasher` y emite un JWT compatible con Control; refresh tokens con `POST /auth/refresh`; creación al arrancar de los usuarios iniciales por rol; alcance mínimo y sin sobreingeniería. Esta etapa genera solo la especificación y la enmienda autorizada de la constitución.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v3.5.0; `[F001]`, `[F004]`, `[F005]` = features anteriores; `[Decisión]` = decisión explícita del usuario (incluye el prompt de esta feature); `[Observado]` = comprobado leyendo el repositorio. **Pendiente** = no decidido; no es requisito.

## Contexto

**Estado observado** [Observado]:
- ms-autenticacion solo expone `GET /health`, el manejador global y `Response<T>`. Tiene mapeadas `Usuario` (`Id`, `Email` único, `PasswordHash`, `RolId`) y `Rol` (`Id`, `Nombre`) desde la F004.
- Los roles Administrador (1) y Operador (2) ya están sembrados por V001. No existe ninguna tabla para refresh tokens.
- En Compose, ms-autenticacion recibe solo la cadena de conexión. No recibe la configuración JWT ni las credenciales iniciales. Esas variables existen en `.env` y ya se pasan a ms-control.
- Control valida HS256 con emisor, audiencia y vigencia, sin tolerancia de reloj. Lee el claim `sub` como identificador y `role` como rol [F005].

**Referencias GitHub** (`referencias/repositorios.md`): ambas son accesibles, consultadas el 2026-11-03 por la API pública de GitHub, solo el árbol de archivos.
- `Clean_architecture_base_net7` no contiene artefactos de autenticación.
- `api_gateway_net7` solo contiene `HeaderToken` y el cliente externo `Happy`. Ambos están excluidos por la constitución §9.
- No se copia ninguna funcionalidad ni dependencia.

**Exigido por el README, decisiones del usuario y pendientes**:

| Tema | README | Decisión del usuario | Pendiente |
|---|---|---|---|
| Login | `/auth/login` valida credenciales contra la fuente de identidad y retorna un JWT Bearer con los claims del usuario [README §2 MS0, §3 paso 0] | Contra la base existente con `PasswordHasher` [Constitución §6] | — |
| Claims y firma | "claims del usuario" | `sub` = `Usuario.Id`; `role` = Administrador u Operador; HS256 con `JWT_CLAVE`, emisor `JWT_EMISOR` y audiencia `JWT_AUDIENCIA` [Constitución §9] [F005] | — |
| Vigencia del token de acceso | — | 60 minutos predeterminados, configurable [Decisión 2026-11-03] | — |
| Credenciales inválidas | — | 401 genérico con `Response<T>`, sin revelar si el usuario existe [Decisión 2026-11-03] | — |
| Refresh tokens | Opcional valorado; `/auth/refresh` si se implementa [README §2 MS0, §4] | Se incluyen: el login entrega un refresh token y `POST /auth/refresh` renueva el token de acceso. Q1: vigencia de 1 hora configurable; Q2: persistido en la base solo como hash; Q3: rotación en cada renovación [Decisión 2026-11-03] | — |
| Usuarios iniciales | — | Uno por rol, con credenciales externas, creados al arrancar por ms-autenticacion, sin duplicar ni sobrescribir contraseñas [Constitución §6] | Nombres de las variables (se fijan en el plan) |
| Logs | Logging estructurado [README §4] | Sin contraseñas ni tokens [Constitución §10] | — |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Iniciar sesión y usar el token en Control (Priority: P1)

Un usuario envía su correo y su contraseña a `/auth/login`. Si son correctos, recibe un token de acceso que Control acepta para subir cargas según su rol. [README §2 MS0, §3 paso 0] [Constitución §6, §9]

**Why this priority**: Sin token no se puede usar Control ni el Gateway. Es el primer paso del flujo del README.

**Independent Test**:
1. Iniciar sesión con el usuario inicial Operador.
2. Decodificar el token y revisar sus claims.
3. Usarlo en `POST` de carga de Control: debe responder 202.
4. Repetir con credenciales erróneas: debe responder 401.

**Acceptance Scenarios**:

1. **Given** un usuario existente con contraseña correcta, **When** llama a `POST /auth/login`, **Then**:
   - Recibe 200 con `Response<T>` exitoso.
   - La respuesta contiene el token de acceso, su fecha de expiración y un refresh token.
2. **Given** el token de acceso emitido, **When** se inspecciona, **Then**:
   - `sub` es el `Id` numérico del usuario y `role` es su rol (Administrador u Operador).
   - El emisor y la audiencia son los configurados, la firma es HS256 con `JWT_CLAVE` y la expiración es 60 minutos después de la emisión (o el valor configurado).
3. **Given** ese token, **When** se usa en el endpoint de carga de Control, **Then** Control lo acepta y aplica el rol: Operador y Administrador pueden cargar [F005].
4. **Given** un correo inexistente, o un correo existente con contraseña errónea, **When** llama a `/auth/login`, **Then**:
   - Recibe 401 con `Response<T>` (`isSuccess` false, `data` null).
   - El mensaje es idéntico en ambos casos y no revela si el usuario existe.
5. **Given** una solicitud sin correo o sin contraseña, o con el cuerpo vacío, **When** llama a `/auth/login`, **Then** recibe 400 con el formato de error común, sin consultar credenciales.

---

### User Story 2 - Renovar el token de acceso (Priority: P2)

Con el refresh token obtenido en el login, el cliente pide un nuevo token de acceso sin volver a enviar la contraseña. [README §2 MS0 (opcional)] [Decisión 2026-11-03]

**Why this priority**: Es la funcionalidad opcional autorizada. El flujo del README funciona sin ella.

**Independent Test**:
1. Iniciar sesión.
2. Llamar a `POST /auth/refresh` con el refresh token.
3. Usar el nuevo token de acceso en Control.
4. Repetir con un refresh token alterado, vencido o ya usado.

**Acceptance Scenarios**:

1. **Given** un refresh token válido y vigente, **When** se llama a `POST /auth/refresh`, **Then**:
   - Se recibe 200 con un nuevo token de acceso de las mismas características que el del login (claims, firma, emisor, audiencia y vigencia).
   - Se recibe también un nuevo refresh token con vigencia completa (1 hora o el valor configurado), y el usado queda invalidado (Q3).
2. **Given** un refresh token alterado, mal formado o desconocido, **When** se llama a `/auth/refresh`, **Then** se recibe 401 genérico con `Response<T>`.
3. **Given** un refresh token emitido hace más de 1 hora (o la vigencia configurada), **When** se llama a `/auth/refresh`, **Then** se recibe 401 genérico.
4. **Given** un refresh token ya usado en una renovación, **When** se vuelve a usar, **Then** se recibe 401 genérico, aunque siga vigente (Q3).
5. **Given** un refresh token, **When** se presenta a Control o al Gateway como token de acceso, **Then** se rechaza (401): un refresh token no sirve como token de acceso.
6. **Given** un token de acceso, **When** se envía a `/auth/refresh` como refresh token, **Then** se recibe 401.
7. **Given** un refresh token emitido a un usuario que ya no existe en la base, **When** se renueva, **Then** se recibe 401.

---

### User Story 3 - Disponer de un usuario inicial por rol (Priority: P1)

Al arrancar, ms-autenticacion garantiza que existan un Administrador y un Operador para la demostración, con credenciales tomadas de la configuración externa. [Constitución §6]

**Why this priority**: Sin usuarios no se puede iniciar sesión. Los scripts no pueden contener contraseñas.

**Independent Test**:
1. Arrancar el servicio con las credenciales configuradas sobre una base sin usuarios: deben existir dos usuarios, uno por rol.
2. Reiniciar el servicio: sigue habiendo dos usuarios y sus hashes no cambian.
3. Cambiar la contraseña configurada y reiniciar: el hash persistido no cambia y el login solo funciona con la contraseña original.

**Acceptance Scenarios**:

1. **Given** una base sin el correo configurado para un rol, **When** el servicio arranca, **Then** se crea ese usuario con su rol y la contraseña con hash de `PasswordHasher`, nunca en texto plano.
2. **Given** el usuario ya existe (mismo correo), **When** el servicio arranca de nuevo, **Then** no se crea otro usuario ni se modifica su contraseña o su rol.
3. **Given** la contraseña configurada cambió, **When** el servicio arranca, **Then** el hash existente se conserva (no se sobrescribe).
4. **Given** falta la configuración de credenciales iniciales (correo o contraseña de algún rol), **When** el servicio arranca, **Then** no arranca y lo informa en el log sin mostrar valores secretos.

---

### Edge Cases

- **Correo con distinta capitalización**: se compara según la intercalación de la base. La predeterminada de SQL Server no distingue mayúsculas, así que `Operador@x` y `operador@x` son el mismo usuario. No se normaliza el correo más allá de eso.
- **Correo inicial existente con otro rol**: no se modifica; se registra una advertencia en el log sin datos secretos.
- **Correo configurado que cambia entre arranques**: se crea el usuario nuevo y el anterior se conserva. No hay administración de usuarios para borrarlo.
- **Base no disponible durante el login, el refresh o el arranque**: error técnico con el contrato común (no 401). Si ocurre en el arranque, el servicio no se declara listo.
- **Token de acceso vencido presentado a Control**: Control responde 401 (comportamiento existente de la F005). El cliente debe renovar o volver a iniciar sesión.
- **Clave JWT ausente o de menos de 32 bytes, o sin emisor o audiencia**: el servicio no arranca, igual que Control.
- **Vigencia configurada no válida** (cero, negativa o no numérica): el servicio no arranca.
- **Usuario borrado entre el login y el refresh**: 401 en el refresh. El token de acceso ya emitido sigue siendo válido hasta su expiración, porque Control no consulta la base para validarlo.
- **Rol del usuario cambiado entre el login y el refresh**: el nuevo token de acceso lleva el rol actual de la base.

## Requirements *(mandatory)*

### Functional Requirements

**Login**
- **FR-001**: El servicio MUST exponer `POST /auth/login`, que recibe correo y contraseña. [README §2 MS0]
- **FR-002**: MUST buscar el usuario por correo en la base existente y verificar la contraseña con `PasswordHasher` de ASP.NET Core Identity, sin implementar el algoritmo manualmente. [Constitución §6]
- **FR-003**: Con credenciales correctas, MUST responder 200 con `Response<T>`. La respuesta MUST contener el token de acceso, su expiración (UTC) y el refresh token. Los nombres exactos de los campos se fijan en el plan. [Decisión]
- **FR-004**: Con correo inexistente o contraseña errónea, MUST responder 401 con `Response<T>` (`data` null) y el mismo mensaje en ambos casos. MUST NOT revelar si el usuario existe. [Decisión 2026-11-03] [Constitución §5]
- **FR-005**: Una solicitud sin correo, sin contraseña o sin cuerpo MUST responder 400 con el formato de error común. [Constitución §5]

**Token de acceso**
- **FR-006**: El token de acceso MUST ser un JWT firmado con HS256 y la clave de `JWT_CLAVE` (al menos 32 bytes). MUST llevar:
  - el emisor de `JWT_EMISOR` y la audiencia de `JWT_AUDIENCIA`;
  - `sub` = `Usuario.Id` en texto y `role` = nombre del rol (Administrador u Operador);
  - la fecha de expiración.
  [Constitución §9] [F005]
- **FR-007**: La vigencia del token de acceso MUST ser de 60 minutos por defecto, configurable por variable de entorno. [Decisión 2026-11-03]
- **FR-008**: Un token de acceso emitido por este servicio MUST ser aceptado por Control sin cambiar la configuración de Control. [F005]
- **FR-009**: Los claims mínimos son `sub` y `role`. No se añade el correo como claim obligatorio. Cualquier otro claim se limita a los estándar de emisión (emisor, audiencia, expiración, emisión). [Constitución §9]

**Refresh token**
- **FR-010**: El login MUST entregar un refresh token, y `POST /auth/refresh` MUST recibirlo y devolver un nuevo token de acceso según FR-006 y FR-007, con el rol actual del usuario en la base. [README §2 MS0] [Decisión 2026-11-03]
- **FR-011**: `/auth/refresh` MUST responder 401 genérico con `Response<T>` cuando el refresh token sea inválido, alterado, desconocido, vencido, del usuario inexistente o ya usado. [Decisión]
- **FR-012**: Un refresh token MUST NOT ser aceptado como token de acceso por Control ni por el Gateway, y un token de acceso MUST NOT ser aceptado por `/auth/refresh`. [Decisión: compatibilidad con Control sin debilitar su validación]
- **FR-013**: El refresh token MUST tener una vigencia de 1 hora por defecto, configurable por variable de entorno. Vencido → 401. [Decisión Q1, 2026-11-03]
- **FR-014**: El refresh token MUST persistirse en la base solo como hash (nunca el valor en claro), asociado al usuario, con su expiración y su estado de uso. La tabla se crea con un nuevo script versionado aplicado por el runner de `database/`; ningún servicio crea esquema al arrancar. [Decisión Q2, 2026-11-03] [Constitución §6]
- **FR-015**: Cada renovación exitosa MUST invalidar el refresh token usado y entregar uno nuevo (rotación). Reusar uno invalidado → 401. Sin detección de familias ni revocación en cadena. [Decisión Q3, 2026-11-03]

**Usuarios iniciales**
- **FR-016**: Al arrancar, el servicio MUST asegurar un usuario por rol (Administrador y Operador), con el correo y la contraseña tomados de variables de entorno. Los nombres de las variables se fijan en el plan. [Constitución §6]
- **FR-017**: La creación MUST ser repetible: si el correo ya existe, MUST NOT crear otro usuario ni modificar su contraseña o su rol. [Constitución §6]
- **FR-018**: La contraseña inicial MUST guardarse solo como hash generado por `PasswordHasher`. MUST NOT aparecer en scripts, migraciones, archivos versionados ni logs. [Constitución §6, §10]
- **FR-019**: Si falta la configuración de credenciales iniciales o la configuración JWT es inválida, el servicio MUST NOT arrancar y MUST informarlo sin mostrar valores secretos. [Decisión: mismo criterio que Control con `Jwt`]

**Transversales**
- **FR-020**: Los logs MUST NOT contener contraseñas, hashes, tokens de acceso, refresh tokens ni la clave JWT. Pueden registrar el resultado del login o el refresh y el `Usuario.Id` cuando se conoce. [Constitución §10]
- **FR-021**: Los errores MUST usar `Response<T>` y el manejador global existentes. `Code` coincide con el estado HTTP y no hay un segundo formato de error. [Constitución §5]
- **FR-022**: Compose MUST pasar a ms-autenticacion las variables JWT existentes (`JWT_CLAVE`, `JWT_EMISOR`, `JWT_AUDIENCIA`), la vigencia y las credenciales iniciales desde `.env`. `.env.example` las documenta sin valores secretos. [Constitución §11]

**Fuera de alcance** [Decisión]:
- Registro, administración o borrado de usuarios; cambio o recuperación de contraseña; MFA.
- Proveedores externos; logout; administración o listado de sesiones.
- El framework completo de ASP.NET Core Identity (solo su `PasswordHasher`).
- Rate Limiting en `/auth/login`: la constitución §9 lo ubica en Control y no se duplica.
- Validación del JWT en el Gateway (feature 009); cambios en Control.
- Rehash automático de contraseñas al iniciar sesión; igualar el tiempo de respuesta entre usuario inexistente y contraseña errónea.

### Key Entities *(include if feature involves data)*

- **Usuario** (existente, F004): `Id`, `Email` (único), `PasswordHash`, `RolId`. Se lee en el login y en el refresh, y se inserta solo para los usuarios iniciales.
- **Rol** (existente, F004): Administrador (1) y Operador (2). Solo lectura; aporta el valor del claim `role`.
- **Refresh token**: credencial de renovación asociada a un usuario, con vigencia limitada. Se persiste en la base con el hash del token, el usuario, la emisión, la expiración y su estado de uso (Q2). Requiere un nuevo script versionado (V005) aplicado por el runner; su definición exacta se fija en el plan. Sin limpieza automática de registros vencidos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En 2 de 2 intentos (uno por usuario inicial), el login con credenciales correctas devuelve un token aceptado por Control en el endpoint de carga, sin cambiar la configuración de Control.
- **SC-002**: El 100 % de los tokens de acceso inspeccionados tienen `sub` numérico igual al `Id` del usuario, `role` igual a su rol, y el emisor, la audiencia y el algoritmo esperados. Su expiración es la emisión más 60 minutos (o el valor configurado), con una tolerancia de 1 minuto.
- **SC-003**: Los intentos con correo inexistente y con contraseña errónea devuelven respuestas indistinguibles en estado, cuerpo y mensaje: 401 y `data` null.
- **SC-004**: Un refresh token válido produce un nuevo token aceptado por Control y un nuevo refresh token. Los refresh tokens alterados, vencidos, de usuario inexistente o ya usados se rechazan con 401 en el 100 % de los casos; un refresh token usado una vez se rechaza en su segundo uso.
- **SC-005**: Un refresh token presentado a Control se rechaza con 401, y un token de acceso presentado a `/auth/refresh` también.
- **SC-006**: Tras 3 arranques consecutivos con la misma configuración hay exactamente 1 usuario por correo configurado, y los hashes no cambian. Tras cambiar la contraseña configurada, el hash persistido sigue igual.
- **SC-007**: La búsqueda de las contraseñas configuradas, los tokens emitidos durante la prueba y la clave JWT en los logs del servicio da 0 coincidencias.

## Assumptions

- La identidad del usuario es su correo (`Usuario.Email`), que es el campo único existente. No se añaden columnas a `Usuario` ni a `Rol`.
- No hay usuarios deshabilitados ni bloqueo por intentos fallidos, porque el esquema no lo contempla y no se solicitó.
- Los relojes del contenedor y de Control son coherentes. Control valida sin tolerancia de reloj [F005].
- Las fechas se emiten en UTC.
- Solo los usuarios iniciales se crean en esta feature. Los usuarios de prueba de otras features siguen creándose en bases temporales.
- Se trabaja con una sola instancia de ms-autenticacion. Si la inserción de un usuario inicial choca por correo duplicado, se trata como "ya existe".
- Postman es el cliente de verificación. No hay tests automatizados [Constitución §11].

## Decisiones del usuario (2026-11-03)

| # | Pregunta | Decisión | Requisitos |
|---|---|---|---|
| Q1 | Vigencia del refresh token | 1 hora, configurable (reto técnico) | FR-013, US2-3, SC-004 |
| Q2 | Persistencia | En la base, solo el hash del token; tabla nueva por script V005 del runner | FR-014, Key Entities, US2-4 |
| Q3 | Rotación | Cada renovación invalida el usado y entrega uno nuevo; reusar → 401; sin detección de familias | FR-015, US2-1, US2-4, SC-004 |

Con Q1 y Q3, una sesión sin renovar caduca a la hora; si el cliente renueva antes de que venza el refresh token, sigue sin volver a enviar la contraseña.

## Asuntos pendientes (no son requisitos aprobados)

Se fijan en el plan, sin bloquear: nombres de las variables de entorno, nombres de los campos de la respuesta, formato del refresh token y definición de la tabla del script V005.
