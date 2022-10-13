using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Scim.Users;
using Bit.Scim.Utilities;
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
        var expectedResult = new Models.ScimUserResponseModel
        {
            Id = organizationUserUserDetails.Id.ToString(),
            UserName = organizationUserUserDetails.Email,
            Name = new Models.BaseScimUserModel.NameModel(organizationUserUserDetails.Name),
            Emails = new List<Models.BaseScimUserModel.EmailModel> { new Models.BaseScimUserModel.EmailModel(organizationUserUserDetails.Email) },
            DisplayName = organizationUserUserDetails.Name,
            Active = organizationUserUserDetails.Status != Core.Enums.OrganizationUserStatusType.Revoked ? true : false,
            Groups = new List<string>(),
            ExternalId = organizationUserUserDetails.ExternalId,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByIdAsync(organizationUserUserDetails.Id)
            .Returns(organizationUserUserDetails);

        var result = await sutProvider.Sut.GetUserAsync(organizationUserUserDetails.OrganizationId, organizationUserUserDetails.Id);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetDetailsByIdAsync(organizationUserUserDetails.Id);
        AssertHelper.AssertPropertyEqual(expectedResult, result);
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
