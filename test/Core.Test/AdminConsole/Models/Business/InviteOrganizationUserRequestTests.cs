using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Models.Business;

public class InviteOrganizationUserRequestTests
{
    [Theory]
    [BitAutoData]
    public void Create_WhenPassedInvalidEmail_ThrowsException(string email)
    {
        var action = () => OrganizationUserSingleInvite.Create(email, []);

        var exception = Assert.Throws<BadRequestException>(action);

        Assert.Equal(OrganizationUserSingleInvite.InvalidEmailErrorMessage, exception.Message);
    }

    [Fact]
    public void Create_WhenPassedInvalidCollectionAccessConfiguration_ThrowsException()
    {
        var validEmail = "test@email.com";

        var invalidCollectionConfiguration = new CollectionAccessSelection
        {
            Manage = true,
            HidePasswords = true
        };

        var action = () => OrganizationUserSingleInvite.Create(validEmail, [invalidCollectionConfiguration]);

        var exception = Assert.Throws<BadRequestException>(action);

        Assert.Equal(OrganizationUserSingleInvite.InvalidCollecitonConfigurationErrorMessage, exception.Message);
    }

    [Fact]
    public void Create_WhenPassedValidArguments_ReturnsInvite()
    {
        const string validEmail = "test@email.com";
        var validCollectionConfiguration = new CollectionAccessSelection { Id = Guid.NewGuid(), Manage = true };

        var invite = OrganizationUserSingleInvite.Create(validEmail, [validCollectionConfiguration]);

        Assert.NotNull(invite);
        Assert.Equal(validEmail, invite.Email);
        Assert.Contains(validCollectionConfiguration.Id, invite.AccessibleCollections);
    }
}
