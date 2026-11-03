# Contrato de esquema: V005 `RefreshToken`

**Archivo**: `database/scripts/V005__refresh_token.sql`. Lo aplica el runner en su transacción (`XACT_ABORT`) y lo registra en `SchemaMigraciones` (que pasa de 4 a 5 registros). Lo autorizan la decisión Q2 del usuario (2026-11-03) y la constitución §6 (esquema solo por scripts versionados). No contiene contraseñas ni datos.

```sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
CREATE TABLE dbo.RefreshToken (
    Id              BIGINT     NOT NULL IDENTITY(1,1) CONSTRAINT PK_RefreshToken PRIMARY KEY,
    UsuarioId       INT        NOT NULL CONSTRAINT FK_RefreshToken_Usuario REFERENCES dbo.Usuario (Id),
    TokenHash       BINARY(32) NOT NULL CONSTRAINT UQ_RefreshToken_TokenHash UNIQUE,
    FechaCreacion   DATETIME2  NOT NULL,
    FechaExpiracion DATETIME2  NOT NULL,
    FechaUso        DATETIME2  NULL,
    CONSTRAINT CK_RefreshToken_Fechas CHECK (FechaExpiracion > FechaCreacion)
);
GO
```

- No se crean procedimientos, índices adicionales ni limpieza programada.
- Las bases existentes (`CargaMasivaApp`) reciben V005 en el siguiente arranque del runner. `CargaMasiva` y `CargaMasivaDb` no se tocan.
