namespace Acity.Common.Helpers;

/// <summary>Contrato común de respuesta de los servicios. <c>Code</c> coincide con el estado HTTP real.</summary>
public sealed record Response<T>(
    T? Data,
    bool IsSuccess,
    string? Message,
    int Code,
    IReadOnlyCollection<string>? Errors = null)
{
    public static Response<T> Ok(T? data, string? message = "OK", int code = 200)
        => new(data, true, message, code);

    public static Response<T> Fail(int code, string? message, IReadOnlyCollection<string>? errors = null)
        => new(default, false, message, code, errors);
}
