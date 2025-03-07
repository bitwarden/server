using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Helpers;

public static class InviteUserOrganizationValidationRequestHelpers
{
    public static InviteUserOrganizationValidationRequest GetInviteValidationRequestMock(InviteScimOrganizationUserRequest request,
        InviteOrganization inviteOrganization) =>
        new()
        {
            Invites =
            [
                OrganizationUserInviteDto.Create(request.Email,
                    OrganizationUserInvite.Create(request, request.ExternalId), inviteOrganization.OrganizationId)
            ],
            InviteOrganization = inviteOrganization,
            PerformedBy = Guid.Empty,
            PerformedAt = request.PerformedAt,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 0,
            PasswordManagerSubscriptionUpdate = new PasswordManagerSubscriptionUpdate(inviteOrganization, 0, 0),
            SecretsManagerSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(inviteOrganization, 0, 0, 0)
        };

    public static InviteUserOrganizationValidationRequest WithPasswordManagerUpdate(this InviteUserOrganizationValidationRequest request, PasswordManagerSubscriptionUpdate passwordManagerSubscriptionUpdate) =>
        new()
        {
            Invites = request.Invites,
            InviteOrganization = request.InviteOrganization,
            PerformedBy = request.PerformedBy,
            PerformedAt = request.PerformedAt,
            OccupiedPmSeats = request.OccupiedPmSeats,
            OccupiedSmSeats = request.OccupiedSmSeats,
            PasswordManagerSubscriptionUpdate = passwordManagerSubscriptionUpdate,
            SecretsManagerSubscriptionUpdate = request.SecretsManagerSubscriptionUpdate
        };

    public static InviteUserOrganizationValidationRequest WithSecretsManagerUpdate(this InviteUserOrganizationValidationRequest request, SecretsManagerSubscriptionUpdate secretsManagerSubscriptionUpdate) =>
        new()
        {
            Invites = request.Invites,
            InviteOrganization = request.InviteOrganization,
            PerformedBy = request.PerformedBy,
            PerformedAt = request.PerformedAt,
            OccupiedPmSeats = request.OccupiedPmSeats,
            OccupiedSmSeats = request.OccupiedSmSeats,
            PasswordManagerSubscriptionUpdate = request.PasswordManagerSubscriptionUpdate,
            SecretsManagerSubscriptionUpdate = secretsManagerSubscriptionUpdate
        };
}
