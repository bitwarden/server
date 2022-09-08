using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
public class PutUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task PutUser_Restore_Success(SutProvider<PutUserCommand> sutProvider, OrganizationUser organizationUser, OrganizationUserUserDetails organizationUserUserDetails)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            Active = true,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        organizationUser.Status = Core.Enums.OrganizationUserStatusType.Revoked;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByIdAsync(organizationUser.Id)
            .Returns(organizationUserUserDetails);

        var expectedResult = new ScimUserResponseModel(organizationUserUserDetails);

        var result = await sutProvider.Sut.PutUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimUserRequestModel);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetByIdAsync(organizationUser.Id);
        await sutProvider.GetDependency<IOrganizationService>().Received(1).RestoreUserAsync(organizationUser, null, Arg.Any<IUserService>());
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetDetailsByIdAsync(organizationUser.Id);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task PutUser_Revoke_Success(SutProvider<PutUserCommand> sutProvider, OrganizationUser organizationUser, OrganizationUserUserDetails organizationUserUserDetails)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            Active = false,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        organizationUser.Status = Core.Enums.OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByIdAsync(organizationUser.Id)
            .Returns(organizationUserUserDetails);

        var expectedResult = new ScimUserResponseModel(organizationUserUserDetails);

        var result = await sutProvider.Sut.PutUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimUserRequestModel);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetByIdAsync(organizationUser.Id);
        await sutProvider.GetDependency<IOrganizationService>().Received(1).RevokeUserAsync(organizationUser, null);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetDetailsByIdAsync(organizationUser.Id);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task PutUser_NoAction_Success(SutProvider<PutUserCommand> sutProvider, OrganizationUser organizationUser, OrganizationUserUserDetails organizationUserUserDetails)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            Active = true,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        organizationUser.Status = Core.Enums.OrganizationUserStatusType.Accepted;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByIdAsync(organizationUser.Id)
            .Returns(organizationUserUserDetails);

        var expectedResult = new ScimUserResponseModel(organizationUserUserDetails);

        var result = await sutProvider.Sut.PutUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimUserRequestModel);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetByIdAsync(organizationUser.Id);
        await sutProvider.GetDependency<IOrganizationService>().Received(0).RestoreUserAsync(organizationUser, null, Arg.Any<IUserService>());
        await sutProvider.GetDependency<IOrganizationService>().Received(0).RevokeUserAsync(organizationUser, null);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetDetailsByIdAsync(organizationUser.Id);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task PutUser_NotFound_Throws(SutProvider<PutUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            Emails = new List<BaseScimUserModel.EmailModel>(),
            Active = true,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PutUserAsync(organizationId, organizationUserId, scimUserRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PutUser_MismatchingOrganizationId_Throws(SutProvider<PutUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            Emails = new List<BaseScimUserModel.EmailModel>(),
            Active = true,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = Guid.NewGuid()
            });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PutUserAsync(organizationId, organizationUserId, scimUserRequestModel));
    }
}
