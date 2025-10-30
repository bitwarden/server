﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    public async Task ValidateAsync_WithValidAcceptedUser_ReturnsValidResult(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;
        organization.PlanType = PlanType.EnterpriseAnnually;

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(userId)
            .Returns(0);

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
    public async Task ValidateAsync_WithInvitedUser_ReturnsUserIsNotAcceptedError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Invited)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserIsNotAccepted>(result.AsError);
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

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserIdIsInvalid>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_FreeOrgUserIsAdminOfAnotherFreeOrg_ReturnsError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [OrganizationCustomize(PlanType = PlanType.Free)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(userId)
            .Returns(1); // User is admin/owner of another free org

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserToConfirmIsAnAdminOrOwnerOfAnotherFreeOrganization>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserWithout2FA_And2FARequired_ReturnsError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        var twoFactorPolicyDetails = new OrganizationUserPolicyDetails
        {
            OrganizationId = organization.Id,
            PolicyType = PolicyType.TwoFactorAuthentication,
            PolicyEnabled = true
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(userId)
            .Returns(0);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, false)]);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(false);

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.TwoFactorAuthentication)
            .Returns([twoFactorPolicyDetails]);

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
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(userId)
            .Returns(0);

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
    public async Task ValidateAsync_UserInMultipleOrgs_WithSingleOrgPolicyOnThisOrg_ReturnsError(
        SutProvider<AutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        var singleOrgPolicyDetails = new OrganizationUserPolicyDetails
        {
            OrganizationId = organization.Id,
            PolicyType = PolicyType.SingleOrg,
            PolicyEnabled = true
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(userId)
            .Returns(0);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser, otherOrgUser]);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(false);

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg)
            .Returns([singleOrgPolicyDetails]);

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
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        var singleOrgPolicyDetails = new OrganizationUserPolicyDetails
        {
            OrganizationId = Guid.NewGuid(), // Different org
            PolicyType = PolicyType.SingleOrg,
            PolicyEnabled = true
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(userId)
            .Returns(0);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser, otherOrgUser]);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(false);

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg)
            .Returns([singleOrgPolicyDetails]);

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
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(userId)
            .Returns(0);

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
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        Guid userId)
    {
        // Arrange
        organizationUser.UserId = userId;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser,
            Organization = organization,
            PerformedBy = Substitute.For<IActingUser>(),
            PerformedOn = DateTimeOffset.UtcNow,
            Key = "test-key",
            DefaultUserCollectionName = "test-collection"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(userId)
            .Returns(0);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, true)]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns([organizationUser, otherOrgUser]);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(false);

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg)
            .Returns([]);

        // Create a real instance with no policies
        var policyRequirement = new SingleOrganizationPolicyRequirement([]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(userId)
            .Returns(policyRequirement);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }
}
