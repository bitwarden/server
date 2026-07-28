using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response;

public class OrganizationUserResponseModelTests
{
    [Theory, BitAutoData]
    public void OrganizationUserDetailsResponseModel_Constructor_PopulatesCreationDate(
        OrganizationUserUserDetails orgUser)
    {
        // Permissions is deserialized as JSON by the base constructor; clear the random fixture value.
        orgUser.Permissions = null;

        var result = new OrganizationUserDetailsResponseModel(orgUser, claimedByOrganization: true,
            collections: new List<CollectionAccessSelection>());

        Assert.Equal(orgUser.CreationDate, result.CreationDate);
    }

    [Theory, BitAutoData]
    public void OrganizationUserUserDetailsResponseModel_Constructor_PopulatesCreationDate(
        OrganizationUserUserDetails orgUser)
    {
        orgUser.Permissions = null;

        var result = new OrganizationUserUserDetailsResponseModel(orgUser, twoFactorEnabled: false,
            claimedByOrganization: true);

        Assert.Equal(orgUser.CreationDate, result.CreationDate);
    }

    [Theory, BitAutoData]
    public void OrganizationUserUserDetailsResponseModel_TupleConstructor_PopulatesCreationDate(
        OrganizationUserUserDetails orgUser)
    {
        orgUser.Permissions = null;

        var result = new OrganizationUserUserDetailsResponseModel((orgUser, false, true));

        Assert.Equal(orgUser.CreationDate, result.CreationDate);
    }
}
