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
    private readonly SendEventClassifier _sut;

    public SendEventClassifierTests()
    {
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _organizationDomainRepository = Substitute.For<IOrganizationDomainRepository>();
        _sut = new SendEventClassifier(_organizationUserRepository, _organizationDomainRepository);
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
    }

    [Fact]
    public async Task BuildAccessContextAsync_OwnerHasNoConfirmedOrgs_ReturnsEmpty()
    {
        var userId = Guid.NewGuid();
        _organizationUserRepository.GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>());

        var context = await _sut.BuildAccessContextAsync(userId, "alice@example.com");

        Assert.Empty(context);
    }

    [Fact]
    public async Task BuildAccessContextAsync_AccessorIsConfirmedMember_SetsAccessorUserId()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var accessorUserId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");
        _organizationUserRepository.GetByOrganizationEmailAsync(orgId, "alice@example.com")
            .Returns(new OrganizationUser
            {
                OrganizationId = orgId,
                Status = OrganizationUserStatusType.Confirmed,
                UserId = accessorUserId,
            });

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
        _organizationUserRepository.GetByOrganizationEmailAsync(orgId, "alice@example.com")
            .Returns((OrganizationUser?)null);

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
        _organizationUserRepository.GetByOrganizationEmailAsync(orgId, "bob@external.com")
            .Returns((OrganizationUser?)null);

        var context = await _sut.BuildAccessContextAsync(userId, "bob@external.com");

        Assert.False(context.ContainsKey(orgId));
    }

    [Fact]
    public async Task BuildAccessContextAsync_CaseInsensitive_MatchesClaimedDomain()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");
        _organizationUserRepository.GetByOrganizationEmailAsync(orgId, "Alice@Example.COM")
            .Returns((OrganizationUser?)null);

        var context = await _sut.BuildAccessContextAsync(userId, "Alice@Example.COM");

        Assert.Equal("example.com", context[orgId].ClaimedDomain);
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
        _organizationUserRepository.GetByOrganizationEmailAsync(memberOrg, "alice@example.com")
            .Returns(new OrganizationUser
            {
                OrganizationId = memberOrg,
                Status = OrganizationUserStatusType.Confirmed,
                UserId = accessorUserId,
            });
        _organizationUserRepository.GetByOrganizationEmailAsync(claimedOrg, "alice@example.com")
            .Returns((OrganizationUser?)null);
        _organizationUserRepository.GetByOrganizationEmailAsync(externalOrg, "alice@example.com")
            .Returns((OrganizationUser?)null);

        var context = await _sut.BuildAccessContextAsync(userId, "alice@example.com");

        Assert.Equal(accessorUserId, context[memberOrg].AccessorUserId);
        Assert.Null(context[memberOrg].ClaimedDomain);
        Assert.Null(context[claimedOrg].AccessorUserId);
        Assert.Equal("example.com", context[claimedOrg].ClaimedDomain);
        Assert.False(context.ContainsKey(externalOrg));
    }

    [Fact]
    public async Task BuildAccessContextAsync_InvitedMember_TreatedAsExternal()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "other.com");
        // Accessor exists but is only Invited (no platform UserId) -> not attributable.
        _organizationUserRepository.GetByOrganizationEmailAsync(orgId, "alice@example.com")
            .Returns(new OrganizationUser
            {
                OrganizationId = orgId,
                Status = OrganizationUserStatusType.Invited,
                UserId = null,
            });

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
}
