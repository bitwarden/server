using Bit.Core.AdminConsole.Shared.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;

public interface IEnvironmentValidator : IValidator<EnvironmentRequest>;

public class EnvironmentValidator : IEnvironmentValidator
{
    public Task<ValidationResult<EnvironmentRequest>> ValidateAsync(EnvironmentRequest value) =>
        Task.FromResult<ValidationResult<EnvironmentRequest>>(
            value.IsSelfHosted && value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd > 0 ?
                new Invalid<EnvironmentRequest>(new CannotAutoScaleOnSelfHostError(value)) :
                new Valid<EnvironmentRequest>(value));
}
