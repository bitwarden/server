namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public abstract record ValidationResult<T>(T Value, IEnumerable<string> Errors)
{
    public bool IsValid => !Errors.Any();

    public string ErrorMessageString => string.Join(" ", Errors);
}

public record Valid<T>(T Value) : ValidationResult<T>(Value, []);

public record Invalid<T>(IEnumerable<string> Errors) : ValidationResult<T>(default, Errors)
{
    public Invalid(string error) : this([error]) { }
}
