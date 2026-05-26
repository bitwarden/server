using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;

[SutProviderCustomize]
public class UpdateUserResetPasswordEnrollmentCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenKeyIsProvided_EnrollsUser(
        Guid organizationId, Guid callingUserId, string resetPasswordKey,
        OrganizationUser orgUser, Organization org,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        SetupValidRequest(sutProvider, organizationId, callingUserId, orgUser, org);

        await sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
            organizationId, callingUserId, resetPasswordKey, callingUserId);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(orgUser);
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            orgUser, EventType.OrganizationUser_ResetPassword_Enroll);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenKeyIsNull_WithdrawsUser(
        Guid organizationId, Guid callingUserId,
        OrganizationUser orgUser, Organization org,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        SetupValidRequest(sutProvider, organizationId, callingUserId, orgUser, org);

        await sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
            organizationId, callingUserId, null, callingUserId);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(orgUser);
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            orgUser, EventType.OrganizationUser_ResetPassword_Withdraw);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenUserNotFound_ThrowsBadRequest(
        Guid organizationId, Guid callingUserId, string resetPasswordKey,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, callingUserId)
            .Returns((OrganizationUser)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
                organizationId, callingUserId, resetPasswordKey, callingUserId));

        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenCallingUserIdIsNull_ThrowsBadRequest(
        Guid organizationId, Guid userId, string resetPasswordKey,
        OrganizationUser orgUser,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organizationId;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
                organizationId, userId, resetPasswordKey, null));

        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenUserIdMismatch_ThrowsBadRequest(
        Guid organizationId, Guid callingUserId, string resetPasswordKey,
        OrganizationUser orgUser,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        orgUser.UserId = Guid.NewGuid();
        orgUser.OrganizationId = organizationId;
        SetupOrgUser(sutProvider, organizationId, callingUserId, orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
                organizationId, callingUserId, resetPasswordKey, callingUserId));

        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenOrganizationIdMismatch_ThrowsBadRequest(
        Guid organizationId, Guid callingUserId, string resetPasswordKey,
        OrganizationUser orgUser,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        orgUser.UserId = callingUserId;
        orgUser.OrganizationId = Guid.NewGuid();
        SetupOrgUser(sutProvider, organizationId, callingUserId, orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
                organizationId, callingUserId, resetPasswordKey, callingUserId));

        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenOrganizationNotFound_ThrowsBadRequest(
        Guid organizationId, Guid callingUserId, string resetPasswordKey,
        OrganizationUser orgUser,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        orgUser.UserId = callingUserId;
        orgUser.OrganizationId = organizationId;
        SetupOrgUser(sutProvider, organizationId, callingUserId, orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
                organizationId, callingUserId, resetPasswordKey, callingUserId));

        Assert.Contains("Organization does not allow password reset enrollment.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenOrgDoesNotUseResetPassword_ThrowsBadRequest(
        Guid organizationId, Guid callingUserId, string resetPasswordKey,
        OrganizationUser orgUser, Organization org,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        orgUser.UserId = callingUserId;
        orgUser.OrganizationId = organizationId;
        org.UseResetPassword = false;
        SetupOrgUser(sutProvider, organizationId, callingUserId, orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
                organizationId, callingUserId, resetPasswordKey, callingUserId));

        Assert.Contains("Organization does not allow password reset enrollment.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenPolicyNotEnabled_ThrowsBadRequest(
        Guid organizationId, Guid callingUserId, string resetPasswordKey,
        OrganizationUser orgUser, Organization org,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        orgUser.UserId = callingUserId;
        orgUser.OrganizationId = organizationId;
        org.UseResetPassword = true;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, callingUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(org);

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organizationId, PolicyType.ResetPassword)
            .Returns(new PolicyStatus(organizationId, PolicyType.ResetPassword,
                new Policy { Enabled = false }));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
                organizationId, callingUserId, resetPasswordKey, callingUserId));

        Assert.Contains("Organization does not have the password reset policy enabled.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserResetPasswordEnrollmentAsync_WhenAutoEnrollEnabledAndKeyIsNull_ThrowsBadRequest(
        Guid organizationId, Guid callingUserId,
        OrganizationUser orgUser, Organization org,
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider)
    {
        var policyData = CoreHelpers.ClassToJsonData(new ResetPasswordDataModel { AutoEnrollEnabled = true });
        SetupValidRequest(sutProvider, organizationId, callingUserId, orgUser, org, policyData);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateUserResetPasswordEnrollmentAsync(
                organizationId, callingUserId, null, callingUserId));

        Assert.Contains("Due to an Enterprise Policy, you are not allowed to withdraw from account recovery.", exception.Message);
    }

    private static void SetupOrgUser(
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider,
        Guid organizationId, Guid callingUserId, OrganizationUser orgUser)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, callingUserId)
            .Returns(orgUser);
    }

    private static void SetupValidRequest(
        SutProvider<UpdateUserResetPasswordEnrollmentCommand> sutProvider,
        Guid organizationId,
        Guid callingUserId,
        OrganizationUser orgUser,
        Organization org,
        string? policyData = null)
    {
        orgUser.UserId = callingUserId;
        orgUser.OrganizationId = organizationId;
        org.UseResetPassword = true;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, callingUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(org);

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organizationId, PolicyType.ResetPassword)
            .Returns(new PolicyStatus(organizationId, PolicyType.ResetPassword,
                new Policy { Enabled = true, Data = policyData }));
    }
}
