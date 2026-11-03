using Acity.Application.Cargas.Queries.Dtos;

namespace Acity.Application.Interfaces.Repositories;

/// <summary>
/// Lecturas de seguimiento (Feature 010). El Administrador ve todas las cargas; el Operador solo las propias.
/// Una carga ajena para un Operador se devuelve como inexistente (null).
/// </summary>
public interface ICargasConsulta
{
    Task<IReadOnlyList<CargaResumenDto>> ListarAsync(int usuarioId, bool esAdministrador, CancellationToken cancellationToken);

    Task<CargaDetalleDto?> ObtenerDetalleAsync(long idCarga, int usuarioId, bool esAdministrador, CancellationToken cancellationToken);

    Task<ArchivoCarga?> ObtenerArchivoAsync(long idCarga, int usuarioId, bool esAdministrador, CancellationToken cancellationToken);
}

public sealed record ArchivoCarga(string NombreArchivo, string RutaArchivo);
