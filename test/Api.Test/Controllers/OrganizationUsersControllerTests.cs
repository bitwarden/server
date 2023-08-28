using System.Security.Claims;
using Bit.Api.Controllers;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.Policies;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
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
    public async Task PutResetPasswordEnrollment_InivitedUser_AcceptsInvite(Guid orgId, Guid userId, OrganizationUserResetPasswordEnrollmentRequestModel model,
        User user, OrganizationUser orgUser, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.Status = Core.Enums.OrganizationUserStatusType.Invited;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(default, default).ReturnsForAnyArgs(orgUser);

        await sutProvider.Sut.PutResetPasswordEnrollment(orgId, userId, model);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).AcceptUserAsync(orgId, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutResetPasswordEnrollment_ConfirmedUser_AcceptsInvite(Guid orgId, Guid userId, OrganizationUserResetPasswordEnrollmentRequestModel model,
        User user, OrganizationUser orgUser, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.Status = Core.Enums.OrganizationUserStatusType.Confirmed;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(default, default).ReturnsForAnyArgs(orgUser);

        await sutProvider.Sut.PutResetPasswordEnrollment(orgId, userId, model);

        await sutProvider.GetDependency<IOrganizationService>().Received(0).AcceptUserAsync(orgId, user, sutProvider.GetDependency<IUserService>());
    }

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
    public async Task Put_WithAccessSecretsManagerFalse_Success(Guid orgId, OrganizationUser orgUser, OrganizationUserUpdateRequestModel model, Guid? savingUserId, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.OrganizationId = orgId;
        orgUser.AccessSecretsManager = false;
        model.AccessSecretsManager = false;

        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(savingUserId);

        await sutProvider.Sut.Put(orgId, orgUser.Id, model);

        await sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>().DidNotReceiveWithAnyArgs()
            .CountNewSmSeatsRequiredAsync(default, default);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>().DidNotReceiveWithAnyArgs().UpdateSubscriptionAsync(default);
        await sutProvider.GetDependency<IUpdateOrganizationUserCommand>().Received(1).UpdateUserAsync(
            Arg.Is<OrganizationUser>(ou => ou.Id == orgUser.Id && ou.OrganizationId == orgId),
            savingUserId,
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            model.Groups);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithAccessSecretsManagerTrue_RequireNewSeat_Success(Organization org, OrganizationUser orgUser, OrganizationUserUpdateRequestModel model, Guid? savingUserId, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.OrganizationId = org.Id;
        orgUser.AccessSecretsManager = false;
        model.AccessSecretsManager = true;

        sutProvider.GetDependency<ICurrentContext>().ManageUsers(org.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(savingUserId);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>().CountNewSmSeatsRequiredAsync(org.Id, 1).Returns(1);

        await sutProvider.Sut.Put(org.Id, orgUser.Id, model);

        await sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>().Received(1).CountNewSmSeatsRequiredAsync(orgUser.OrganizationId, 1);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>().Received(1)
            .ValidateUpdate(Arg.Is<SecretsManagerSubscriptionUpdate>(s => s.Organization == org && s.SmSeats == (org.SmSeats + 1)));
        await sutProvider.GetDependency<IUpdateOrganizationUserCommand>().Received(1).UpdateUserAsync(
            Arg.Is<OrganizationUser>(ou => ou.Id == orgUser.Id && ou.OrganizationId == org.Id),
            savingUserId,
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            model.Groups);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>().Received(1)
            .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(s => s.Organization == org && s.SmSeats == (org.SmSeats + 1)));
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithAccessSecretsManagerTrue_NoNewSeat_Success(Organization org, OrganizationUser orgUser, OrganizationUserUpdateRequestModel model, Guid? savingUserId, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.OrganizationId = org.Id;
        orgUser.AccessSecretsManager = false;
        model.AccessSecretsManager = true;

        sutProvider.GetDependency<ICurrentContext>().ManageUsers(org.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(savingUserId);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>().CountNewSmSeatsRequiredAsync(org.Id, 1).Returns(0);

        await sutProvider.Sut.Put(org.Id, orgUser.Id, model);

        await sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>().Received(1).CountNewSmSeatsRequiredAsync(orgUser.OrganizationId, 1);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>().DidNotReceiveWithAnyArgs().ValidateUpdate(default);
        await sutProvider.GetDependency<IUpdateOrganizationUserCommand>().Received(1).UpdateUserAsync(
            Arg.Is<OrganizationUser>(ou => ou.Id == orgUser.Id && ou.OrganizationId == org.Id),
            savingUserId,
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            model.Groups);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>().DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(default);
    }
}
