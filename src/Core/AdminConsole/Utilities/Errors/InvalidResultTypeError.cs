namespace Bit.Core.AdminConsole.Utilities.Errors;

public record InvalidResultTypeError<T>(T Value) : Error<T>(Code, Value)
{
    public const string Code = "Invalid result type.";
};
