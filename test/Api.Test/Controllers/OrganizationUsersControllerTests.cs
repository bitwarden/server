using System.Security.Claims;
using Bit.Api.Controllers;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.Policies;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(OrganizationUsersController))]
[SutProviderCustomize]
public class OrganizationUsersControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Accept_RequiresKnownUser(Guid orgId, Guid orgUserId, OrganizationUserAcceptRequestModel model,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs((User)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.Accept(orgId, orgUserId, model));
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_NoMasterPasswordReset(Guid orgId, Guid orgUserId,
        OrganizationUserAcceptRequestModel model, User user, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);

        await sutProvider.Sut.Accept(orgId, orgUserId, model);

        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .AcceptUserAsync(orgUserId, user, model.Token, sutProvider.GetDependency<IUserService>());
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceiveWithAnyArgs()
            .UpdateUserResetPasswordEnrollmentAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_RequireMasterPasswordReset(Guid orgId, Guid orgUserId,
        OrganizationUserAcceptRequestModel model, User user, SutProvider<OrganizationUsersController> sutProvider)
    {
        var policy = new Policy
        {
            Enabled = true,
            Data = CoreHelpers.ClassToJsonData(new ResetPasswordDataModel { AutoEnrollEnabled = true, }),
        };
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(orgId,
            Core.Enums.PolicyType.ResetPassword).Returns(policy);

        await sutProvider.Sut.Accept(orgId, orgUserId, model);

        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .AcceptUserAsync(orgUserId, user, model.Token, sutProvider.GetDependency<IUserService>());
        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .UpdateUserResetPasswordEnrollmentAsync(orgId, user.Id, model.ResetPasswordKey, user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithoutManageUsersPermission_Throws(Guid orgId, Guid orgUserId, OrganizationUserUpdateRequestModel model, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(orgId, orgUserId, model));
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithNullOrgUser_Throws(Guid orgId, Guid orgUserId, OrganizationUserUpdateRequestModel model, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(orgId, orgUserId, model));
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithNonMatchingOrgId_Throws(Guid orgId, OrganizationUser orgUser, OrganizationUserUpdateRequestModel model, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(orgId, orgUser.Id, model));
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success(Guid orgId, OrganizationUser orgUser, OrganizationUserUpdateRequestModel model, Guid? savingUserId, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.OrganizationId = orgId;

        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(savingUserId);

        await sutProvider.Sut.Put(orgId, orgUser.Id, model);

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageUsers(orgId);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetByIdAsync(orgUser.Id);
        sutProvider.GetDependency<IUserService>().Received(1).GetProperUserId(Arg.Any<ClaimsPrincipal>());
        await sutProvider.GetDependency<IUpdateOrganizationUserCommand>().Received(1).UpdateUserAsync(
            Arg.Is<OrganizationUser>(ou => ou.Id == orgUser.Id && ou.OrganizationId == orgId),
            savingUserId,
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            model.Groups);
    }
}
