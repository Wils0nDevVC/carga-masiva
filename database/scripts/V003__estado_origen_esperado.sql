-- V003: estado de origen esperado opcional en usp_CambiarEstadoCarga (Feature 005, decision del usuario opcion A).
-- Contrato: specs/005-recepcion-registro-cargas/contracts/usp-cambiar-estado-carga-v003.md.
-- El parametro nuevo va al final y es opcional: sin el, el comportamiento es identico a V002.
-- Con el, si el estado actual no coincide, devuelve @Cambiado = 0 sin escribir nada; la lectura,
-- la comparacion y el cambio ocurren en la misma transaccion con el bloqueo de fila.
-- MUST invocarse fuera de cualquier transaccion abierta de la aplicacion (igual que en V002).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

ALTER PROCEDURE dbo.usp_CambiarEstadoCarga
    @CargaId        BIGINT,
    @NuevoEstado    VARCHAR(20),
    @PeriodoFallido CHAR(7) = NULL,
    @Cambiado       BIT OUTPUT,
    @EstadoEsperado VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT > 0
        THROW 50020, N'usp_CambiarEstadoCarga no puede invocarse dentro de una transaccion abierta.', 1;

    SET @Cambiado = 0;

    IF @NuevoEstado IS NULL OR @NuevoEstado NOT IN ('Pendiente','En proceso','Cargado','Finalizado','Notificado','Fallido')
        THROW 50021, N'Estado de destino invalido.', 1;

    IF @EstadoEsperado IS NOT NULL AND @EstadoEsperado NOT IN ('Pendiente','En proceso','Cargado','Finalizado','Notificado','Fallido')
        THROW 50023, N'Estado de origen esperado invalido.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @actual VARCHAR(20) = (SELECT Estado FROM dbo.CargaArchivo WITH (UPDLOCK, ROWLOCK) WHERE Id = @CargaId);
        IF @actual IS NULL
            THROW 50022, N'La carga no existe.', 1;

        -- Origen esperado (opcional): si el estado ya cambio, no se sobrescribe ni se registra nada.
        IF @EstadoEsperado IS NOT NULL AND @actual <> @EstadoEsperado
        BEGIN
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- Matriz de transiciones; Notificado y Fallido son terminales. Repeticion o invalida: 0, sin escribir.
        IF NOT (   (@actual = 'Pendiente'  AND @NuevoEstado IN ('En proceso', 'Fallido'))
                OR (@actual = 'En proceso' AND @NuevoEstado IN ('Cargado', 'Fallido'))
                OR (@actual = 'Cargado'    AND @NuevoEstado = 'Finalizado')
                OR (@actual = 'Finalizado' AND @NuevoEstado = 'Notificado'))
        BEGIN
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        UPDATE dbo.CargaArchivo
        SET Estado = @NuevoEstado,
            FechaFin = CASE WHEN @NuevoEstado = 'Finalizado' THEN SYSUTCDATETIME() ELSE FechaFin END
        WHERE Id = @CargaId;

        INSERT INTO dbo.CargaEstadoHistorial (CargaId, Estado, FechaCambio)
        VALUES (@CargaId, @NuevoEstado, SYSUTCDATETIME());

        IF @NuevoEstado = 'Fallido'
        BEGIN
            -- Pendientes (Admitido) no afirman haber fallado: NoProcesado + incidencia; el periodo del fallo queda Fallido.
            INSERT INTO dbo.Incidencia (CargaId, CargaPeriodoId, Tipo, Motivo, FechaRegistro)
            SELECT CargaId, Id, 'NoProcesado', N'No procesado por interrupción de la carga', SYSUTCDATETIME()
            FROM dbo.CargaPeriodo
            WHERE CargaId = @CargaId AND Resultado = 'Admitido'
              AND (@PeriodoFallido IS NULL OR Periodo <> @PeriodoFallido);

            -- Libera las reservas; los Completado se conservan.
            UPDATE dbo.CargaPeriodo
            SET Resultado = CASE WHEN Periodo = @PeriodoFallido THEN 'Fallido' ELSE 'NoProcesado' END,
                PeriodoReservado = NULL,
                FechaResultado = SYSUTCDATETIME()
            WHERE CargaId = @CargaId AND Resultado = 'Admitido';
        END;

        COMMIT TRANSACTION;
        SET @Cambiado = 1;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
