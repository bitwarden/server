using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Scim.Commands.Users;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Commands.Users;

[SutProviderCustomize]
public class GetUsersListCommandTests
{
    [Theory]
    [BitAutoData(10, 1)]
    [BitAutoData(2, 1)]
    [BitAutoData(1, 3)]
    public async Task GetUsersList_Success(int? count, int? startIndex, SutProvider<GetUsersListCommand> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);

        var expectedResult = new ScimListResponseModel<ScimUserResponseModel>
        {
            Resources = organizationUserUserDetails
                .OrderBy(ouud => ouud.Email)
                .Skip(startIndex.Value - 1)
                .Take(count.Value)
                .Select(ouud => new Models.ScimUserResponseModel
                {
                    Id = ouud.Id.ToString(),
                    UserName = ouud.Email,
                    Name = new Models.BaseScimUserModel.NameModel(ouud.Name),
                    Emails = new List<Models.BaseScimUserModel.EmailModel> { new Models.BaseScimUserModel.EmailModel(ouud.Email) },
                    DisplayName = ouud.Name,
                    Active = ouud.Status != Core.Enums.OrganizationUserStatusType.Revoked ? true : false,
                    Groups = new List<string>(),
                    ExternalId = ouud.ExternalId,
                    Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
                }).ToList(),
            ItemsPerPage = count.GetValueOrDefault(organizationUserUserDetails.Count),
            TotalResults = organizationUserUserDetails.Count,
            StartIndex = startIndex.GetValueOrDefault(1),
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, null, count, startIndex);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData("user1@example.com")]
    public async Task GetUsersList_FilterUserName_Success(string email, SutProvider<GetUsersListCommand> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        organizationUserUserDetails.First().Email = email;
        string filter = $"userName eq {email}";

        var expectedResult = new ScimListResponseModel<ScimUserResponseModel>
        {
            Resources = organizationUserUserDetails
                .Where(ou => ou.Email.ToLowerInvariant() == email)
                .Select(ouud => new Models.ScimUserResponseModel
                {
                    Id = ouud.Id.ToString(),
                    UserName = ouud.Email,
                    Name = new Models.BaseScimUserModel.NameModel(ouud.Name),
                    Emails = new List<Models.BaseScimUserModel.EmailModel> { new Models.BaseScimUserModel.EmailModel(ouud.Email) },
                    DisplayName = ouud.Name,
                    Active = ouud.Status != Core.Enums.OrganizationUserStatusType.Revoked ? true : false,
                    Groups = new List<string>(),
                    ExternalId = ouud.ExternalId,
                    Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
                }).ToList(),
            ItemsPerPage = 1,
            TotalResults = 1,
            StartIndex = 1,
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData("user1@example.com")]
    public async Task GetUsersList_FilterUserName_Empty(string email, SutProvider<GetUsersListCommand> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        string filter = $"userName eq {email}";

        var expectedResult = new ScimListResponseModel<ScimUserResponseModel>
        {
            Resources = new List<ScimUserResponseModel>(),
            ItemsPerPage = 0,
            TotalResults = 0,
            StartIndex = 1,
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsersList_FilterExternalId_Success(SutProvider<GetUsersListCommand> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        string externalId = organizationUserUserDetails.First().ExternalId;
        string filter = $"externalId eq {externalId}";

        var expectedResult = new ScimListResponseModel<ScimUserResponseModel>
        {
            Resources = organizationUserUserDetails
                .Where(ou => ou.ExternalId == externalId)
                .Select(ouud => new Models.ScimUserResponseModel
                {
                    Id = ouud.Id.ToString(),
                    UserName = ouud.Email,
                    Name = new Models.BaseScimUserModel.NameModel(ouud.Name),
                    Emails = new List<Models.BaseScimUserModel.EmailModel> { new Models.BaseScimUserModel.EmailModel(ouud.Email) },
                    DisplayName = ouud.Name,
                    Active = ouud.Status != Core.Enums.OrganizationUserStatusType.Revoked ? true : false,
                    Groups = new List<string>(),
                    ExternalId = ouud.ExternalId,
                    Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
                }).ToList(),
            ItemsPerPage = 1,
            TotalResults = 1,
            StartIndex = 1,
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsersList_FilterExternalId_Empty(string externalId, SutProvider<GetUsersListCommand> sutProvider, Guid organizationId, IList<OrganizationUserUserDetails> organizationUserUserDetails)
    {
        organizationUserUserDetails = SetUsersOrganizationId(organizationUserUserDetails, organizationId);
        string filter = $"externalId eq {externalId}";

        var expectedResult = new ScimListResponseModel<ScimUserResponseModel>
        {
            Resources = new List<ScimUserResponseModel>(),
            ItemsPerPage = 0,
            TotalResults = 0,
            StartIndex = 1,
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUsersListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyDetailsByOrganizationAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
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
