using Bit.Core.AdminConsole.Errors;

namespace Bit.Core.AdminConsole.Shared.Validation;

public abstract record ValidationResult<T>;

public record Valid<T> : ValidationResult<T>
{
    public Valid() { }

    public Valid(T Value)
    {
        this.Value = Value;
    }

    public T Value { get; init; }
}

public record PartialValidationResult<T>
{
    public List<Invalid<T>> InvalidResults { get; init; }

    public List<Valid<T>> ValidResults { get; init; }
}

public record Invalid<T> : ValidationResult<T>
{
    public IEnumerable<Error<T>> Errors { get; init; } = [];

    public string ErrorMessageString => string.Join(" ", Errors.Select(e => e.Message));

    public Invalid() { }

    public Invalid(Error<T> error) : this([error]) { }

    public Invalid(IEnumerable<Error<T>> errors)
    {
        Errors = errors;
    }
}
