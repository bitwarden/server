using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class InitPendingOrganizationValidatorTests
{
    private readonly IOrgUserInviteTokenableFactory _orgUserInviteTokenableFactory = Substitute.For<IOrgUserInviteTokenableFactory>();
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    [Theory, BitAutoData]
    public void ValidateInviteToken_ValidToken_ReturnsTrue(
        User user,
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        orgUser.Email = user.Email;
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        _orgUserInviteTokenableFactory.CreateToken(orgUser).Returns(new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
        });

        var orgUserInviteTokenable = _orgUserInviteTokenableFactory.CreateToken(orgUser);
        var protectedToken = _orgUserInviteTokenDataFactory.Protect(orgUserInviteTokenable);

        // Act
        var result = sutProvider.Sut.ValidateInviteToken(orgUser, user, protectedToken);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public void ValidateInviteToken_InvalidToken_ReturnsFalse(
        User user,
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        // Act
        var result = sutProvider.Sut.ValidateInviteToken(orgUser, user, "invalid-token");

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public void ValidateUserEmail_MatchingEmail_ReturnsNull(
        User user,
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        orgUser.Email = user.Email;

        // Act
        var result = sutProvider.Sut.ValidateUserEmail(orgUser, user);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public void ValidateUserEmail_MismatchedEmail_ReturnsError(
        User user,
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        orgUser.Email = "different@example.com";
        user.Email = "user@example.com";

        // Act
        var result = sutProvider.Sut.ValidateUserEmail(orgUser, user);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<EmailMismatchError>(result);
    }

    [Theory, BitAutoData]
    public void ValidateUserEmail_NullOrgUserEmail_ReturnsError(
        User user,
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        orgUser.Email = null;

        // Act
        var result = sutProvider.Sut.ValidateUserEmail(orgUser, user);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<EmailMismatchError>(result);
    }

    [Theory, BitAutoData]
    public void ValidateOrganizationState_ValidState_ReturnsNull(
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = null;
        org.PrivateKey = null;

        // Act
        var result = sutProvider.Sut.ValidateOrganizationState(org);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public void ValidateOrganizationState_OrgEnabled_ReturnsError(
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        org.Enabled = true;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = null;
        org.PrivateKey = null;

        // Act
        var result = sutProvider.Sut.ValidateOrganizationState(org);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OrganizationAlreadyEnabledError>(result);
    }

    [Theory, BitAutoData]
    public void ValidateOrganizationState_OrgNotPending_ReturnsError(
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        org.Enabled = false;
        org.Status = OrganizationStatusType.Created;
        org.PublicKey = null;
        org.PrivateKey = null;

        // Act
        var result = sutProvider.Sut.ValidateOrganizationState(org);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OrganizationNotPendingError>(result);
    }

    [Theory, BitAutoData]
    public void ValidateOrganizationState_OrgHasKeys_ReturnsError(
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = "existing-public-key";
        org.PrivateKey = "existing-private-key";

        // Act
        var result = sutProvider.Sut.ValidateOrganizationState(org);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OrganizationHasKeysError>(result);
    }

    [Theory, BitAutoData]
    public void ValidateOrganizationMatch_Matching_ReturnsNull(
        OrganizationUser orgUser,
        Guid organizationId,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        orgUser.OrganizationId = organizationId;

        // Act
        var result = sutProvider.Sut.ValidateOrganizationMatch(orgUser, organizationId);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public void ValidateOrganizationMatch_NotMatching_ReturnsError(
        OrganizationUser orgUser,
        Guid organizationId,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        orgUser.OrganizationId = Guid.NewGuid();

        // Act
        var result = sutProvider.Sut.ValidateOrganizationMatch(orgUser, organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OrganizationMismatchError>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidatePoliciesAsync_AllPoliciesPass_ReturnsNull(
        User user,
        Guid organizationId,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg)
            .Returns(false);

        var twoFactorReq = new RequireTwoFactorPolicyRequirement(Enumerable.Empty<PolicyDetails>());
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(twoFactorReq);

        // Act
        var result = await sutProvider.Sut.ValidatePoliciesAsync(user, organizationId);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task ValidatePoliciesAsync_SingleOrgPolicyViolation_ReturnsError(
        User user,
        Guid organizationId,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidatePoliciesAsync(user, organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<SingleOrgPolicyViolationError>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidatePoliciesAsync_TwoFactorRequired_UserDoesNotHave2FA_ReturnsError(
        User user,
        Guid organizationId,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg)
            .Returns(false);

        var policyDetails = new PolicyDetails
        {
            OrganizationId = organizationId,
            PolicyType = PolicyType.TwoFactorAuthentication,
            OrganizationUserType = OrganizationUserType.Owner,
            OrganizationUserStatus = OrganizationUserStatusType.Invited
        };

        var twoFactorReq = new RequireTwoFactorPolicyRequirement(new[] { policyDetails });
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(twoFactorReq);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.ValidatePoliciesAsync(user, organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorRequiredError>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateBusinessRulesAsync_PaidOrg_ReturnsNull(
        User user,
        Organization org,
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Type = OrganizationUserType.Owner;

        // Act
        var result = await sutProvider.Sut.ValidateBusinessRulesAsync(user, org, orgUser);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateBusinessRulesAsync_FreeOrgNonAdmin_ReturnsNull(
        User user,
        Organization org,
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.Free;
        orgUser.Type = OrganizationUserType.User;

        // Act
        var result = await sutProvider.Sut.ValidateBusinessRulesAsync(user, org, orgUser);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateBusinessRulesAsync_FreeOrgAdminNoExisting_ReturnsNull(
        User user,
        Organization org,
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.Free;
        orgUser.Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.ValidateBusinessRulesAsync(user, org, orgUser);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateBusinessRulesAsync_FreeOrgAdminLimitExceeded_ReturnsError(
        User user,
        Organization org,
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.Free;
        orgUser.Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(1);

        // Act
        var result = await sutProvider.Sut.ValidateBusinessRulesAsync(user, org, orgUser);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<FreeOrgAdminLimitError>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidatePoliciesAsync_AutoConfirmPolicyViolation_ReturnsError(
        User user,
        Guid organizationId,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(true);

        var policyDetails = new PolicyDetails
        {
            OrganizationId = organizationId,
            PolicyType = PolicyType.AutomaticUserConfirmation,
            OrganizationUserType = OrganizationUserType.Owner,
            OrganizationUserStatus = OrganizationUserStatusType.Invited
        };

        var autoConfirmReq = new AutomaticUserConfirmationPolicyRequirement(new[] { policyDetails });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(autoConfirmReq);

        // Act
        var result = await sutProvider.Sut.ValidatePoliciesAsync(user, organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<SingleOrgPolicyViolationError>(result);
    }
}
