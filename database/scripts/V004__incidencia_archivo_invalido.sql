-- V004: tipo de incidencia ArchivoInvalido (Feature 006, decision del usuario Q1, autorizada 2026-11-03).
-- Contrato: specs/006-procesamiento-cargas/contracts/v004-incidencia-archivo-invalido.md.
-- Amplia CK_Incidencia_Tipo (mismo nombre) con 'ArchivoInvalido': archivo corrupto, sin hojas o sin
-- los encabezados requeridos. Sin otros cambios de esquema. Lo aplica el runner en su transaccion.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

ALTER TABLE dbo.Incidencia DROP CONSTRAINT CK_Incidencia_Tipo;
GO

ALTER TABLE dbo.Incidencia ADD CONSTRAINT CK_Incidencia_Tipo
    CHECK (Tipo IN ('PeriodoRechazado','PeriodoBloqueado','FilaOmitida','Existente','NoProcesado','ErrorTecnico','ErrorNotificacion','ArchivoInvalido'));
GO
