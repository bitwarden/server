using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class OrganizationDomainAllowEmailChangeQueryTests
{
    // Domain literals are intentionally generic; the scenario lives in the test name.
    private const string _currentEmail = "old@example.com";
    private const string _newDomain = "test-domain.com";
    private const string _newEmail = "user@test-domain.com";
    private const string _otherDomain = "other-test-domain.com";
    private const string _claimedNotVerifiedMessage =
        "Your account is managed by an organization, and this email address isn't on one of the organization's verified domains.";
    private const string _blockedByPolicyMessage =
        "This email address is claimed by an organization using Bitwarden.";

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_ClaimingOrganizationHasNewDomainVerified_DoesNotThrow(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization claimingOrganization,
        OrganizationDomain verifiedDomain)
    {
        user.Email = _currentEmail;
        claimingOrganization.Enabled = true;
        claimingOrganization.UseOrganizationDomains = true;
        verifiedDomain.OrganizationId = claimingOrganization.Id;
        verifiedDomain.DomainName = _newDomain;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { claimingOrganization });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(claimingOrganization.Id)))
            .Returns(new List<OrganizationDomain> { verifiedDomain });

        await sutProvider.Sut.IsAllowedAsync(user, _newEmail);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_ClaimingOrganizationDoesNotHaveNewDomainVerified_ThrowsWithClaimedMessage(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization claimingOrganization,
        OrganizationDomain verifiedDomain)
    {
        user.Email = _currentEmail;
        claimingOrganization.Enabled = true;
        claimingOrganization.UseOrganizationDomains = true;
        verifiedDomain.OrganizationId = claimingOrganization.Id;
        verifiedDomain.DomainName = _otherDomain;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { claimingOrganization });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { verifiedDomain });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.IsAllowedAsync(user, _newEmail));
        Assert.Equal(_claimedNotVerifiedMessage, ex.Message);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_OrganizationDisabled_NotTreatedAsClaiming_FallsThroughToBlockCheck(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization disabledOrganization)
    {
        user.Email = _currentEmail;
        disabledOrganization.Enabled = false;
        disabledOrganization.UseOrganizationDomains = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { disabledOrganization });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(_newDomain)
            .Returns(false);

        await sutProvider.Sut.IsAllowedAsync(user, _newEmail);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(_newDomain);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_OrganizationDoesNotUseOrganizationDomains_NotTreatedAsClaiming_FallsThroughToBlockCheck(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization organizationWithoutDomains)
    {
        user.Email = _currentEmail;
        organizationWithoutDomains.Enabled = true;
        organizationWithoutDomains.UseOrganizationDomains = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { organizationWithoutDomains });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(_newDomain)
            .Returns(false);

        await sutProvider.Sut.IsAllowedAsync(user, _newEmail);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(_newDomain);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_NoClaimingOrganization_DomainNotBlocked_DoesNotThrow(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user)
    {
        user.Email = _currentEmail;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization>());

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(_newDomain, Arg.Any<Guid?>())
            .Returns(false);

        await sutProvider.Sut.IsAllowedAsync(user, _newEmail);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_NoClaimingOrganization_DomainBlocked_ThrowsWithPolicyMessage(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user)
    {
        user.Email = _currentEmail;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization>());

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(_newDomain, Arg.Any<Guid?>())
            .Returns(true);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.IsAllowedAsync(user, _newEmail));
        Assert.Equal(_blockedByPolicyMessage, ex.Message);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_MultipleClaimingOrganizations_DomainVerifiedByAny_DoesNotThrow(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization firstClaimingOrg,
        Organization secondClaimingOrg,
        OrganizationDomain firstOrgDomain,
        OrganizationDomain secondOrgDomain)
    {
        user.Email = _currentEmail;
        firstClaimingOrg.Enabled = true;
        firstClaimingOrg.UseOrganizationDomains = true;
        secondClaimingOrg.Enabled = true;
        secondClaimingOrg.UseOrganizationDomains = true;

        firstOrgDomain.OrganizationId = firstClaimingOrg.Id;
        firstOrgDomain.DomainName = _otherDomain;
        secondOrgDomain.OrganizationId = secondClaimingOrg.Id;
        secondOrgDomain.DomainName = _newDomain;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { firstClaimingOrg, secondClaimingOrg });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { firstOrgDomain, secondOrgDomain });

        await sutProvider.Sut.IsAllowedAsync(user, _newEmail);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_ClaimingAndNonClaimingOrganizations_OnlyClaimingOrgDomainsConsidered(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization claimingOrganization,
        Organization disabledOrganization,
        OrganizationDomain claimingOrgDomain)
    {
        user.Email = _currentEmail;
        claimingOrganization.Enabled = true;
        claimingOrganization.UseOrganizationDomains = true;
        disabledOrganization.Enabled = false;
        disabledOrganization.UseOrganizationDomains = true;
        claimingOrgDomain.OrganizationId = claimingOrganization.Id;
        claimingOrgDomain.DomainName = _newDomain;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { claimingOrganization, disabledOrganization });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { claimingOrgDomain });

        await sutProvider.Sut.IsAllowedAsync(user, _newEmail);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(claimingOrganization.Id) && !ids.Contains(disabledOrganization.Id)));
        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_SameDomain_SkipsPolicyLookup(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user)
    {
        user.Email = "old@example.com";

        await sutProvider.Sut.IsAllowedAsync(user, "new@example.com");

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs()
            .GetByVerifiedUserEmailDomainAsync(default);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceiveWithAnyArgs()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_SameDomainDifferentCase_SkipsPolicyLookup(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user)
    {
        // EmailValidation.GetDomain lowercases the domain, so casing differences must not
        // re-enter the policy gate.
        user.Email = "old@Example.com";

        await sutProvider.Sut.IsAllowedAsync(user, "new@example.com");

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs()
            .GetByVerifiedUserEmailDomainAsync(default);
    }

    [Theory]
    [BitAutoData("no-at-sign")]
    [BitAutoData("too@many@signs.com")]
    [BitAutoData("@no-local-part.com")]
    [BitAutoData("")]
    public async Task IsAllowedAsync_InvalidNewEmail_ThrowsBadRequest(
        string invalidEmail,
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user)
    {
        user.Email = _currentEmail;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.IsAllowedAsync(user, invalidEmail));
        Assert.Equal("Invalid email address format.", ex.Message);
    }
}
