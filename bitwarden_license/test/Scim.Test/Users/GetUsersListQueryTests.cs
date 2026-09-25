using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Scim.Models;
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

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, new GetUsersQueryParamModel { Count = count, StartIndex = startIndex });

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        Assert.Equal(organizationUserUserDetails.Count, result.totalResults);
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

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, new GetUsersQueryParamModel { Filter = filter });

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        Assert.Equal(expectedTotalResults, result.totalResults);
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

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, new GetUsersQueryParamModel { Filter = filter });

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

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, new GetUsersQueryParamModel { Filter = filter });

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        Assert.Equal(expectedTotalResults, result.totalResults);
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

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, new GetUsersQueryParamModel { Filter = filter });

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedUserList, result.userList);
        AssertHelper.AssertPropertyEqual(expectedTotalResults, result.totalResults);
    }

    [Theory]
    [BitAutoData("user1@example.com")]
    public async Task GetUsersList_FilterUserName_Ne_Success(string email, SutProvider<GetUsersListQuery> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        organizationUserUserDetails.First().Email = email;
        string filter = $"userName ne \"{email}\"";

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, new GetUsersQueryParamModel { Filter = filter });

        Assert.Equal(organizationUserUserDetails.Count - 1, result.totalResults);
        Assert.DoesNotContain(result.userList, u => u.Email == email);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsersList_FilterUserName_Co_Success(SutProvider<GetUsersListQuery> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        organizationUserUserDetails.First().Email = "test-contains@example.com";
        string filter = "userName co \"contains\"";

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, new GetUsersQueryParamModel { Filter = filter });

        Assert.Single(result.userList);
        Assert.Equal("test-contains@example.com", result.userList.First().Email);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsersList_FilterUserName_Sw_Success(SutProvider<GetUsersListQuery> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        organizationUserUserDetails.First().Email = "prefix-user@example.com";
        string filter = "userName sw \"prefix-\"";

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, new GetUsersQueryParamModel { Filter = filter });

        Assert.Single(result.userList);
        Assert.Equal("prefix-user@example.com", result.userList.First().Email);
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
