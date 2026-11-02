-- Bootstrap del runner (idempotente): crea la base y el registro de scripts aplicados.
-- Variable sqlcmd: DB_NAME (validada por el runner: solo letras, digitos y guion bajo).
SET NOCOUNT ON;
IF DB_ID(N'$(DB_NAME)') IS NULL
    CREATE DATABASE [$(DB_NAME)];
GO
USE [$(DB_NAME)];
GO
IF OBJECT_ID(N'dbo.SchemaMigraciones', N'U') IS NULL
    CREATE TABLE dbo.SchemaMigraciones (
        Script        NVARCHAR(200) NOT NULL CONSTRAINT PK_SchemaMigraciones PRIMARY KEY,
        Checksum      CHAR(64)      NOT NULL,
        FechaAplicado DATETIME2     NOT NULL
    );
GO
