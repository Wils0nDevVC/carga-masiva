namespace Acity.Infrastructure.Messaging;

/// <summary>Conexión a RabbitMQ (variables de entorno <c>RabbitMq__*</c>; credenciales sin valores en el repositorio).</summary>
public sealed class RabbitMqOptions
{
    public const string Seccion = "RabbitMq";

    public string Host { get; set; } = string.Empty;

    public int Puerto { get; set; } = 5672;

    public string Usuario { get; set; } = string.Empty;

    public string Clave { get; set; } = string.Empty;

    public bool EsValida() =>
        !string.IsNullOrWhiteSpace(Host) && Puerto > 0
        && !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Clave);
}
