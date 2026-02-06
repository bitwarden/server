using Bit.Core.AdminConsole.Utilities.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;

public interface IInviteUsersEnvironmentValidator : IValidator<EnvironmentRequest>;

public class InviteUsersEnvironmentValidator : IInviteUsersEnvironmentValidator
{
    public Task<ValidationResult<EnvironmentRequest>> ValidateAsync(EnvironmentRequest value) =>
        Task.FromResult<ValidationResult<EnvironmentRequest>>(
            value.IsSelfHosted && value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd > 0 ?
                new Invalid<EnvironmentRequest>(new CannotAutoScaleOnSelfHostError(value)) :
                new Valid<EnvironmentRequest>(value));
}
