using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using NSubstitute;
using Xunit;

namespace Api.Test.AdminConsole.Queries;

[SutProviderCustomize]
public class OrganizationUserUserDetailsQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task Get_HandlesNullPermissionsObject(
        ICollection<OrganizationUserUserDetails> organizationUsers,
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider,
        Guid organizationId
    )
    {
        Get_Setup(organizationUsers, sutProvider, organizationId);
        organizationUsers.First().Permissions = "null";
        var response = await sutProvider.Sut.GetOrganizationUserUserDetails(
            new OrganizationUserUserDetailsQueryRequest { OrganizationId = organizationId }
        );

        Assert.True(response.All(r => organizationUsers.Any(ou => ou.Id == r.Id)));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_ReturnsUsers(
        ICollection<OrganizationUserUserDetails> organizationUsers,
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider,
        Guid organizationId
    )
    {
        Get_Setup(organizationUsers, sutProvider, organizationId);
        var response = await sutProvider.Sut.GetOrganizationUserUserDetails(
            new OrganizationUserUserDetailsQueryRequest { OrganizationId = organizationId }
        );

        Assert.True(response.All(r => organizationUsers.Any(ou => ou.Id == r.Id)));
    }

    private void Get_Setup(
        ICollection<OrganizationUserUserDetails> organizationUsers,
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider,
        Guid organizationId
    )
    {
        foreach (var orgUser in organizationUsers)
        {
            orgUser.Permissions = null;
        }

        sutProvider
            .GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId, Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(organizationUsers);
    }
}
