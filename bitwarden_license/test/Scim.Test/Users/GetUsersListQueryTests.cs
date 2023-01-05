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
public class GetUsersListQueryTests
{
    [Theory]
    [BitAutoData(10, 1)]
    [BitAutoData(2, 1)]
    [BitAutoData(1, 3)]
    public async Task GetUsersList_Success(int count, int startIndex, SutProvider<GetUsersListQuery> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, null, count, startIndex);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(organizationUserUserDetails.Skip(startIndex - 1).Take(count).ToList(), result.userList);
        AssertHelper.AssertPropertyEqual(organizationUserUserDetails.Count, result.totalResults);
    }

    [Theory]
    [BitAutoData("user1@example.com")]
    public async Task GetUsersList_FilterUserName_Success(string email, SutProvider<GetUsersListQuery> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        organizationUserUserDetails.First().Email = email;
        string filter = $"userName eq {email}";

        var expectedUserList = organizationUserUserDetails
            .Where(u => u.Email == email)
            .ToList();
        var expectedTotalResults = expectedUserList.Count;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedUserList, result.userList);
        AssertHelper.AssertPropertyEqual(expectedTotalResults, result.totalResults);
    }

    [Theory]
    [BitAutoData("user1@example.com")]
    public async Task GetUsersList_FilterUserName_Empty(string email, SutProvider<GetUsersListQuery> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        string filter = $"userName eq {email}";

        var expectedUserList = new List<OrganizationUserUserDetails>();
        var expectedTotalResults = expectedUserList.Count;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedUserList, result.userList);
        AssertHelper.AssertPropertyEqual(expectedTotalResults, result.totalResults);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsersList_FilterExternalId_Success(SutProvider<GetUsersListQuery> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        string externalId = organizationUserUserDetails.First().ExternalId;
        string filter = $"externalId eq {externalId}";

        var expectedUserList = organizationUserUserDetails
            .Where(u => u.ExternalId == externalId)
            .ToList();
        var expectedTotalResults = expectedUserList.Count;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedUserList, result.userList);
        AssertHelper.AssertPropertyEqual(expectedTotalResults, result.totalResults);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsersList_FilterExternalId_Empty(string externalId, SutProvider<GetUsersListQuery> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        string filter = $"externalId eq {externalId}";

        var expectedUserList = organizationUserUserDetails
            .Where(u => u.ExternalId == externalId)
            .ToList();
        var expectedTotalResults = expectedUserList.Count;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedUserList, result.userList);
        AssertHelper.AssertPropertyEqual(expectedTotalResults, result.totalResults);
    }

    private IList<OrganizationUserUserDetails> SetUsersOrganizationId(IList<OrganizationUserUserDetails> organizationUserUserDetails, Guid organizationId)
    {
        return organizationUserUserDetails.Select(ouud =>
        {
            ouud.OrganizationId = organizationId;
            return ouud;
        }).ToList();
    }
}
