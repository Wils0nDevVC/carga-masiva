using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Procedimientos de referencias/V002__procedimientos.sql (una llamada por procedimiento, sin GO).
            migrationBuilder.Sql("""
CREATE PROCEDURE dbo.usp_ReservarPeriodo
    @CargaId   BIGINT,
    @Periodo   CHAR(7),
    @Resultado VARCHAR(20) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT > 0
        THROW 50010, N'usp_ReservarPeriodo no puede invocarse dentro de una transaccion abierta.', 1;

    SET @Resultado = NULL;

    IF @Periodo IS NULL OR @Periodo NOT LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9]' OR SUBSTRING(@Periodo, 6, 2) NOT BETWEEN '01' AND '12'
        THROW 50011, N'Periodo invalido: se espera AAAA-MM.', 1;

    DECLARE @estado VARCHAR(20) = (SELECT Estado FROM dbo.CargaArchivo WHERE Id = @CargaId);
    IF @estado IS NULL
        THROW 50012, N'La carga no existe.', 1;
    IF @estado <> 'En proceso'
        THROW 50013, N'La carga debe estar En proceso para reservar periodos.', 1;

    DECLARE @intento INT = 1;
    DECLARE @nuevo VARCHAR(20);

    WHILE @intento <= 2
    BEGIN
        -- Idempotencia: un resultado existente de (CargaId, Periodo) se devuelve sin cambios.
        SELECT @Resultado = Resultado FROM dbo.CargaPeriodo WHERE CargaId = @CargaId AND Periodo = @Periodo;
        IF @Resultado IS NOT NULL
            RETURN;

        BEGIN TRY
            BEGIN TRANSACTION;

            -- Excluye la carga actual.
            IF EXISTS (SELECT 1 FROM dbo.CargaPeriodo WHERE CargaId <> @CargaId AND Periodo = @Periodo AND Resultado = 'Completado')
                SET @nuevo = 'Rechazado';
            ELSE IF EXISTS (SELECT 1 FROM dbo.CargaPeriodo WHERE CargaId <> @CargaId AND Periodo = @Periodo AND Resultado = 'Admitido')
                SET @nuevo = 'Bloqueado';
            ELSE
                SET @nuevo = 'Admitido';

            INSERT INTO dbo.CargaPeriodo (CargaId, Periodo, Resultado, PeriodoReservado, FechaResultado)
            VALUES (@CargaId, @Periodo, @nuevo, CASE WHEN @nuevo = 'Admitido' THEN @Periodo END, SYSUTCDATETIME());

            IF @nuevo IN ('Rechazado', 'Bloqueado')
                INSERT INTO dbo.Incidencia (CargaId, CargaPeriodoId, Tipo, Motivo, FechaRegistro)
                VALUES (@CargaId, SCOPE_IDENTITY(),
                        CASE WHEN @nuevo = 'Rechazado' THEN 'PeriodoRechazado' ELSE 'PeriodoBloqueado' END,
                        CASE WHEN @nuevo = 'Rechazado'
                             THEN N'Periodo ' + @Periodo + N' ya completado por otra carga.'
                             ELSE N'Periodo ' + @Periodo + N' en proceso por otra carga.' END,
                        SYSUTCDATETIME());

            COMMIT TRANSACTION;
            SET @Resultado = @nuevo;
            RETURN;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

            -- Choque con el indice filtrado o con (CargaId, Periodo): reevaluar UNA vez.
            IF ERROR_NUMBER() IN (2601, 2627)
               AND (ERROR_MESSAGE() LIKE N'%UX_CargaPeriodo_PeriodoReservado%' OR ERROR_MESSAGE() LIKE N'%UQ_CargaPeriodo_Carga_Periodo%')
            BEGIN
                IF @intento >= 2
                    THROW 50014, N'Colision persistente al reservar el periodo; error tecnico, lo trata la aplicacion.', 1;
                SET @intento += 1;
                CONTINUE;
            END;

            THROW;
        END CATCH;
    END;
END;
""");

            migrationBuilder.Sql("""
CREATE PROCEDURE dbo.usp_CambiarEstadoCarga
    @CargaId        BIGINT,
    @NuevoEstado    VARCHAR(20),
    @PeriodoFallido CHAR(7) = NULL,
    @Cambiado       BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT > 0
        THROW 50020, N'usp_CambiarEstadoCarga no puede invocarse dentro de una transaccion abierta.', 1;

    SET @Cambiado = 0;

    IF @NuevoEstado IS NULL OR @NuevoEstado NOT IN ('Pendiente','En proceso','Cargado','Finalizado','Notificado','Fallido')
        THROW 50021, N'Estado de destino invalido.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @actual VARCHAR(20) = (SELECT Estado FROM dbo.CargaArchivo WITH (UPDLOCK, ROWLOCK) WHERE Id = @CargaId);
        IF @actual IS NULL
            THROW 50022, N'La carga no existe.', 1;

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
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE dbo.usp_CambiarEstadoCarga;");
            migrationBuilder.Sql("DROP PROCEDURE dbo.usp_ReservarPeriodo;");
        }
    }
}
