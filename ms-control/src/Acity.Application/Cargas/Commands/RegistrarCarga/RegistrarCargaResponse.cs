namespace Acity.Application.Cargas.Commands.RegistrarCarga;

/// <summary>Resultado de una carga aceptada: su identificador y el estado inicial.</summary>
public sealed record RegistrarCargaResponse(long IdCarga, string Estado);
