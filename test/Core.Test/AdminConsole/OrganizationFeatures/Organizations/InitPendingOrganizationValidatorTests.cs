using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
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
    public async Task ValidateAsync_InvalidToken_ReturnsInvalidTokenError(
        User user,
        OrganizationUser orgUser,
        InitPendingOrganizationValidationRequest request,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        SetupTokenFactory(sutProvider);
        orgUser.Email = user.Email;
        var validationRequest = request with
        {
            User = user,
            OrganizationUser = orgUser,
            EmailToken = "invalid-token"
        };

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<InvalidTokenError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EmailMismatch_ReturnsEmailMismatchError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = "orguser@example.com";
        var token = CreateValidToken(orgUser, sutProvider);

        user.Email = "differentuser@example.com";

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<EmailMismatchError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NullOrgUserEmail_ReturnsInvalidTokenError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        orgUser.Email = null;

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<InvalidTokenError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_OrganizationMismatch_ReturnsOrganizationMismatchError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        orgUser.OrganizationId = Guid.NewGuid();
        SetValidOrganizationState(org);

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationMismatchError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_OrgEnabled_ReturnsOrganizationAlreadyEnabledError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        org.Enabled = true;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = null;
        org.PrivateKey = null;

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationAlreadyEnabledError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_OrgNotPending_ReturnsOrganizationNotPendingError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        org.Enabled = false;
        org.Status = OrganizationStatusType.Created;
        org.PublicKey = null;
        org.PrivateKey = null;

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotPendingError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_OrgHasKeys_ReturnsOrganizationHasKeysError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = "existing-public-key";
        org.PrivateKey = "existing-private-key";

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasKeysError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_SingleOrgPolicyViolation_ReturnsError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        SetValidOrganizationState(org);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg)
            .Returns(true);

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<SingleOrgPolicyViolationError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AutoConfirmPolicyViolation_ReturnsError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        SetValidOrganizationState(org);

        var policyDetails = new PolicyDetails
        {
            OrganizationId = org.Id,
            PolicyType = PolicyType.AutomaticUserConfirmation,
            OrganizationUserType = OrganizationUserType.Owner,
            OrganizationUserStatus = OrganizationUserStatusType.Invited
        };

        var autoConfirmReq = new AutomaticUserConfirmationPolicyRequirement(new[] { policyDetails });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(autoConfirmReq);

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<SingleOrgPolicyViolationError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TwoFactorRequired_UserDoesNotHave2FA_ReturnsError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        SetValidOrganizationState(org);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg)
            .Returns(false);

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var policyDetails = new PolicyDetails
        {
            OrganizationId = validationRequest.OrganizationId,
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

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_FreeOrgAdminLimitExceeded_ReturnsError(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        SetValidOrganizationState(org);
        org.PlanType = PlanType.Free;
        orgUser.Type = OrganizationUserType.Owner;

        SetupPassingPolicies(user, sutProvider);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(1);

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsError);
        Assert.IsType<FreeOrgAdminLimitError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AllValid_ReturnsValid(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        SetValidOrganizationState(org);

        SetupPassingPolicies(user, sutProvider);

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_PaidOrg_SkipsFreeOrgLimit_ReturnsValid(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        SetValidOrganizationState(org);
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Type = OrganizationUserType.Owner;

        SetupPassingPolicies(user, sutProvider);

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_FreeOrgNonAdmin_SkipsFreeOrgLimit_ReturnsValid(
        User user,
        OrganizationUser orgUser,
        Organization org,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        orgUser.Email = user.Email;
        var token = CreateValidToken(orgUser, sutProvider);
        SetValidOrganizationState(org);
        org.PlanType = PlanType.Free;
        orgUser.Type = OrganizationUserType.User;

        SetupPassingPolicies(user, sutProvider);

        var validationRequest = CreateValidationRequest(user, org, orgUser, token);
        orgUser.OrganizationId = validationRequest.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(validationRequest);

        Assert.True(result.IsValid);
    }

    private void SetupTokenFactory(SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();
    }

    private string CreateValidToken(
        OrganizationUser orgUser,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        SetupTokenFactory(sutProvider);

        _orgUserInviteTokenableFactory.CreateToken(orgUser).Returns(new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
        });

        var orgUserInviteTokenable = _orgUserInviteTokenableFactory.CreateToken(orgUser);
        return _orgUserInviteTokenDataFactory.Protect(orgUserInviteTokenable);
    }

    private static void SetValidOrganizationState(Organization org)
    {
        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = null;
        org.PrivateKey = null;
    }

    private static InitPendingOrganizationValidationRequest CreateValidationRequest(
        User user,
        Organization org,
        OrganizationUser orgUser,
        string emailToken)
    {
        return new InitPendingOrganizationValidationRequest
        {
            User = user,
            OrganizationId = Guid.NewGuid(),
            OrganizationUserId = orgUser.Id,
            OrganizationKeys = new Bit.Core.KeyManagement.Models.Data.PublicKeyEncryptionKeyPairData(
                wrappedPrivateKey: "wrapped-private-key",
                publicKey: "public-key"),
            CollectionName = null,
            EmailToken = emailToken,
            EncryptedOrganizationSymmetricKey = "encrypted-org-key",
            Organization = org,
            OrganizationUser = orgUser,
        };
    }

    private static void SetupPassingPolicies(
        User user,
        SutProvider<InitPendingOrganizationValidator> sutProvider)
    {
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg)
            .Returns(false);

        var twoFactorReq = new RequireTwoFactorPolicyRequirement(Enumerable.Empty<PolicyDetails>());
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(twoFactorReq);
    }
}
