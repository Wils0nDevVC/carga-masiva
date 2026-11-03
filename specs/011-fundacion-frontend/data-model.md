# Data Model: Fundación del frontend React

Sin persistencia propia, salvo la sesión en `sessionStorage`. Los `models` son **contratos TypeScript de la API** [Spec FR-002]; solo se crean los que esta feature usa.

| Modelo (`frontend/src/models/`) | Forma | Fuente |
|---|---|---|
| `Response<T>` | `{ data: T \| null; isSuccess: boolean; message: string \| null; code: number; errors: string[] \| null }` | Constitución §5 |
| `TokensDto` | `{ accessToken: string; expiraEn: string; refreshToken: string; refreshExpiraEn: string }` (fechas ISO UTC) | F008 `contracts/auth-api.md` |
| `LoginRequest` | `{ email: string; password: string }` | F008 |
| `RefreshRequest` | `{ refreshToken: string }` | F008 |
| `ApiError` | `{ code: number; message: string; errors: string[] }` (normalización del cliente; `code` 0 si no hay respuesta) | Spec FR-011 |

Los DTO de consultas (F010) y de subida (F005) **no** se crean aquí: se añaden con sus pantallas (sin archivos sin uso).

## Sesión en `sessionStorage` [Dec Q1]

Clave `carga-masiva.sesion`: el JSON de `TokensDto`. Estados:
- **sin sesión**: no existe la clave;
- **con sesión**: existe la clave.

Transiciones:
- login correcto o renovación → se guarda o se reemplaza;
- cierre de sesión o fallo de la renovación → se elimina.
