# Contrato HTTP: ms-autenticacion

Base: `http://localhost:8081` (Compose). JSON en camelCase. Todas las respuestas usan `Response<T>` (`data`, `isSuccess`, `message`, `code`, `errors`), con `code` igual al estado HTTP. Ningún endpoint requiere token.

## POST /auth/login

Solicitud:
```json
{ "email": "operador@ejemplo.test", "password": "••••" }
```

200 (`data`):
```json
{
  "accessToken": "<JWT>",
  "expiraEn": "2026-11-03T18:00:00Z",
  "refreshToken": "<43 caracteres Base64Url>",
  "refreshExpiraEn": "2026-11-03T18:00:00Z"
}
```

| Caso | Estado | `message` | `data` |
|---|---|---|---|
| Correcto | 200 | `OK` | tokens |
| Correo inexistente o contraseña errónea | 401 | `Credenciales inválidas` (idéntico en ambos casos) | null |
| Falta `email` o `password`, cuerpo vacío o JSON inválido | 400 | `Error de validación` (con `errors`) | null |
| Base no disponible u otro error técnico | 500 | `Error interno del servidor` | null |

## POST /auth/refresh

Solicitud:
```json
{ "refreshToken": "<valor recibido>" }
```

Respuesta 200: igual que en el login. Devuelve un token de acceso nuevo y un refresh token nuevo; el refresh token enviado queda usado.

| Caso | Estado | `message` |
|---|---|---|
| Correcto | 200 | `OK` |
| Desconocido, alterado, token de acceso enviado como refresh, usado, vencido, usuario inexistente o uso concurrente | 401 | `Refresh token inválido` (idéntico en todos los casos) |
| Falta `refreshToken` o el cuerpo es inválido | 400 | `Error de validación` |

## Token de acceso (JWT) [Constitución §9] [F005]

| Elemento | Valor |
|---|---|
| `alg` | HS256 (clave `JWT_CLAVE`, al menos 32 bytes) |
| `iss` / `aud` | `JWT_EMISOR` / `JWT_AUDIENCIA` |
| `sub` | `Usuario.Id` en texto (`"1"`) |
| `role` | `Administrador` u `Operador` |
| `iat`, `nbf`, `exp` | emisión; `exp` = emisión + `JWT_VIGENCIA_ACCESO_MINUTOS` (60) |

Control lo valida sin cambios. El refresh token no es un JWT, por lo que Control lo rechaza (401).
