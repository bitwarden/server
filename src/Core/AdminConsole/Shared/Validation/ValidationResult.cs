namespace Bit.Core.AdminConsole.Shared.Validation;

public abstract record ValidationResult<T>;

public record Valid<T> : ValidationResult<T>
{
    public T Value { get; init; }
}

public record Invalid<T> : ValidationResult<T>
{
    public IEnumerable<Error<T>> Errors { get; init; }
}
