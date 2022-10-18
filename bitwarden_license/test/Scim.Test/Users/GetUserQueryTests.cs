using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Scim.Users;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Users;

[SutProviderCustomize]
public class GetUserQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetUser_Success(SutProvider<GetUserQuery> sutProvider, OrganizationUserUserDetails organizationUserUserDetails)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByIdAsync(organizationUserUserDetails.Id)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUserAsync(organizationUserUserDetails.OrganizationId, organizationUserUserDetails.Id);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetDetailsByIdAsync(organizationUserUserDetails.Id);
        AssertHelper.AssertPropertyEqual(organizationUserUserDetails, result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUser_NotFound_Throws(SutProvider<GetUserQuery> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetUserAsync(organizationId, organizationUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetUser_MismatchingOrganizationId_Throws(SutProvider<GetUserQuery> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = Guid.NewGuid()
            });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetUserAsync(organizationId, organizationUserId));
    }
}
