namespace Bit.Core.AdminConsole.Utilities.Validation;

public interface IValidator<T>
{
    public Task<ValidationResult<T>> ValidateAsync(T value);
}
