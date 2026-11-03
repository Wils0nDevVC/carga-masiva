using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Acity.Application.Autenticacion.Commands.CrearUsuariosIniciales;

/// <summary>
/// Repetible: si el correo ya existe no se crea otro usuario ni se modifica su contraseña o su rol (constitución §6).
/// Nunca registra la contraseña ni el hash.
/// </summary>
public sealed class CrearUsuariosInicialesCommandHandler(
    IAutenticacionRepository autenticacion,
    IGenericRepository<Usuario> usuarios,
    IUnitOfWork unitOfWork,
    IHashContrasena hashContrasena,
    ILogger<CrearUsuariosInicialesCommandHandler> logger) : IRequestHandler<CrearUsuariosInicialesCommand>
{
    public async Task Handle(CrearUsuariosInicialesCommand request, CancellationToken cancellationToken)
    {
        foreach (var inicial in request.Usuarios)
        {
            var rolId = await autenticacion.ObtenerRolIdAsync(inicial.Rol, cancellationToken)
                ?? throw new InvalidOperationException($"El rol {inicial.Rol} no existe en la base (semilla de V001).");

            var existente = await autenticacion.ObtenerUsuarioPorEmailAsync(inicial.Email, cancellationToken);
            if (existente is not null)
            {
                if (existente.Usuario.RolId != rolId)
                {
                    logger.LogWarning("El usuario inicial {Rol} ya existe con otro rol ({RolActual}); no se modifica. UsuarioId {UsuarioId}",
                        inicial.Rol, existente.Rol, existente.Usuario.Id);
                }
                else
                {
                    logger.LogInformation("Usuario inicial {Rol} ya existe; no se modifica. UsuarioId {UsuarioId}",
                        inicial.Rol, existente.Usuario.Id);
                }

                continue;
            }

            var usuario = new Usuario { Email = inicial.Email, RolId = rolId };
            usuario.PasswordHash = hashContrasena.Generar(usuario, inicial.Clave);
            await usuarios.CreateAsync(usuario, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Usuario inicial {Rol} creado. UsuarioId {UsuarioId}", inicial.Rol, usuario.Id);
        }
    }
}
