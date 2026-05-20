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

    // ---------- BuildAccessResolverAsync ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("noatsign")]
    [InlineData("trailing@")]
    public async Task BuildAccessResolverAsync_InvalidEmail_ReturnsNull(string? accessorEmail)
    {
        var resolver = await _sut.BuildAccessResolverAsync(
            Guid.NewGuid(),
            accessorEmail,
            EventType.Send_Accessed_Text_FromClaimedDomain,
            EventType.Send_Accessed_Text_FromExternalDomain);

        Assert.Null(resolver);
    }

    [Fact]
    public async Task BuildAccessResolverAsync_UserHasNoOrgs_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        _organizationUserRepository.GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>());

        var resolver = await _sut.BuildAccessResolverAsync(
            userId,
            "alice@example.com",
            EventType.Send_Accessed_Text_FromClaimedDomain,
            EventType.Send_Accessed_Text_FromExternalDomain);

        Assert.Null(resolver);
    }

    [Fact]
    public async Task BuildAccessResolverAsync_UserHasOrgsWithoutClaimedDomains_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        _organizationUserRepository.GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>
            {
                new() { OrganizationId = orgId, Status = OrganizationUserStatusType.Confirmed }
            });
        _organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgId)))
            .Returns(new List<OrganizationDomain>());

        var resolver = await _sut.BuildAccessResolverAsync(
            userId,
            "alice@example.com",
            EventType.Send_Accessed_Text_FromClaimedDomain,
            EventType.Send_Accessed_Text_FromExternalDomain);

        Assert.Null(resolver);
    }

    [Fact]
    public async Task BuildAccessResolverAsync_AccessorInClaimedDomain_ReturnsClaimedVariant()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");

        var resolver = await _sut.BuildAccessResolverAsync(
            userId,
            "alice@example.com",
            EventType.Send_Accessed_Text_FromClaimedDomain,
            EventType.Send_Accessed_Text_FromExternalDomain);

        Assert.NotNull(resolver);
        Assert.Equal(EventType.Send_Accessed_Text_FromClaimedDomain, resolver!(orgId));
    }

    [Fact]
    public async Task BuildAccessResolverAsync_AccessorOutsideClaimedDomain_ReturnsExternalVariant()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");

        var resolver = await _sut.BuildAccessResolverAsync(
            userId,
            "bob@external.com",
            EventType.Send_Accessed_Text_FromClaimedDomain,
            EventType.Send_Accessed_Text_FromExternalDomain);

        Assert.NotNull(resolver);
        Assert.Equal(EventType.Send_Accessed_Text_FromExternalDomain, resolver!(orgId));
    }

    [Fact]
    public async Task BuildAccessResolverAsync_CaseInsensitive_MatchesClaimedDomain()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");

        var resolver = await _sut.BuildAccessResolverAsync(
            userId,
            "Alice@Example.COM",
            EventType.Send_Accessed_Text_FromClaimedDomain,
            EventType.Send_Accessed_Text_FromExternalDomain);

        Assert.NotNull(resolver);
        Assert.Equal(EventType.Send_Accessed_Text_FromClaimedDomain, resolver!(orgId));
    }

    [Fact]
    public async Task BuildAccessResolverAsync_MultipleOrgs_PerOrgClassification()
    {
        var userId = Guid.NewGuid();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var orgCWithoutDomains = Guid.NewGuid();
        _organizationUserRepository.GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>
            {
                new() { OrganizationId = orgA, Status = OrganizationUserStatusType.Confirmed },
                new() { OrganizationId = orgB, Status = OrganizationUserStatusType.Confirmed },
                new() { OrganizationId = orgCWithoutDomains, Status = OrganizationUserStatusType.Confirmed },
            });
        _organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain>
            {
                new() { OrganizationId = orgA, DomainName = "example.com" },
                new() { OrganizationId = orgB, DomainName = "partner.com" },
            });

        var resolver = await _sut.BuildAccessResolverAsync(
            userId,
            "alice@example.com",
            EventType.Send_Accessed_Text_FromClaimedDomain,
            EventType.Send_Accessed_Text_FromExternalDomain);

        Assert.NotNull(resolver);
        Assert.Equal(EventType.Send_Accessed_Text_FromClaimedDomain, resolver!(orgA));
        Assert.Equal(EventType.Send_Accessed_Text_FromExternalDomain, resolver(orgB));
        Assert.Null(resolver(orgCWithoutDomains));
    }

    [Fact]
    public async Task BuildAccessResolverAsync_OnlyConfirmedOrgs_AreConsidered()
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

        var resolver = await _sut.BuildAccessResolverAsync(
            userId,
            "alice@example.com",
            EventType.Send_Accessed_Text_FromClaimedDomain,
            EventType.Send_Accessed_Text_FromExternalDomain);

        await _organizationDomainRepository.Received(1).GetVerifiedDomainsByOrganizationIdsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(confirmedOrg) && !ids.Contains(invitedOrg)));
        Assert.NotNull(resolver);
    }

    // ---------- BuildCreationResolverAsync ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BuildCreationResolverAsync_EmptyRecipients_ReturnsNull(string? recipients)
    {
        var resolver = await _sut.BuildCreationResolverAsync(
            Guid.NewGuid(),
            recipients,
            EventType.Send_Created_Text_WithEmailVerification_FromClaimedDomain,
            EventType.Send_Created_Text_WithEmailVerification_FromExternalDomain);

        Assert.Null(resolver);
    }

    [Fact]
    public async Task BuildCreationResolverAsync_AllRecipientsInClaimedDomain_ReturnsClaimed()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");

        var resolver = await _sut.BuildCreationResolverAsync(
            userId,
            "alice@example.com, bob@example.com",
            EventType.Send_Created_Text_WithEmailVerification_FromClaimedDomain,
            EventType.Send_Created_Text_WithEmailVerification_FromExternalDomain);

        Assert.NotNull(resolver);
        Assert.Equal(EventType.Send_Created_Text_WithEmailVerification_FromClaimedDomain, resolver!(orgId));
    }

    [Fact]
    public async Task BuildCreationResolverAsync_AnyRecipientOutsideClaimedDomain_ReturnsExternal()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");

        var resolver = await _sut.BuildCreationResolverAsync(
            userId,
            "alice@example.com, bob@external.com",
            EventType.Send_Created_Text_WithEmailVerification_FromClaimedDomain,
            EventType.Send_Created_Text_WithEmailVerification_FromExternalDomain);

        Assert.NotNull(resolver);
        Assert.Equal(EventType.Send_Created_Text_WithEmailVerification_FromExternalDomain, resolver!(orgId));
    }

    [Fact]
    public async Task BuildCreationResolverAsync_RecipientsWithExtraWhitespaceAndCasing_Normalized()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        SetupOrgWithDomains(userId, orgId, "example.com");

        var resolver = await _sut.BuildCreationResolverAsync(
            userId,
            "  Alice@Example.COM ,  Bob@EXAMPLE.com ",
            EventType.Send_Created_Text_WithEmailVerification_FromClaimedDomain,
            EventType.Send_Created_Text_WithEmailVerification_FromExternalDomain);

        Assert.NotNull(resolver);
        Assert.Equal(EventType.Send_Created_Text_WithEmailVerification_FromClaimedDomain, resolver!(orgId));
    }

    [Fact]
    public async Task BuildCreationResolverAsync_OrgWithoutClaimedDomains_ResolverReturnsNullForThatOrg()
    {
        var userId = Guid.NewGuid();
        var orgWithDomains = Guid.NewGuid();
        var orgWithoutDomains = Guid.NewGuid();
        _organizationUserRepository.GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>
            {
                new() { OrganizationId = orgWithDomains, Status = OrganizationUserStatusType.Confirmed },
                new() { OrganizationId = orgWithoutDomains, Status = OrganizationUserStatusType.Confirmed },
            });
        _organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain>
            {
                new() { OrganizationId = orgWithDomains, DomainName = "example.com" }
            });

        var resolver = await _sut.BuildCreationResolverAsync(
            userId,
            "alice@example.com",
            EventType.Send_Created_Text_WithEmailVerification_FromClaimedDomain,
            EventType.Send_Created_Text_WithEmailVerification_FromExternalDomain);

        Assert.NotNull(resolver);
        Assert.Equal(EventType.Send_Created_Text_WithEmailVerification_FromClaimedDomain, resolver!(orgWithDomains));
        Assert.Null(resolver(orgWithoutDomains));
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
