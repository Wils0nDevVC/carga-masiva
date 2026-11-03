# Data Model: Autenticación con JWT y refresh tokens

**Procedencia**: `F004` = mapeo existente; `Dec Q2` = persistir el refresh token como hash; `Plan` = research D-03 y D-05.

## Usuario (existente, F004; sin cambios de esquema)

| Columna | Tipo | Uso en la 008 |
|---|---|---|
| `Id` | INT IDENTITY PK | claim `sub` |
| `Email` | NVARCHAR(256) UNIQUE | búsqueda en el login; clave de idempotencia de los usuarios iniciales |
| `PasswordHash` | VARCHAR(255) | `PasswordHasher` (formato v3, unos 84 caracteres en Base64); solo se escribe al crear un usuario inicial |
| `RolId` | SMALLINT FK `Rol` | rol del claim `role` |

## Rol (existente, F004; solo lectura)

`Id` SMALLINT; `Nombre` NVARCHAR(50) UNIQUE. La semilla de V001 contiene Administrador (1) y Operador (2). Se busca por `Nombre`.

## RefreshToken (nuevo, V005) [Dec Q2] [Plan]

| Columna | Tipo | Regla |
|---|---|---|
| `Id` | BIGINT IDENTITY PK | — |
| `UsuarioId` | INT NOT NULL, FK `Usuario(Id)` | propietario |
| `TokenHash` | BINARY(32) NOT NULL, UNIQUE `UQ_RefreshToken_TokenHash` | SHA-256 del valor; el valor en claro nunca se guarda |
| `FechaCreacion` | DATETIME2 NOT NULL | UTC |
| `FechaExpiracion` | DATETIME2 NOT NULL | creación + `VigenciaRefreshMinutos`; `CHECK (FechaExpiracion > FechaCreacion)` |
| `FechaUso` | DATETIME2 NULL | NULL = disponible; con fecha = usado (rotado) |

Estados derivados (sin columna de estado):
- **Vigente**: `FechaUso IS NULL` y `FechaExpiracion > ahora`.
- **Usado**: `FechaUso` no es NULL.
- **Vencido**: `FechaExpiracion <= ahora`.

Transición única: Vigente → Usado, mediante un `UPDATE` condicional con `FechaUso IS NULL`. Ningún registro se borra ni se modifica de otra forma.

Entidad de dominio `RefreshToken` con esas propiedades y configuración EF `RefreshTokenConfiguration`, que se obtiene por ingeniería inversa de la tabla una vez aplicada V005. La ingeniería inversa es manual y requiere autorización (constitución §6).
