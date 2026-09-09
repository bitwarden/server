using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response;

public class OrganizationUserResponseModelTests
{
    [Theory, BitAutoData]
    public void Constructor_OrganizationUser_PopulatesAccessFlags(OrganizationUser orgUser)
    {
        orgUser.Permissions = null;
        orgUser.AccessSecretsManager = true;
        orgUser.AccessPam = true;

        var result = new OrganizationUserResponseModel(orgUser);

        Assert.True(result.AccessSecretsManager);
        Assert.True(result.AccessPam);
    }

    [Theory, BitAutoData]
    public void Constructor_OrganizationUserUserDetails_PopulatesAccessFlags(OrganizationUserUserDetails orgUser)
    {
        orgUser.Permissions = null;
        orgUser.AccessSecretsManager = true;
        orgUser.AccessPam = true;

        var result = new OrganizationUserResponseModel(orgUser);

        Assert.True(result.AccessSecretsManager);
        Assert.True(result.AccessPam);
    }

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

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public void Constructor_OrganizationUser_NonCustomRole_OmitsPermissions(OrganizationUserType type,
        OrganizationUser orgUser)
    {
        orgUser.Type = type;
        orgUser.SetPermissions(new Permissions { ManageUsers = true });

        var result = new OrganizationUserResponseModel(orgUser);

        Assert.Null(result.Permissions);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public void Constructor_OrganizationUserUserDetails_NonCustomRole_OmitsPermissions(OrganizationUserType type,
        OrganizationUserUserDetails orgUser)
    {
        orgUser.Type = type;
        orgUser.Permissions = CoreHelpers.ClassToJsonData(new Permissions { ManageUsers = true });

        var result = new OrganizationUserResponseModel(orgUser);

        Assert.Null(result.Permissions);
    }

    [Theory, BitAutoData]
    public void Constructor_OrganizationUser_CustomRole_ReturnsPermissions(OrganizationUser orgUser)
    {
        orgUser.Type = OrganizationUserType.Custom;
        orgUser.SetPermissions(new Permissions { ManageUsers = true });

        var result = new OrganizationUserResponseModel(orgUser);

        Assert.True(result.Permissions.ManageUsers);
    }

    [Theory, BitAutoData]
    public void Constructor_OrganizationUserUserDetails_CustomRole_ReturnsPermissions(
        OrganizationUserUserDetails orgUser)
    {
        orgUser.Type = OrganizationUserType.Custom;
        orgUser.Permissions = CoreHelpers.ClassToJsonData(new Permissions { ManageUsers = true });

        var result = new OrganizationUserResponseModel(orgUser);

        Assert.True(result.Permissions.ManageUsers);
    }
}
