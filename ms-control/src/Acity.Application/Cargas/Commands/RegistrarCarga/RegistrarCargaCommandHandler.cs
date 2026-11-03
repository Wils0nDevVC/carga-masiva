using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Common.Helpers;
using Acity.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Acity.Application.Cargas.Commands.RegistrarCarga;

/// <summary>
/// Flujo de recepción (spec FR-018): subir el archivo → registrar la carga Pendiente y su historial en una
/// transacción → publicar el mensaje. Cada paso solo se ejecuta si el anterior terminó bien.
/// </summary>
public sealed class RegistrarCargaCommandHandler(
    IGenericRepository<Usuario> usuarios,
    IGenericRepository<CargaArchivo> cargas,
    IGenericRepository<CargaEstadoHistorial> historial,
    IUnitOfWork unitOfWork,
    IAlmacenamientoArchivos almacenamiento,
    IPublicadorCargas publicador,
    IEstadoCargaService estadoCarga,
    IGenericRepository<Incidencia> incidencias,
    ILogger<RegistrarCargaCommandHandler> logger)
    : IRequestHandler<RegistrarCargaCommand, Response<RegistrarCargaResponse>>
{
    private const string EstadoPendiente = "Pendiente";
    private const string TipoErrorTecnico = "ErrorTecnico";
    private const string MotivoPublicacion = "No se pudo confirmar la publicación del mensaje de procesamiento.";

    public async Task<Response<RegistrarCargaResponse>> Handle(RegistrarCargaCommand request, CancellationToken cancellationToken)
    {
        // El usuario del token debe existir en la identidad persistida; si no, nada se almacena, registra ni publica.
        var usuario = await usuarios.FindAsync(request.UsuarioId);
        if (usuario is null)
        {
            return Response<RegistrarCargaResponse>.Fail(401, "Usuario no reconocido");
        }

        // Desde aquí no se usa la cancelación de la solicitud: una desconexión del cliente no debe cortar el
        // flujo a mitad y dejar estados intermedios evitables.
        var sinCancelacion = CancellationToken.None;
        var ahora = DateTime.UtcNow;
        var rutaArchivo = $"seaweed://cargas/{ahora:yyyy}/{ahora:MM}/{Guid.NewGuid():N}.xlsx";

        // 1. Subida. Si falla, no se crea la carga.
        await almacenamiento.SubirAsync(rutaArchivo, request.AbrirContenido!, sinCancelacion);

        // 2. Registro de la carga y su historial Pendiente en una transacción. Si falla, no se publica.
        var carga = new CargaArchivo
        {
            NombreArchivo = request.NombreArchivo!,
            UsuarioId = request.UsuarioId,
            FechaRecepcion = ahora,
            RutaArchivo = rutaArchivo,
            Estado = EstadoPendiente,
            FechaFin = null,
            IntentosProceso = 0
        };
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await cargas.CreateAsync(carga, ct);
                await unitOfWork.SaveChangesAsync(ct);
                await historial.CreateAsync(new CargaEstadoHistorial
                {
                    CargaId = carga.Id,
                    Estado = EstadoPendiente,
                    FechaCambio = ahora
                }, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }, sinCancelacion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo registrar la carga; el archivo {RutaArchivo} quedó almacenado sin carga", rutaArchivo);
            throw;
        }

        // 3. Publicación con confirmación del intermediario.
        try
        {
            await publicador.PublicarAsync(new MensajeCarga(carga.Id, rutaArchivo, usuario.Email), sinCancelacion);
        }
        catch (Exception exPublicacion)
        {
            // Un fallo sin confirmación no prueba que el mensaje no llegó: Fallido solo si la carga sigue Pendiente.
            if (await RegistrarFalloDePublicacionAsync(carga.Id, exPublicacion))
            {
                throw;
            }

            logger.LogWarning(
                "Publicación de la carga {IdCarga} no confirmada, pero la carga ya salió de Pendiente: el mensaje se recibió",
                carga.Id);
            return Aceptada(carga.Id);
        }

        logger.LogInformation(
            "Carga {IdCarga} aceptada: usuario {UsuarioId}, archivo {NombreArchivo} ({Longitud} bytes) en {RutaArchivo}",
            carga.Id, request.UsuarioId, request.NombreArchivo, request.Longitud, rutaArchivo);

        return Aceptada(carga.Id);
    }

    /// <summary>
    /// Tras un fallo definitivo de publicación: Fallido desde Pendiente (atómico, V003) y, si se aplicó, la incidencia
    /// técnica. Devuelve <c>true</c> si la solicitud debe responder error (Fallido aplicado o no se pudo registrar) y
    /// <c>false</c> si la carga ya había salido de Pendiente (el consumidor recibió el mensaje).
    /// </summary>
    private async Task<bool> RegistrarFalloDePublicacionAsync(long idCarga, Exception exPublicacion)
    {
        try
        {
            if (!await estadoCarga.PasarAFallidoDesdePendienteAsync(idCarga, CancellationToken.None))
            {
                return false;
            }

            await incidencias.CreateAsync(new Incidencia
            {
                CargaId = idCarga,
                CargaPeriodoId = null,
                Tipo = TipoErrorTecnico,
                NumeroFila = null,
                CodigoProducto = null,
                PeriodoTexto = null,
                Motivo = MotivoPublicacion,
                FechaRegistro = DateTime.UtcNow
            }, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);

            logger.LogError(exPublicacion, "No se pudo publicar la carga {IdCarga}: queda Fallido con incidencia técnica", idCarga);
        }
        catch (Exception exRegistro)
        {
            logger.LogError(new AggregateException(exPublicacion, exRegistro),
                "No se pudo publicar la carga {IdCarga} ni completar el registro del fallo", idCarga);
        }

        return true;
    }

    private static Response<RegistrarCargaResponse> Aceptada(long idCarga)
        => Response<RegistrarCargaResponse>.Ok(new RegistrarCargaResponse(idCarga, EstadoPendiente), "Carga registrada", 202);
}
