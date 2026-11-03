-- V005: refresh tokens de ms-autenticacion (Feature 008, decisiones del usuario Q2 y Q3, 2026-11-03).
-- Contrato: specs/008-autenticacion-jwt/contracts/v005-refresh-token.md.
-- Solo se guarda el hash SHA-256 del token (nunca el valor); FechaUso NULL = disponible (rotacion en cada uso).
-- Sin datos ni contrasenas. Lo aplica el runner en su transaccion.
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
