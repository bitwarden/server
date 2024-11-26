namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand.Validation;

public interface IValidation<T>
{
    IValidationResult<T> Validate(T entity);
}
