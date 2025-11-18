using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUsers;

[SutProviderCustomize]
public class AutomaticallyConfirmOrganizationUsersValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithNullOrganizationUser_ReturnsUserNotFoundError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization)
    {
        // Arrange
        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = null,
            OrganizationUserId = Guid.NewGuid(),
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserNotFoundError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithNullUserId_ReturnsUserNotFoundError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser)
    {
        // Arrange
        organizationUser.UserId = null;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserNotFoundError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithNullOrganization_ReturnsOrganizationNotFoundError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = null,
            OrganizationId = organizationUser.OrganizationId,
            Key = "test-key"
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotFound>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithValidAcceptedUser_ReturnsValidResult(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true, planType: PlanType.EnterpriseAnnually)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy autoConfirmPolicy)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(request, result.Request);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithMismatchedOrganizationId_ReturnsOrganizationUserIdIsInvalidError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = Guid.NewGuid(); // Different from organization.Id

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserIdIsInvalid>(result.AsError);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public async Task ValidateAsync_WithNotAcceptedStatus_ReturnsUserIsNotAcceptedError(
        OrganizationUserStatusType statusType,
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Status = statusType;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserIsNotAccepted>(result.AsError);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Custom)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task ValidateAsync_WithNonUserType_ReturnsUserIsNotUserTypeError(
        OrganizationUserType userType,
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Type = userType;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserIsNotUserType>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserWithout2FA_And2FARequired_ReturnsError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy autoConfirmPolicy)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        var twoFactorPolicyDetails = new PolicyDetails
        {
            OrganizationId = organization.Id,
            PolicyType = PolicyType.TwoFactorAuthentication
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, false)]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(userId)
            .Returns(new RequireTwoFactorPolicyRequirement([twoFactorPolicyDetails]));

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserDoesNotHaveTwoFactorEnabled>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserWith2FA_ReturnsValidResult(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy autoConfirmPolicy)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserWithout2FA_And2FANotRequired_ReturnsValidResult(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy autoConfirmPolicy)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, false)]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(userId)
            .Returns(new RequireTwoFactorPolicyRequirement([])); // No 2FA policy

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserInMultipleOrgs_WithSingleOrgPolicyOnThisOrg_ReturnsError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy autoConfirmPolicy)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        var singleOrgPolicyDetails = new PolicyDetails
        {
            OrganizationId = organization.Id,
            PolicyType = PolicyType.SingleOrg
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser, otherOrgUser]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(userId)
            .Returns(new SingleOrganizationPolicyRequirement([singleOrgPolicyDetails]));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationEnforcesSingleOrgPolicy>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserInMultipleOrgs_WithSingleOrgPolicyOnOtherOrg_ReturnsError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy autoConfirmPolicy)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        var otherOrgId = Guid.NewGuid(); // Different org
        var singleOrgPolicyDetails = new PolicyDetails
        {
            OrganizationId = otherOrgId,
            PolicyType = PolicyType.SingleOrg,
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser, otherOrgUser]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(userId)
            .Returns(new SingleOrganizationPolicyRequirement([singleOrgPolicyDetails]));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OtherOrganizationEnforcesSingleOrgPolicy>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserInSingleOrg_ReturnsValidResult(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy autoConfirmPolicy)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]); // Single org

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserInMultipleOrgs_WithNoSingleOrgPolicy_ReturnsValidResult(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        Guid userId,
        Policy autoConfirmPolicy)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;
        autoConfirmPolicy.Type = PolicyType.AutomaticUserConfirmation;
        autoConfirmPolicy.Enabled = true;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser, otherOrgUser]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(userId)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithAutoConfirmPolicyDisabled_ReturnsAutoConfirmPolicyNotEnabledError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns((Policy)null);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<AutomaticallyConfirmUsersPolicyIsNotEnabled>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithOrganizationUseAutomaticUserConfirmationDisabled_ReturnsAutoConfirmPolicyNotEnabledError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: false)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy autoConfirmPolicy)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = "test-collection",
            OrganizationUser = organizationUser,
            OrganizationUserId = organizationUser.Id,
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<AutomaticallyConfirmUsersPolicyIsNotEnabled>(result.AsError);
    }
}
