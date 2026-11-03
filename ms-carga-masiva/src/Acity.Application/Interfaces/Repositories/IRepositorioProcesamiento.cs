using Acity.Domain.Entities;

namespace Acity.Application.Interfaces.Repositories;

/// <summary>
/// Operaciones de persistencia del procesamiento que requieren EF Core (consultas asíncronas, actualización de una
/// sentencia, transacción por periodo y tratamiento de colisiones de unicidad). Solo lo que el consumidor necesita.
/// </summary>
public interface IRepositorioProcesamiento
{
    Task<CargaArchivo?> ObtenerCargaAsync(long cargaId, CancellationToken cancellationToken);

    /// <summary>Cargas interrumpidas a recuperar (En proceso y Cargado), por Id.</summary>
    Task<IReadOnlyList<CargaArchivo>> ObtenerCargasARecuperarAsync(CancellationToken cancellationToken);

    /// <summary>Incrementa <c>IntentosProceso</c> con una sola sentencia y devuelve el nuevo valor.</summary>
    Task<int> IncrementarIntentosAsync(long cargaId, CancellationToken cancellationToken);

    /// <summary>Periodos de la carga: Periodo → (Id, Resultado).</summary>
    Task<IReadOnlyDictionary<string, (long Id, string Resultado)>> ObtenerPeriodosAsync(long cargaId, CancellationToken cancellationToken);

    /// <summary>Códigos (de la lista) ya confirmados por otras cargas; compara la base con su collation.</summary>
    Task<IReadOnlySet<string>> ObtenerCodigosDeOtrasCargasAsync(long cargaId, IReadOnlyCollection<string> codigos, CancellationToken cancellationToken);

    /// <summary>¿Existe ya una incidencia sin periodo de ese tipo para la carga? (idempotencia en recuperación).</summary>
    Task<bool> ExisteIncidenciaSinPeriodoAsync(long cargaId, string tipo, CancellationToken cancellationToken);

    /// <summary>Inserta incidencias en una sola confirmación (todas o ninguna). Sin reintentos.</summary>
    Task RegistrarIncidenciasAsync(IReadOnlyCollection<Incidencia> incidencias, CancellationToken cancellationToken);

    /// <summary>
    /// Confirma en una transacción los productos, las incidencias y el paso del periodo a Completado. Si el periodo ya
    /// está Completado no hace nada. Ante una colisión de <c>CodigoProducto</c> repite el periodo producto a producto y
    /// convierte cada producto que colisiona en la incidencia que devuelve <paramref name="existentePorColision"/>.
    /// </summary>
    Task<ResultadoPersistenciaPeriodo> PersistirPeriodoAsync(
        long cargaPeriodoId,
        IReadOnlyList<Producto> productos,
        IReadOnlyList<Incidencia> incidencias,
        Func<Producto, Incidencia> existentePorColision,
        CancellationToken cancellationToken);

    /// <summary>Correo del responsable y fecha de fin de la carga.</summary>
    Task<(string Email, DateTime? FechaFin)> ObtenerDatosNotificacionAsync(long cargaId, CancellationToken cancellationToken);
}

/// <summary>Resultado de persistir un periodo.</summary>
public enum ResultadoPersistenciaPeriodo
{
    Completado,
    YaCompletado,
    CompletadoConColisiones
}
