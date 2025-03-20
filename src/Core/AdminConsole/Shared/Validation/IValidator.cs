namespace Bit.Core.AdminConsole.Shared.Validation;

public interface IValidator<T>
{
    public Task<ValidationResult<T>> ValidateAsync(T value);
}
