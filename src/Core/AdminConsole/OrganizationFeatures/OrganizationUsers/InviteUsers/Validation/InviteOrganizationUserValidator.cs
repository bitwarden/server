using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OrganizationUserInvite = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.OrganizationUserInvite;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public interface IInviteUsersValidator : IValidator<InviteUserOrganizationValidationRequest>;

public class InviteUsersValidator(
    IOrganizationRepository organizationRepository,
    IInviteUsersPasswordManagerValidator inviteUsersPasswordManagerValidator,
    IUpdateSecretsManagerSubscriptionCommand secretsManagerSubscriptionCommand,
    IPaymentService paymentService) : IInviteUsersValidator
{
    public async Task<ValidationResult<InviteUserOrganizationValidationRequest>> ValidateAsync(InviteUserOrganizationValidationRequest request)
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
            request = new InviteUserOrganizationValidationRequest(request)
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
                .AdjustSeats(GetSecretManagerSeatAdjustment(
                    occupiedSeats: request.OccupiedSmSeats,
                    organization: request.InviteOrganization,
                    invitesToSend: request.Invites.Count(x => x.AccessSecretsManager)));

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

        int GetSecretManagerSeatAdjustment(int occupiedSeats, InviteOrganization organization, int invitesToSend) =>
            organization.SmSeats - (occupiedSeats + invitesToSend) ?? 0;
    }
}
