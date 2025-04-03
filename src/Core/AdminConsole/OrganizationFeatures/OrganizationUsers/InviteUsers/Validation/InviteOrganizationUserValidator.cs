using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OrganizationUserInvite = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.OrganizationUserInvite;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public interface IInviteUsersValidator : IValidator<InviteOrganizationUsersValidationRequest>;

public class InviteOrganizationUsersValidator(
    IOrganizationRepository organizationRepository,
    IInviteUsersPasswordManagerValidator inviteUsersPasswordManagerValidator,
    IUpdateSecretsManagerSubscriptionCommand secretsManagerSubscriptionCommand,
    IPaymentService paymentService) : IInviteUsersValidator
{
    public async Task<ValidationResult<InviteOrganizationUsersValidationRequest>> ValidateAsync(InviteOrganizationUsersValidationRequest request)
    {
        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(request);

        var passwordManagerValidationResult = await inviteUsersPasswordManagerValidator.ValidateAsync(subscriptionUpdate);

        if (passwordManagerValidationResult is Invalid<PasswordManagerSubscriptionUpdate> invalidSubscriptionUpdate)
        {
            return invalidSubscriptionUpdate.Map(request);
        }

        // If the organization has the Secrets Manager Standalone Discount, all users are added to secrets manager.
        // This is an expensive call, so we're doing it now to delay the check as long as possible.
        if (await paymentService.HasSecretsManagerStandalone(request.InviteOrganization))
        {
            request = new InviteOrganizationUsersValidationRequest(request)
            {
                Invites = request.Invites
                    .Select(x => new OrganizationUserInvite(x, accessSecretsManager: true))
                    .ToArray()
            };
        }

        if (request.InviteOrganization.UseSecretsManager && request.Invites.Any(x => x.AccessSecretsManager))
        {
            return await ValidateSecretsManagerSubscriptionUpdateAsync(request, subscriptionUpdate);
        }

        return new Valid<InviteOrganizationUsersValidationRequest>(new InviteOrganizationUsersValidationRequest(
            request,
            subscriptionUpdate,
            null));
    }

    private async Task<ValidationResult<InviteOrganizationUsersValidationRequest>> ValidateSecretsManagerSubscriptionUpdateAsync(
        InviteOrganizationUsersValidationRequest request,
        PasswordManagerSubscriptionUpdate subscriptionUpdate)
    {
        try
        {
            var smSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(
                    organization: await organizationRepository.GetByIdAsync(request.InviteOrganization.OrganizationId),
                    plan: request.InviteOrganization.Plan,
                    autoscaling: true)
                .AdjustSeats(GetSecretManagerSeatAdjustment(request));

            await secretsManagerSubscriptionCommand.ValidateUpdateAsync(smSubscriptionUpdate);

            return new Valid<InviteOrganizationUsersValidationRequest>(new InviteOrganizationUsersValidationRequest(
                request,
                subscriptionUpdate,
                smSubscriptionUpdate));
        }
        catch (Exception ex)
        {
            return new Invalid<InviteOrganizationUsersValidationRequest>(new Error<InviteOrganizationUsersValidationRequest>(ex.Message, request));
        }

        int GetSecretManagerSeatAdjustment(InviteOrganizationUsersValidationRequest request) =>
            Math.Abs(
                request.InviteOrganization.SmSeats -
                request.OccupiedSmSeats -
                request.Invites.Count(x => x.AccessSecretsManager) ?? 0);
    }
}
