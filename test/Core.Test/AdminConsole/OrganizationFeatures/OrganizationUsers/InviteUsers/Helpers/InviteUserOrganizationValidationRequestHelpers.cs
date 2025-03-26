using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.SecretsManager;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Helpers;

public static class InviteUserOrganizationValidationRequestHelpers
{
    public static InviteUserOrganizationValidationRequest GetInviteValidationRequestMock(InviteOrganizationUsersRequest request,
        InviteOrganization inviteOrganization) =>
        new()
        {
            Invites = request.Invites,
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
