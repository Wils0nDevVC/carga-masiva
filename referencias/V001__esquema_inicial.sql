-- V001: esquema inicial de carga-masiva (feature 001).
-- Lo aplica el runner (database/run-scripts.sh) dentro de una transaccion con XACT_ABORT.
-- SchemaMigraciones la crea el runner (database/bootstrap.sql), no este script.
-- Sin contrasenas ni usuarios: los usuarios de demostracion los crea ms-autenticacion.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------------------------------------------------------------- US8: identidad
CREATE TABLE dbo.Rol (
    Id     SMALLINT     NOT NULL CONSTRAINT PK_Rol PRIMARY KEY,
    Nombre NVARCHAR(50) NOT NULL CONSTRAINT UQ_Rol_Nombre UNIQUE
);
GO

-- Roles de referencia (sin secretos)
INSERT INTO dbo.Rol (Id, Nombre) VALUES (1, N'Administrador'), (2, N'Operador');
GO

CREATE TABLE dbo.Usuario (
    Id           INT           NOT NULL IDENTITY(1,1) CONSTRAINT PK_Usuario PRIMARY KEY,
    Email        NVARCHAR(256) NOT NULL CONSTRAINT UQ_Usuario_Email UNIQUE,
    PasswordHash VARCHAR(255)  NOT NULL,
    RolId        SMALLINT      NOT NULL CONSTRAINT FK_Usuario_Rol REFERENCES dbo.Rol (Id)
);
GO

-- ---------------------------------------------------------------- US1: carga
CREATE TABLE dbo.CargaArchivo (
    Id             BIGINT        NOT NULL IDENTITY(1,1) CONSTRAINT PK_CargaArchivo PRIMARY KEY,
    NombreArchivo  NVARCHAR(260) NOT NULL,
    UsuarioId      INT           NOT NULL CONSTRAINT FK_CargaArchivo_Usuario REFERENCES dbo.Usuario (Id),
    FechaRecepcion DATETIME2     NOT NULL,
    RutaArchivo    NVARCHAR(500) NOT NULL,
    Estado         VARCHAR(20)   NOT NULL
        CONSTRAINT CK_CargaArchivo_Estado CHECK (Estado IN ('Pendiente','En proceso','Cargado','Finalizado','Notificado','Fallido')),
    FechaFin       DATETIME2     NULL,
    IntentosProceso INT          NOT NULL CONSTRAINT DF_CargaArchivo_IntentosProceso DEFAULT 0
);
GO

CREATE INDEX IX_CargaArchivo_Usuario_FechaRecepcion ON dbo.CargaArchivo (UsuarioId, FechaRecepcion);
GO

-- ---------------------------------------------------------------- US2: periodos
CREATE TABLE dbo.CargaPeriodo (
    Id               BIGINT      NOT NULL IDENTITY(1,1) CONSTRAINT PK_CargaPeriodo PRIMARY KEY,
    CargaId          BIGINT      NOT NULL CONSTRAINT FK_CargaPeriodo_Carga REFERENCES dbo.CargaArchivo (Id),
    Periodo          CHAR(7)     NOT NULL
        CONSTRAINT CK_CargaPeriodo_Periodo CHECK (Periodo LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9]' AND SUBSTRING(Periodo, 6, 2) BETWEEN '01' AND '12'),
    Resultado        VARCHAR(20) NOT NULL
        CONSTRAINT CK_CargaPeriodo_Resultado CHECK (Resultado IN ('Admitido','Completado','Rechazado','Bloqueado','Fallido','NoProcesado')),
    PeriodoReservado CHAR(7)     NULL,
    FechaResultado   DATETIME2   NOT NULL,
    CONSTRAINT UQ_CargaPeriodo_Carga_Periodo UNIQUE (CargaId, Periodo),
    CONSTRAINT CK_CargaPeriodo_Reserva CHECK (
        (Resultado IN ('Admitido','Completado') AND PeriodoReservado IS NOT NULL AND PeriodoReservado = Periodo)
        OR (Resultado NOT IN ('Admitido','Completado') AND PeriodoReservado IS NULL))
);
GO

-- Reserva: solo Admitido y Completado tienen valor. Filtro con comparacion simple (IS NOT NULL).
CREATE UNIQUE NONCLUSTERED INDEX UX_CargaPeriodo_PeriodoReservado
    ON dbo.CargaPeriodo (PeriodoReservado)
    WHERE PeriodoReservado IS NOT NULL;
GO

-- ---------------------------------------------------------------- US3: productos
CREATE TABLE dbo.Producto (
    Id             BIGINT        NOT NULL IDENTITY(1,1) CONSTRAINT PK_Producto PRIMARY KEY,
    CodigoProducto NVARCHAR(100) COLLATE Latin1_General_100_CI_AS_WS NOT NULL,
    NombreProducto NVARCHAR(200) NOT NULL,
    Precio         DECIMAL(18,2) NOT NULL,
    CargaPeriodoId BIGINT        NOT NULL CONSTRAINT FK_Producto_CargaPeriodo REFERENCES dbo.CargaPeriodo (Id)
);
GO

-- Unicidad global, sin llave compuesta con el periodo.
CREATE UNIQUE NONCLUSTERED INDEX UX_Producto_CodigoProducto ON dbo.Producto (CodigoProducto);
GO

-- ---------------------------------------------------------------- US6: incidencias e historial
CREATE TABLE dbo.Incidencia (
    Id             BIGINT        NOT NULL IDENTITY(1,1) CONSTRAINT PK_Incidencia PRIMARY KEY,
    CargaId        BIGINT        NOT NULL CONSTRAINT FK_Incidencia_Carga REFERENCES dbo.CargaArchivo (Id),
    CargaPeriodoId BIGINT        NULL CONSTRAINT FK_Incidencia_CargaPeriodo REFERENCES dbo.CargaPeriodo (Id),
    Tipo           VARCHAR(30)   NOT NULL
        CONSTRAINT CK_Incidencia_Tipo CHECK (Tipo IN ('PeriodoRechazado','PeriodoBloqueado','FilaOmitida','Existente','NoProcesado','ErrorTecnico','ErrorNotificacion')),
    NumeroFila     INT           NULL,
    CodigoProducto NVARCHAR(100) NULL,
    PeriodoTexto   NVARCHAR(50)  NULL,
    Motivo         NVARCHAR(500) NOT NULL,
    FechaRegistro  DATETIME2     NOT NULL
);
GO

CREATE INDEX IX_Incidencia_Carga_Id ON dbo.Incidencia (CargaId, Id);
GO

CREATE TABLE dbo.CargaEstadoHistorial (
    Id          BIGINT      NOT NULL IDENTITY(1,1) CONSTRAINT PK_CargaEstadoHistorial PRIMARY KEY,
    CargaId     BIGINT      NOT NULL CONSTRAINT FK_CargaEstadoHistorial_Carga REFERENCES dbo.CargaArchivo (Id),
    Estado      VARCHAR(20) NOT NULL
        CONSTRAINT CK_CargaEstadoHistorial_Estado CHECK (Estado IN ('Pendiente','En proceso','Cargado','Finalizado','Notificado','Fallido')),
    FechaCambio DATETIME2   NOT NULL
);
GO

CREATE INDEX IX_CargaEstadoHistorial_Carga_Id ON dbo.CargaEstadoHistorial (CargaId, Id);
GO
