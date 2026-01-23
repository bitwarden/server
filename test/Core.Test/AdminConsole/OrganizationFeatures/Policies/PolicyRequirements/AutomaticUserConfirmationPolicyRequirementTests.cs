using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class AutomaticUserConfirmationPolicyRequirementTests
{
    [Theory]
    [InlineData(OrganizationUserStatusType.Accepted)]
    [InlineData(OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusType.Revoked)]
    public void CannotGrantEmergencyAccess_WithActiveStatus_ReturnsTrue(OrganizationUserStatusType status)
    {
        var policyDetails = new[]
        {
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                PolicyType = PolicyType.AutomaticUserConfirmation,
                OrganizationUserStatus = status
            }
        };

        var sut = new AutomaticUserConfirmationPolicyRequirement(policyDetails);

        Assert.True(sut.CannotGrantEmergencyAccess());
    }

    [Fact]
    public void CannotGrantEmergencyAccess_WithInvitedStatus_ReturnsFalse()
    {
        var policyDetails = new[]
        {
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                PolicyType = PolicyType.AutomaticUserConfirmation,
                OrganizationUserStatus = OrganizationUserStatusType.Invited
            }
        };

        var sut = new AutomaticUserConfirmationPolicyRequirement(policyDetails);

        Assert.False(sut.CannotGrantEmergencyAccess());
    }

    [Fact]
    public void CannotGrantEmergencyAccess_WithNoPolicies_ReturnsFalse()
    {
        var sut = new AutomaticUserConfirmationPolicyRequirement([]);

        Assert.False(sut.CannotGrantEmergencyAccess());
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Accepted)]
    [InlineData(OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusType.Revoked)]
    public void CannotBeGrantedEmergencyAccess_WithActiveStatus_ReturnsTrue(OrganizationUserStatusType status)
    {
        var policyDetails = new[]
        {
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                PolicyType = PolicyType.AutomaticUserConfirmation,
                OrganizationUserStatus = status
            }
        };

        var sut = new AutomaticUserConfirmationPolicyRequirement(policyDetails);

        Assert.True(sut.CannotBeGrantedEmergencyAccess());
    }

    [Fact]
    public void CannotBeGrantedEmergencyAccess_WithInvitedStatus_ReturnsFalse()
    {
        var policyDetails = new[]
        {
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                PolicyType = PolicyType.AutomaticUserConfirmation,
                OrganizationUserStatus = OrganizationUserStatusType.Invited
            }
        };

        var sut = new AutomaticUserConfirmationPolicyRequirement(policyDetails);

        Assert.False(sut.CannotBeGrantedEmergencyAccess());
    }

    [Fact]
    public void CannotBeGrantedEmergencyAccess_WithNoPolicies_ReturnsFalse()
    {
        var sut = new AutomaticUserConfirmationPolicyRequirement([]);

        Assert.False(sut.CannotBeGrantedEmergencyAccess());
    }

    [Fact]
    public void CannotGrantEmergencyAccess_WithMultiplePolicies_OneActive_ReturnsTrue()
    {
        var policyDetails = new[]
        {
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                PolicyType = PolicyType.AutomaticUserConfirmation,
                OrganizationUserStatus = OrganizationUserStatusType.Invited
            },
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                PolicyType = PolicyType.AutomaticUserConfirmation,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed
            }
        };

        var sut = new AutomaticUserConfirmationPolicyRequirement(policyDetails);

        Assert.True(sut.CannotGrantEmergencyAccess());
    }

    [Fact]
    public void CannotBeGrantedEmergencyAccess_WithMultiplePolicies_OneActive_ReturnsTrue()
    {
        var policyDetails = new[]
        {
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                PolicyType = PolicyType.AutomaticUserConfirmation,
                OrganizationUserStatus = OrganizationUserStatusType.Invited
            },
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                PolicyType = PolicyType.AutomaticUserConfirmation,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed
            }
        };

        var sut = new AutomaticUserConfirmationPolicyRequirement(policyDetails);

        Assert.True(sut.CannotBeGrantedEmergencyAccess());
    }
}
