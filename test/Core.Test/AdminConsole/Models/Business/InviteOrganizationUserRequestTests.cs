using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Models.Business;

public class InviteOrganizationUserRequestTests
{
    [Theory]
    [BitAutoData]
    public void Create_WhenPassedInvalidEmail_ThrowsException(string email, string externalId,
        OrganizationUserType type, Permissions permissions)
    {
        var action = () => OrganizationUserSingleEmailInvite.Create(email, [], externalId, type, permissions);

        var exception = Assert.Throws<BadRequestException>(action);

        Assert.Equal(OrganizationUserSingleEmailInvite.InvalidEmailErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData]
    public void Create_WhenPassedInvalidCollectionAccessConfiguration_ThrowsException(string externalId,
        OrganizationUserType type, Permissions permissions)
    {
        var validEmail = "test@email.com";

        var invalidCollectionConfiguration = new CollectionAccessSelection { Manage = true, HidePasswords = true };

        var action = () =>
            OrganizationUserSingleEmailInvite.Create(validEmail, [invalidCollectionConfiguration], externalId, type,
                permissions);

        var exception = Assert.Throws<BadRequestException>(action);

        Assert.Equal(OrganizationUserSingleEmailInvite.InvalidCollectionConfigurationErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData]
    public void Create_WhenPassedValidArguments_ReturnsInvite(string externalId, OrganizationUserType type,
        Permissions permissions)
    {
        const string validEmail = "test@email.com";
        var validCollectionConfiguration = new CollectionAccessSelection { Id = Guid.NewGuid(), Manage = true };

        var invite = OrganizationUserSingleEmailInvite.Create(validEmail, [validCollectionConfiguration], externalId,
            type, permissions);

        Assert.NotNull(invite);
        Assert.Equal(validEmail, invite.Email);
        Assert.Contains(validCollectionConfiguration.Id, invite.AccessibleCollections);
    }
}
