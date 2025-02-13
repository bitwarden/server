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
    public void Create_WhenPassedInvalidEmails_ThrowsException(string[] emails, OrganizationUserType type, Permissions permissions, string externalId)
    {
        var action = () => OrganizationUserInvite.Create(emails, [], type, permissions, externalId, false);

        var exception = Assert.Throws<BadRequestException>(action);

        Assert.Contains(InvalidEmailErrorMessage, exception.Message);
    }

    [Fact]
    public void Create_WhenPassedInvalidCollectionAccessConfiguration_ThrowsException()
    {
        const string validEmail = "test@email.com";

        var invalidCollectionConfiguration = new CollectionAccessSelection
        {
            Manage = true,
            HidePasswords = true
        };

        var action = () => OrganizationUserInvite.Create([validEmail], [invalidCollectionConfiguration], default, default, default, false);

        var exception = Assert.Throws<BadRequestException>(action);

        Assert.Equal(InvalidCollectionConfigurationErrorMessage, exception.Message);
    }

    [Fact]
    public void Create_WhenPassedValidArguments_ReturnsInvite()
    {
        const string validEmail = "test@email.com";
        var validCollectionConfiguration = new CollectionAccessSelection { Id = Guid.NewGuid(), Manage = true };

        var invite = OrganizationUserInvite.Create([validEmail], [validCollectionConfiguration], default, default, default, false);

        Assert.NotNull(invite);
        Assert.Contains(validEmail, invite.Emails);
        Assert.Contains(validCollectionConfiguration.Id, invite.AccessibleCollections);
    }
}
