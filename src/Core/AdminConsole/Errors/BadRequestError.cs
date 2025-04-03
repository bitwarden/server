namespace Bit.Core.AdminConsole.Errors;

public record BadRequestError<T> : Error<T>
{
    public BadRequestError(string code, T invalidRequest)
        : base(code, invalidRequest) { }
}
