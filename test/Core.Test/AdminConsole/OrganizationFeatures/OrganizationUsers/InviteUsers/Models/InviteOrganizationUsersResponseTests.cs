using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersResponseTests
{
    [Theory, BitAutoData]
    public void Constructor_FromValidationRequest_CreatesInvitedUsers(Guid organizationId)
    {
        var validationRequest = new InviteOrganizationUsersValidationRequest
        {
            InviteOrganization = new InviteOrganization { OrganizationId = organizationId },
            Invites =
            [
                new OrganizationUserInviteCommandModel("First@Example.com", "first-external-id"),
                new OrganizationUserInviteCommandModel("second@example.com", "second-external-id"),
            ],
        };

        var response = new InviteOrganizationUsersResponse(validationRequest);

        var invitedUsers = response.InvitedUsers.ToList();
        Assert.Equal(2, invitedUsers.Count);
        Assert.All(invitedUsers, orgUser =>
        {
            Assert.Equal(organizationId, orgUser.OrganizationId);
            Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
            Assert.Equal(OrganizationUserType.User, orgUser.Type);
            Assert.Null(orgUser.UserId);
            Assert.Null(orgUser.Key);
        });
        Assert.Equal("first@example.com", invitedUsers[0].Email);
        Assert.Equal("first-external-id", invitedUsers[0].ExternalId);
        Assert.Equal(invitedUsers.Select(u => u.Id), response.InvitedUsers.Select(u => u.Id));
    }
}
