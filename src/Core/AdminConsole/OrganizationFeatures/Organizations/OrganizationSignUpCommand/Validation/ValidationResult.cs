namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand.Validation;

public record ValidationResult(params string[] ErrorMessages)
{
    public bool IsValid => ErrorMessages.Length == 0;
}

public record ValidationResult<T>(T Value, params string[] ErrorMessages) : IValidationResult<T>
{
    public bool IsValid => ErrorMessages.Length == 0;
}

public interface IValidationResult<out T>
{
    T Value { get; }
};

public record ValidResult<T>(T Value) : ValidResult, IValidationResult<T>;

public record InvalidResult<T>(T Value, params string[] ErrorMessages)
    : InvalidResult(ErrorMessages), IValidationResult<T>;

public record ValidResult() : ValidationResult([]);

public record InvalidResult(params string[] ErrorMessages) : ValidationResult(ErrorMessages);
