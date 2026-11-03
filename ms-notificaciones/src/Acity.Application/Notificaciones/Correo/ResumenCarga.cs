namespace Acity.Application.Notificaciones.Correo;

/// <summary>Datos de la carga para el correo (Feature 016): solo lectura.</summary>
public sealed record ResumenCarga(
    string NombreArchivo,
    IReadOnlyList<PeriodoResumen> Periodos,
    int TotalIncidencias,
    IReadOnlyList<TipoRecuento> PorTipo,
    IReadOnlyList<IncidenciaResumen> Primeras);

public sealed record PeriodoResumen(string Periodo, string Resultado);

public sealed record TipoRecuento(string Tipo, int Cantidad);

public sealed record IncidenciaResumen(string Tipo, int? NumeroFila, string? CodigoProducto, string? PeriodoTexto, string Motivo);
