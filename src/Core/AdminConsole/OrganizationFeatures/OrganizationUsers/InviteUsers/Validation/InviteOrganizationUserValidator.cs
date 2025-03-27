using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public interface IInviteUsersValidator : IValidator<InviteUserOrganizationValidationRequest>;

public class InviteUsersValidator(
    IOrganizationRepository organizationRepository,
    IPasswordManagerInviteUserValidator passwordManagerInviteUserValidator,
    IUpdateSecretsManagerSubscriptionCommand secretsManagerSubscriptionCommand) : IInviteUsersValidator
{
    public async Task<ValidationResult<InviteUserOrganizationValidationRequest>> ValidateAsync(InviteUserOrganizationValidationRequest request)
    {
        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(request);

        var passwordManagerValidationResult = await passwordManagerInviteUserValidator.ValidateAsync(subscriptionUpdate);

        if (passwordManagerValidationResult is Invalid<PasswordManagerSubscriptionUpdate> invalidSubscriptionUpdate)
        {
            return invalidSubscriptionUpdate.Map(request);
        }

        if (request.InviteOrganization.UseSecretsManager && request.Invites.Any(x => x.AccessSecretsManager))
        {
            return await ValidateSecretsManagerSubscriptionUpdateAsync(request, subscriptionUpdate);
        }

        return new Valid<InviteUserOrganizationValidationRequest>(new InviteUserOrganizationValidationRequest(
            request,
            subscriptionUpdate,
            null));
    }

    private async Task<ValidationResult<InviteUserOrganizationValidationRequest>> ValidateSecretsManagerSubscriptionUpdateAsync(
        InviteUserOrganizationValidationRequest request,
        PasswordManagerSubscriptionUpdate subscriptionUpdate)
    {
        try
        {
            var smSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(
                    organization: await organizationRepository.GetByIdAsync(request.InviteOrganization.OrganizationId),
                    plan: request.InviteOrganization.Plan,
                    autoscaling: true)
                .AdjustSeats(request.Invites.Count(x => x.AccessSecretsManager));


            await secretsManagerSubscriptionCommand.ValidateUpdateAsync(smSubscriptionUpdate);

            return new Valid<InviteUserOrganizationValidationRequest>(new InviteUserOrganizationValidationRequest(
                request,
                subscriptionUpdate,
                smSubscriptionUpdate));
        }
        catch (Exception ex)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(new Error<InviteUserOrganizationValidationRequest>(ex.Message, request));
        }
    }
}
