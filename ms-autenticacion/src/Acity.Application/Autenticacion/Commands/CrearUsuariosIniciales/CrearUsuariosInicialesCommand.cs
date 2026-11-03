using MediatR;

namespace Acity.Application.Autenticacion.Commands.CrearUsuariosIniciales;

/// <summary>Un usuario inicial por rol, con credenciales de configuración externa (constitución §6).</summary>
public sealed record CrearUsuariosInicialesCommand(IReadOnlyList<UsuarioInicial> Usuarios) : IRequest;

public sealed record UsuarioInicial(string Rol, string Email, string Clave);
