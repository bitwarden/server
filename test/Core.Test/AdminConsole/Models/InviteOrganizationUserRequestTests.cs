using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.InviteOrganizationUserErrorMessages;

namespace Bit.Core.Test.AdminConsole.Models;

public class InviteOrganizationUserRequestTests
{
    [Theory]
    [BitAutoData]
    public void Create_WhenPassedInvalidEmail_ThrowsException(string email,
        OrganizationUserType type, Permissions permissions, bool accessSecretsManager)
    {
        var action = () => new OrganizationUserSingleEmailInvite(email, [], type, permissions, accessSecretsManager);

        var exception = Assert.Throws<BadRequestException>(action);

        Assert.Equal(InvalidEmailErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData]
    public void Create_WhenPassedInvalidCollectionAccessConfiguration_ThrowsException(OrganizationUserType type, Permissions permissions, bool accessSecretsManager)
    {
        var validEmail = "test@email.com";

        var invalidCollectionConfiguration = new CollectionAccessSelection { Manage = true, HidePasswords = true };

        var action = () => new OrganizationUserSingleEmailInvite(validEmail, [invalidCollectionConfiguration], type, permissions, accessSecretsManager);

        var exception = Assert.Throws<BadRequestException>(action);

        Assert.Equal(InvalidCollectionConfigurationErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData]
    public void Create_WhenPassedValidArguments_ReturnsInvite(OrganizationUserType type, Permissions permissions, bool accessSecretsManager)
    {
        const string validEmail = "test@email.com";
        var validCollectionConfiguration = new CollectionAccessSelection { Id = Guid.NewGuid(), Manage = true };

        var invite = new OrganizationUserSingleEmailInvite(validEmail, [validCollectionConfiguration], type, permissions, accessSecretsManager);

        Assert.NotNull(invite);
        Assert.Equal(validEmail, invite.Email);
        Assert.Contains(validCollectionConfiguration, invite.AccessibleCollections);
    }
}
