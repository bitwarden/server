using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.Utilities.Errors;
using Bit.Core.AdminConsole.Utilities.Validation;
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
    public async Task<ValidationResult<InviteOrganizationUsersValidationRequest>> ValidateAsync(
        InviteOrganizationUsersValidationRequest request)
    {
        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(request);

        var passwordManagerValidationResult =
            await inviteUsersPasswordManagerValidator.ValidateAsync(subscriptionUpdate);

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
                autoscaling: true);

            var seatsToAdd = GetSecretManagerSeatAdjustment(request);

            if (seatsToAdd > 0)
            {
                smSubscriptionUpdate.AdjustSeats(seatsToAdd);

                await secretsManagerSubscriptionCommand.ValidateUpdateAsync(smSubscriptionUpdate);
            }

            return new Valid<InviteOrganizationUsersValidationRequest>(new InviteOrganizationUsersValidationRequest(
                request,
                subscriptionUpdate,
                smSubscriptionUpdate));
        }
        catch (Exception ex)
        {
            return new Invalid<InviteOrganizationUsersValidationRequest>(
                new Error<InviteOrganizationUsersValidationRequest>(ex.Message, request));
        }
    }

    /// <summary>
    /// This calculates the number of SM seats to add to the organization seat total.
    ///
    /// If they have a current seat limit (it can be null), we want to figure out how many are available (seats -
    /// occupied seats). Then, we'll subtract the available seats from the number of users we're trying to invite.
    ///
    /// If it's negative, we have available seats and do not need to increase, so we go with 0.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    private static int GetSecretManagerSeatAdjustment(InviteOrganizationUsersValidationRequest request) =>
        request.InviteOrganization.SmSeats.HasValue
            ? Math.Max(
                request.Invites.Count(x => x.AccessSecretsManager) -
                (request.InviteOrganization.SmSeats.Value -
                 request.OccupiedSmSeats),
                0)
            : 0;
}
