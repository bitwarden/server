using Bit.Core.AdminConsole.Shared.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;

public interface IEnvironmentValidator : IValidator<EnvironmentRequest>;

public class EnvironmentValidator : IEnvironmentValidator
{
    public async Task<ValidationResult<EnvironmentRequest>> ValidateAsync(EnvironmentRequest value) =>
        value.IsSelfHosted && value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd > 0 ?
           new Invalid<EnvironmentRequest>(new CannotAutoScaleOnSelfHostError(value)) :
            new Valid<EnvironmentRequest>(value);
}
