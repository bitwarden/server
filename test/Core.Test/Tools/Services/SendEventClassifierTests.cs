#nullable enable

using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Tools.SendFeatures.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class SendEventClassifierTests
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IUserRepository _userRepository;
    private readonly SendEventClassifier _sut;

    public SendEventClassifierTests()
    {
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _organizationDomainRepository = Substitute.For<IOrganizationDomainRepository>();
        _userRepository = Substitute.For<IUserRepository>();
        _sut = new SendEventClassifier(
            _organizationUserRepository, _organizationDomainRepository, _userRepository);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("noatsign")]
    [InlineData("trailing@")]
    public async Task BuildAccessContextAsync_NoAccessorEmail_ReturnsEmpty(string? accessorEmail)
    {
        var context = await _sut.BuildAccessContextAsync(Guid.NewGuid(), accessorEmail);

        Assert.Empty(context);
        await _organizationUserRepository.DidNotReceiveWithAnyArgs().GetManyByUserAsync(default);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!);
    }

    [Fact]
    public async Task BuildAccessContextAsync_OwnerHasNoConfirmedOrgs_ReturnsEmpty()
    {
        var userId = Guid.NewGuid();
        _organizationUserRepository.GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>());

        var context = await _sut.BuildAccessContextAsync(userId, "alice@example.com");

        Assert.Empty(context);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!);
    }

    [Fact]
    public async Task BuildAccessContextAsync_AccessorIsConfirmedMember_SetsAccessorUserId()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var accessorUserId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");
        // OrganizationUser.Email is null for confirmed members; the accessor is matched via their User.
        SetupAccessorMemberships("alice@example.com", accessorUserId, (orgId, OrganizationUserStatusType.Confirmed));

        var context = await _sut.BuildAccessContextAsync(userId, "alice@example.com");

        Assert.Equal(accessorUserId, context[orgId].AccessorUserId);
        Assert.Null(context[orgId].ClaimedDomain);
    }

    [Fact]
    public async Task BuildAccessContextAsync_AccessorInClaimedDomainButNotMember_SetsClaimedDomain()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");
        // No user account for the accessor email -> not a member.
        _userRepository.GetByEmailAsync("alice@example.com").Returns((User?)null);

        var context = await _sut.BuildAccessContextAsync(userId, "alice@example.com");

        Assert.Null(context[orgId].AccessorUserId);
        Assert.Equal("example.com", context[orgId].ClaimedDomain);
    }

    [Fact]
    public async Task BuildAccessContextAsync_AccessorOutsideClaimedDomain_HasNoEntry()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");
        _userRepository.GetByEmailAsync("bob@external.com").Returns((User?)null);

        var context = await _sut.BuildAccessContextAsync(userId, "bob@external.com");

        Assert.False(context.ContainsKey(orgId));
    }

    [Fact]
    public async Task BuildAccessContextAsync_CaseInsensitive_MatchesClaimedDomain()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");
        // The accessor email is normalized before lookup, so the user query receives the lowercased form.
        _userRepository.GetByEmailAsync("alice@example.com").Returns((User?)null);

        var context = await _sut.BuildAccessContextAsync(userId, "Alice@Example.COM");

        Assert.Equal("example.com", context[orgId].ClaimedDomain);
    }

    [Fact]
    public async Task BuildAccessContextAsync_MixedCaseAccessorEmail_ResolvesMemberViaNormalizedLookup()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var accessorUserId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");
        // The User account is keyed on the normalized (trimmed + lowercased) email. Resolution must
        // normalize the caller's value before lookup, or the member silently renders as External on
        // case-sensitive providers.
        SetupAccessorMemberships("alice@example.com", accessorUserId, (orgId, OrganizationUserStatusType.Confirmed));

        var context = await _sut.BuildAccessContextAsync(userId, "  Alice@Example.COM ");

        Assert.Equal(accessorUserId, context[orgId].AccessorUserId);
        await _userRepository.Received(1).GetByEmailAsync("alice@example.com");
    }

    [Fact]
    public async Task BuildAccessContextAsync_MultipleOrgs_ClassifiesEachIndependently()
    {
        var userId = Guid.NewGuid();
        var memberOrg = Guid.NewGuid();
        var claimedOrg = Guid.NewGuid();
        var externalOrg = Guid.NewGuid();
        var accessorUserId = Guid.NewGuid();

        _organizationUserRepository.GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>
            {
                new() { OrganizationId = memberOrg, Status = OrganizationUserStatusType.Confirmed },
                new() { OrganizationId = claimedOrg, Status = OrganizationUserStatusType.Confirmed },
                new() { OrganizationId = externalOrg, Status = OrganizationUserStatusType.Confirmed },
            });
        _organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain>
            {
                new() { OrganizationId = memberOrg, DomainName = "example.com" },
                new() { OrganizationId = claimedOrg, DomainName = "example.com" },
            });
        // Accessor is a confirmed member of memberOrg only.
        SetupAccessorMemberships("alice@example.com", accessorUserId, (memberOrg, OrganizationUserStatusType.Confirmed));

        var context = await _sut.BuildAccessContextAsync(userId, "alice@example.com");

        Assert.Equal(accessorUserId, context[memberOrg].AccessorUserId);
        Assert.Null(context[memberOrg].ClaimedDomain);
        Assert.Null(context[claimedOrg].AccessorUserId);
        Assert.Equal("example.com", context[claimedOrg].ClaimedDomain);
        Assert.False(context.ContainsKey(externalOrg));
    }

    [Fact]
    public async Task BuildAccessContextAsync_AccessorOnlyInvitedToOwnerOrg_TreatedAsExternal()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var accessorUserId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "other.com");
        // Accessor has a user account but is only Invited (not Confirmed) to the org -> not attributable.
        SetupAccessorMemberships("alice@example.com", accessorUserId, (orgId, OrganizationUserStatusType.Invited));

        var context = await _sut.BuildAccessContextAsync(userId, "alice@example.com");

        Assert.False(context.ContainsKey(orgId));
    }

    [Fact]
    public async Task BuildAccessContextAsync_OnlyConfirmedOwnerOrgs_AreConsidered()
    {
        var userId = Guid.NewGuid();
        var confirmedOrg = Guid.NewGuid();
        var invitedOrg = Guid.NewGuid();
        _organizationUserRepository.GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>
            {
                new() { OrganizationId = confirmedOrg, Status = OrganizationUserStatusType.Confirmed },
                new() { OrganizationId = invitedOrg, Status = OrganizationUserStatusType.Invited },
            });
        _organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain>
            {
                new() { OrganizationId = confirmedOrg, DomainName = "example.com" }
            });

        await _sut.BuildAccessContextAsync(userId, "alice@example.com");

        await _organizationDomainRepository.Received(1).GetVerifiedDomainsByOrganizationIdsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(confirmedOrg) && !ids.Contains(invitedOrg)));
    }

    private void SetupOrgWithDomains(Guid userId, Guid orgId, params string[] domainNames)
    {
        _organizationUserRepository.GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>
            {
                new() { OrganizationId = orgId, Status = OrganizationUserStatusType.Confirmed }
            });
        _organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(domainNames
                .Select(d => new OrganizationDomain { OrganizationId = orgId, DomainName = d })
                .ToList());
    }

    private void SetupAccessorMemberships(
        string accessorEmail,
        Guid accessorUserId,
        params (Guid orgId, OrganizationUserStatusType status)[] memberships)
    {
        _userRepository.GetByEmailAsync(accessorEmail).Returns(new User { Id = accessorUserId });
        _organizationUserRepository.GetManyByUserAsync(accessorUserId)
            .Returns(memberships
                .Select(m => new OrganizationUser
                {
                    OrganizationId = m.orgId,
                    Status = m.status,
                    UserId = accessorUserId,
                })
                .ToList());
    }
}
