using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class SendOptionsPolicyRequirementFactoryTests
{
    // [Theory, BitAutoData]
    // public void DisableSend_IsFalse_IfNoDisableSendPolicies(
    //     [PolicyDetails(PolicyType.RequireSso)] PolicyDetails otherPolicy1,
    //     [PolicyDetails(PolicyType.SendOptions)] PolicyDetails otherPolicy2,
    //     SutProvider<SendPolicyRequirementFactory> sutProvider)
    // {
    //     EnableDisableHideEmail(otherPolicy2);
    //
    //     var actual = sutProvider.Create([otherPolicy1, otherPolicy2]);
    //
    //     Assert.False(actual.DisableSend);
    // }
    //
    // [Theory]
    // [InlineAutoData(OrganizationUserType.Owner, false)]
    // [InlineAutoData(OrganizationUserType.Admin, false)]
    // [InlineAutoData(OrganizationUserType.User, true)]
    // [InlineAutoData(OrganizationUserType.Custom, true)]
    // public void DisableSend_TestRoles(
    //     OrganizationUserType userType,
    //     bool shouldBeEnforced,
    //     [PolicyDetails(PolicyType.DisableSend)] PolicyDetails policyDetails)
    // {
    //     policyDetails.OrganizationUserType = userType;
    //
    //     var actual = SendPolicyRequirement.Create([policyDetails]);
    //
    //     Assert.Equal(shouldBeEnforced, actual.DisableSend);
    // }
    //
    // [Theory, AutoData]
    // public void DisableSend_Not_EnforcedAgainstProviders(
    //     [PolicyDetails(PolicyType.DisableSend, isProvider: true)] PolicyDetails policyDetails)
    // {
    //     var actual = SendPolicyRequirement.Create([policyDetails]);
    //
    //     Assert.False(actual.DisableSend);
    // }
    //
    // [Theory]
    // [InlineAutoData(OrganizationUserStatusType.Confirmed, true)]
    // [InlineAutoData(OrganizationUserStatusType.Accepted, true)]
    // [InlineAutoData(OrganizationUserStatusType.Invited, false)]
    // [InlineAutoData(OrganizationUserStatusType.Revoked, false)]
    // public void DisableSend_TestStatuses(
    //     OrganizationUserStatusType userStatus,
    //     bool shouldBeEnforced,
    //     [PolicyDetails(PolicyType.DisableSend)] PolicyDetails policyDetails)
    // {
    //     policyDetails.OrganizationUserStatus = userStatus;
    //
    //     var actual = SendPolicyRequirement.Create([policyDetails]);
    //
    //     Assert.Equal(shouldBeEnforced, actual.DisableSend);
    // }
    //
    // [Theory, AutoData]
    // public void DisableHideEmail_IsFalse_IfNoSendOptionsPolicies(
    //     [PolicyDetails(PolicyType.RequireSso)] PolicyDetails otherPolicy1,
    //     [PolicyDetails(PolicyType.DisableSend)] PolicyDetails otherPolicy2)
    // {
    //     var actual = SendPolicyRequirement.Create([otherPolicy1, otherPolicy2]);
    //
    //     Assert.False(actual.DisableHideEmail);
    // }
    //
    // [Theory]
    // [InlineAutoData(OrganizationUserType.Owner, false)]
    // [InlineAutoData(OrganizationUserType.Admin, false)]
    // [InlineAutoData(OrganizationUserType.User, true)]
    // [InlineAutoData(OrganizationUserType.Custom, true)]
    // public void DisableHideEmail_TestRoles(
    //     OrganizationUserType userType,
    //     bool shouldBeEnforced,
    //     [PolicyDetails(PolicyType.SendOptions)] PolicyDetails policyDetails)
    // {
    //     EnableDisableHideEmail(policyDetails);
    //     policyDetails.OrganizationUserType = userType;
    //
    //     var actual = SendPolicyRequirement.Create([policyDetails]);
    //
    //     Assert.Equal(shouldBeEnforced, actual.DisableHideEmail);
    // }
    //
    // [Theory, AutoData]
    // public void DisableHideEmail_Not_EnforcedAgainstProviders(
    //     [PolicyDetails(PolicyType.SendOptions, isProvider: true)] PolicyDetails policyDetails)
    // {
    //     EnableDisableHideEmail(policyDetails);
    //
    //     var actual = SendPolicyRequirement.Create([policyDetails]);
    //
    //     Assert.False(actual.DisableHideEmail);
    // }
    //
    // [Theory]
    // [InlineAutoData(OrganizationUserStatusType.Confirmed, true)]
    // [InlineAutoData(OrganizationUserStatusType.Accepted, true)]
    // [InlineAutoData(OrganizationUserStatusType.Invited, false)]
    // [InlineAutoData(OrganizationUserStatusType.Revoked, false)]
    // public void DisableHideEmail_TestStatuses(
    //     OrganizationUserStatusType userStatus,
    //     bool shouldBeEnforced,
    //     [PolicyDetails(PolicyType.SendOptions)] PolicyDetails policyDetails)
    // {
    //     EnableDisableHideEmail(policyDetails);
    //     policyDetails.OrganizationUserStatus = userStatus;
    //
    //     var actual = SendPolicyRequirement.Create([policyDetails]);
    //
    //     Assert.Equal(shouldBeEnforced, actual.DisableHideEmail);
    // }
    //
    // [Theory, AutoData]
    // public void DisableHideEmail_HandlesNullData(
    //     [PolicyDetails(PolicyType.SendOptions)] PolicyDetails policyDetails)
    // {
    //     policyDetails.PolicyData = null;
    //
    //     var actual = SendPolicyRequirement.Create([policyDetails]);
    //
    //     Assert.False(actual.DisableHideEmail);
    // }
    //
    // private static void EnableDisableHideEmail(PolicyDetails policyDetails)
    //     => policyDetails.SetDataModel(new SendOptionsPolicyData { DisableHideEmail = true });
}
