using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.InviteOrganizationUserErrorMessages;

namespace Bit.Core.Test.AdminConsole.Models;

public class InviteOrganizationUsersRequestTests
{
    [Theory]
    [BitAutoData]
    public void Constructor_WhenPassedInvalidEmail_ThrowsException(string email, OrganizationUserType type, Permissions permissions, string externalId)
    {
        var exception = Assert.Throws<BadRequestException>(() =>
            new OrganizationUserInvite(email, [], [], type, permissions, externalId, false));

        Assert.Contains(InvalidEmailErrorMessage, exception.Message);
    }

    [Fact]
    public void Constructor_WhenPassedInvalidCollectionAccessConfiguration_ThrowsException()
    {
        const string validEmail = "test@email.com";

        var invalidCollectionConfiguration = new CollectionAccessSelection
        {
            Manage = true,
            HidePasswords = true
        };

        var exception = Assert.Throws<BadRequestException>(() =>
            new OrganizationUserInvite(
                email: validEmail,
                assignedCollections: [invalidCollectionConfiguration],
                groups: [],
                type: default,
                permissions: new Permissions(),
                externalId: string.Empty,
                accessSecretsManager: false));

        Assert.Equal(InvalidCollectionConfigurationErrorMessage, exception.Message);
    }

    [Fact]
    public void Constructor_WhenPassedValidArguments_ReturnsInvite()
    {
        const string validEmail = "test@email.com";
        var validCollectionConfiguration = new CollectionAccessSelection { Id = Guid.NewGuid(), Manage = true };

        var invite = new OrganizationUserInvite(
            email: validEmail,
            assignedCollections: [validCollectionConfiguration],
            groups: [],
            type: default,
            permissions: null,
            externalId: null,
            accessSecretsManager: false);

        Assert.NotNull(invite);
        Assert.Contains(validEmail, invite.Email);
        Assert.Contains(validCollectionConfiguration, invite.AssignedCollections);
    }
}
