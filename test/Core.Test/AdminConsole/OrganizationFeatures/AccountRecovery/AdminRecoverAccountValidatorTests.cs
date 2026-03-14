using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.AccountRecovery;

[SutProviderCustomize]
public class AdminRecoverAccountValidatorTests
{
    // region Error: NoActionRequestedError

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NeitherResetRequested_ReturnsNoActionRequestedError(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = false,
            ResetTwoFactor = false,
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<NoActionRequestedError>(result.AsError);
    }

    // region Error: MissingPasswordFieldsError

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetMasterPassword_NullHash_ReturnsMissingPasswordFieldsError(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = null,
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<MissingPasswordFieldsError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetMasterPassword_EmptyHash_ReturnsMissingPasswordFieldsError(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<MissingPasswordFieldsError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetMasterPassword_NullKey_ReturnsMissingPasswordFieldsError(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = null,
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<MissingPasswordFieldsError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetMasterPassword_EmptyKey_ReturnsMissingPasswordFieldsError(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<MissingPasswordFieldsError>(result.AsError);
    }

    // region Error: FeatureDisabledError

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetTwoFactor_FeatureFlagDisabled_ReturnsFeatureDisabledError(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AdminResetTwoFactor)
            .Returns(false);

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = false,
            ResetTwoFactor = true,
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<FeatureDisabledError>(result.AsError);
    }

    // region Error: OrgDoesNotAllowResetError

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_OrganizationDoesNotExist_ReturnsOrgDoesNotAllowResetError(
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns((Organization)null);

        var request = new RecoverAccountRequest
        {
            OrgId = orgId,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrgDoesNotAllowResetError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_OrganizationDoesNotAllowResetPassword_ReturnsOrgDoesNotAllowResetError(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        organization.UseResetPassword = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrgDoesNotAllowResetError>(result.AsError);
    }

    // region Error: PolicyNotEnabledError

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_PolicyNotEnabled_ReturnsPolicyNotEnabledError(
        Organization organization,
        OrganizationUser organizationUser,
        [Policy(PolicyType.ResetPassword, false)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.ResetPassword)
            .Returns(policy);

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<PolicyNotEnabledError>(result.AsError);
    }

    // region Error: InvalidOrgUserError

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_OrgUserNotConfirmed_ReturnsInvalidOrgUserError(
        Organization organization,
        OrganizationUser organizationUser,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);

        organizationUser.Status = OrganizationUserStatusType.Invited;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId = Guid.NewGuid();

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InvalidOrgUserError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_OrgUserWrongOrganization_ReturnsInvalidOrgUserError(
        Organization organization,
        OrganizationUser organizationUser,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);

        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = Guid.NewGuid(); // Different org
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId = Guid.NewGuid();

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InvalidOrgUserError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_OrgUserNotEnrolledInAccountRecovery_ReturnsInvalidOrgUserError(
        Organization organization,
        OrganizationUser organizationUser,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);

        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.ResetPasswordKey = null; // Not enrolled
        organizationUser.UserId = Guid.NewGuid();

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InvalidOrgUserError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_OrgUserNoUserId_ReturnsInvalidOrgUserError(
        Organization organization,
        OrganizationUser organizationUser,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);

        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId = null;

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InvalidOrgUserError>(result.AsError);
    }

    // region Error: UserNotFoundError

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserDoesNotExist_ReturnsUserNotFoundError(
        Organization organization,
        OrganizationUser organizationUser,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns((User)null);

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserNotFoundError>(result.AsError);
    }

    // region Error: KeyConnectorUserError

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetMasterPassword_UserUsesKeyConnector_ReturnsKeyConnectorUserError(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);

        user.UsesKeyConnector = true;
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(user);

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<KeyConnectorUserError>(result.AsError);
    }

    // region Success paths

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetMasterPasswordOnly_ReturnsValid(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = false,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(request, result.Request);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetTwoFactorOnly_ReturnsValid(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AdminResetTwoFactor)
            .Returns(true);

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = false,
            ResetTwoFactor = true,
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(request, result.Request);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetBothPasswordAndTwoFactor_ReturnsValid(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountValidator> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AdminResetTwoFactor)
            .Returns(true);

        var request = new RecoverAccountRequest
        {
            OrgId = organization.Id,
            OrganizationUser = organizationUser,
            ResetMasterPassword = true,
            ResetTwoFactor = true,
            NewMasterPasswordHash = "some-hash",
            Key = "some-key",
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(request, result.Request);
    }

    // region Helper methods

    private static void SetupValidOrganization(
        SutProvider<AdminRecoverAccountValidator> sutProvider,
        Organization organization)
    {
        organization.UseResetPassword = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }

    private static void SetupValidPolicy(
        SutProvider<AdminRecoverAccountValidator> sutProvider,
        Organization organization,
        PolicyStatus policy)
    {
        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.ResetPassword)
            .Returns(policy);
    }

    private static void SetupValidOrganizationUser(OrganizationUser organizationUser, Guid orgId)
    {
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = orgId;
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId ??= Guid.NewGuid();
    }

    private static void SetupValidUser(
        SutProvider<AdminRecoverAccountValidator> sutProvider,
        User user,
        OrganizationUser organizationUser)
    {
        user.UsesKeyConnector = false;
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(user);
    }
}
