using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Enums;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class OrganizationDomainAllowEmailChangeQueryTests
{
    [Theory, BitAutoData]
    public async Task IsAllowedAsync_ClaimingOrganizationHasNewDomainVerified_ReturnsAllowed(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization claimingOrganization,
        OrganizationDomain verifiedDomain)
    {
        const string newEmailDomain = "claimed-domain.com";
        claimingOrganization.Enabled = true;
        claimingOrganization.UseOrganizationDomains = true;
        verifiedDomain.OrganizationId = claimingOrganization.Id;
        verifiedDomain.DomainName = newEmailDomain;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { claimingOrganization });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(claimingOrganization.Id)))
            .Returns(new List<OrganizationDomain> { verifiedDomain });

        var result = await sutProvider.Sut.IsAllowedAsync(user, newEmailDomain);

        Assert.Equal(OrganizationDomainAllowEmailChangeDenialReason.Allowed, result);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_ClaimingOrganizationDoesNotHaveNewDomainVerified_ReturnsUserIsClaimedAndDomainNotVerified(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization claimingOrganization,
        OrganizationDomain verifiedDomain)
    {
        claimingOrganization.Enabled = true;
        claimingOrganization.UseOrganizationDomains = true;
        verifiedDomain.OrganizationId = claimingOrganization.Id;
        verifiedDomain.DomainName = "claimed-domain.com";

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { claimingOrganization });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { verifiedDomain });

        var result = await sutProvider.Sut.IsAllowedAsync(user, "other-domain.com");

        Assert.Equal(OrganizationDomainAllowEmailChangeDenialReason.UserIsClaimedAndDomainNotVerified, result);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_OrganizationDisabled_NotTreatedAsClaiming_FallsThroughToBlockCheck(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization disabledOrganization)
    {
        const string newEmailDomain = "new-domain.com";
        disabledOrganization.Enabled = false;
        disabledOrganization.UseOrganizationDomains = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { disabledOrganization });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(newEmailDomain)
            .Returns(false);

        var result = await sutProvider.Sut.IsAllowedAsync(user, newEmailDomain);

        Assert.Equal(OrganizationDomainAllowEmailChangeDenialReason.Allowed, result);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(newEmailDomain);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_OrganizationDoesNotUseOrganizationDomains_NotTreatedAsClaiming_FallsThroughToBlockCheck(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization organizationWithoutDomains)
    {
        const string newEmailDomain = "new-domain.com";
        organizationWithoutDomains.Enabled = true;
        organizationWithoutDomains.UseOrganizationDomains = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { organizationWithoutDomains });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(newEmailDomain)
            .Returns(false);

        var result = await sutProvider.Sut.IsAllowedAsync(user, newEmailDomain);

        Assert.Equal(OrganizationDomainAllowEmailChangeDenialReason.Allowed, result);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(newEmailDomain);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_NoClaimingOrganization_DomainNotBlocked_ReturnsAllowed(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user)
    {
        const string newEmailDomain = "unblocked-domain.com";

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization>());

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(newEmailDomain)
            .Returns(false);

        var result = await sutProvider.Sut.IsAllowedAsync(user, newEmailDomain);

        Assert.Equal(OrganizationDomainAllowEmailChangeDenialReason.Allowed, result);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_NoClaimingOrganization_DomainBlocked_ReturnsDomainIsBlockedByPolicy(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user)
    {
        const string newEmailDomain = "blocked-domain.com";

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization>());

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(newEmailDomain)
            .Returns(true);

        var result = await sutProvider.Sut.IsAllowedAsync(user, newEmailDomain);

        Assert.Equal(OrganizationDomainAllowEmailChangeDenialReason.DomainIsBlockedByPolicy, result);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_MultipleClaimingOrganizations_DomainVerifiedByAny_ReturnsAllowed(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization firstClaimingOrg,
        Organization secondClaimingOrg,
        OrganizationDomain firstOrgDomain,
        OrganizationDomain secondOrgDomain)
    {
        const string newEmailDomain = "second-org-domain.com";
        firstClaimingOrg.Enabled = true;
        firstClaimingOrg.UseOrganizationDomains = true;
        secondClaimingOrg.Enabled = true;
        secondClaimingOrg.UseOrganizationDomains = true;

        firstOrgDomain.OrganizationId = firstClaimingOrg.Id;
        firstOrgDomain.DomainName = "first-org-domain.com";
        secondOrgDomain.OrganizationId = secondClaimingOrg.Id;
        secondOrgDomain.DomainName = newEmailDomain;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { firstClaimingOrg, secondClaimingOrg });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { firstOrgDomain, secondOrgDomain });

        var result = await sutProvider.Sut.IsAllowedAsync(user, newEmailDomain);

        Assert.Equal(OrganizationDomainAllowEmailChangeDenialReason.Allowed, result);
    }

    [Theory, BitAutoData]
    public async Task IsAllowedAsync_ClaimingAndNonClaimingOrganizations_OnlyClaimingOrgDomainsConsidered(
        SutProvider<OrganizationDomainAllowEmailChangeQuery> sutProvider,
        User user,
        Organization claimingOrganization,
        Organization disabledOrganization,
        OrganizationDomain claimingOrgDomain)
    {
        const string newEmailDomain = "claimed-domain.com";
        claimingOrganization.Enabled = true;
        claimingOrganization.UseOrganizationDomains = true;
        disabledOrganization.Enabled = false;
        disabledOrganization.UseOrganizationDomains = true;
        claimingOrgDomain.OrganizationId = claimingOrganization.Id;
        claimingOrgDomain.DomainName = newEmailDomain;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { claimingOrganization, disabledOrganization });

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { claimingOrgDomain });

        var result = await sutProvider.Sut.IsAllowedAsync(user, newEmailDomain);

        Assert.Equal(OrganizationDomainAllowEmailChangeDenialReason.Allowed, result);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(claimingOrganization.Id) && !ids.Contains(disabledOrganization.Id)));
        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>(), Arg.Any<Guid?>());
    }
}
