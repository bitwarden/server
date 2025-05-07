namespace Bit.Core.AdminConsole.Utilities.Errors;

public record Error<T>(string Message, T ErroredValue);

public static class ErrorMappers
{
    public static Error<B> ToError<A, B>(this Error<A> errorA, B erroredValue) => new(errorA.Message, erroredValue);
}
