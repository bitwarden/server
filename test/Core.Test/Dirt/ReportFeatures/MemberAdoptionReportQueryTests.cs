using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class MemberAdoptionReportQueryTests
{
    private const int ActivityWindowDays = 30;

    private static readonly DateTime _now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static SutProvider<MemberAdoptionReportQuery> GetSutProvider()
    {
        var sutProvider = new SutProvider<MemberAdoptionReportQuery>()
            .WithFakeTimeProvider()
            .Create();

        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);

        return sutProvider;
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_ActivityExactlyOnWindowBoundary_CountsMemberAsActive(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId,
            CreateDetail(lastActivityDate: _now.AddDays(-ActivityWindowDays)));

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        Assert.Equal(1, result.ActiveMemberCount);
        Assert.Equal(0, result.InactiveMemberCount);
        Assert.True(Assert.Single(result.Members).HasRecentLogin);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_ActivityJustInsideWindow_CountsMemberAsActive(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId,
            CreateDetail(lastActivityDate: _now.AddDays(-ActivityWindowDays).AddTicks(1)));

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        Assert.Equal(1, result.TotalMemberCount);
        Assert.Equal(1, result.ActiveMemberCount);
        Assert.Equal(0, result.InactiveMemberCount);
        Assert.True(Assert.Single(result.Members).HasRecentLogin);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_ActivityJustOutsideWindow_CountsMemberAsInactive(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId,
            CreateDetail(lastActivityDate: _now.AddDays(-ActivityWindowDays).AddTicks(-1)));

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        Assert.Equal(1, result.TotalMemberCount);
        Assert.Equal(0, result.ActiveMemberCount);
        Assert.Equal(1, result.InactiveMemberCount);
        Assert.False(Assert.Single(result.Members).HasRecentLogin);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_ActivityExactlyNow_CountsMemberAsActive(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId, CreateDetail(lastActivityDate: _now));

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        Assert.Equal(1, result.ActiveMemberCount);
        Assert.True(Assert.Single(result.Members).HasRecentLogin);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_FutureDatedActivity_CountsMemberAsInactive(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId,
            CreateDetail(lastActivityDate: _now.AddTicks(1)),
            CreateDetail(lastActivityDate: _now.AddYears(1)));

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        Assert.Equal(2, result.TotalMemberCount);
        Assert.Equal(0, result.ActiveMemberCount);
        Assert.Equal(2, result.InactiveMemberCount);
        Assert.All(result.Members, member => Assert.False(member.HasRecentLogin));
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_NullLastActivityDate_CountsMemberAsInactive(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId, CreateDetail(lastActivityDate: null));

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        Assert.Equal(1, result.TotalMemberCount);
        Assert.Equal(0, result.ActiveMemberCount);
        Assert.Equal(1, result.InactiveMemberCount);
        Assert.False(Assert.Single(result.Members).HasRecentLogin);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_OrganizationWithNoMembers_ReturnsZeroedCounts(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId);

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        Assert.Equal(0, result.TotalMemberCount);
        Assert.Equal(0, result.ActiveMemberCount);
        Assert.Equal(0, result.InactiveMemberCount);
        Assert.Equal(0, result.SponsoredFamiliesRedeemedCount);
        Assert.Empty(result.Members);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_CountsRedeemedSponsorshipsIndependentlyOfActivity(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId,
            CreateDetail(lastActivityDate: _now, hasRedeemedSponsorship: true),
            CreateDetail(lastActivityDate: null, hasRedeemedSponsorship: true),
            CreateDetail(lastActivityDate: _now, hasRedeemedSponsorship: false));

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        Assert.Equal(2, result.SponsoredFamiliesRedeemedCount);
        Assert.Equal(3, result.TotalMemberCount);
        Assert.Equal(2, result.ActiveMemberCount);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_ActiveAndInactiveCounts_SumToTotal(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId,
            CreateDetail(lastActivityDate: _now),
            CreateDetail(lastActivityDate: _now.AddDays(-1)),
            CreateDetail(lastActivityDate: _now.AddDays(-ActivityWindowDays * 2)),
            CreateDetail(lastActivityDate: null));

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        Assert.Equal(4, result.TotalMemberCount);
        Assert.Equal(2, result.ActiveMemberCount);
        Assert.Equal(2, result.InactiveMemberCount);
        Assert.Equal(result.TotalMemberCount, result.ActiveMemberCount + result.InactiveMemberCount);
        Assert.Equal(result.TotalMemberCount, result.Members.Count());
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_ProjectsMemberDetailsOntoResult(
        Guid organizationId,
        Guid organizationUserId,
        Guid userId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var detail = new MemberAdoptionReportDetail
        {
            OrganizationUserId = organizationUserId,
            UserId = userId,
            Name = "Test User",
            Email = "user@example.com",
            LastActivityDate = _now.AddDays(-1),
            HasExtensionInstalled = true,
            VaultItemCount = 12,
            SharedItemCount = 3,
            HasRedeemedSponsorship = true
        };
        SetupDetails(sutProvider, organizationId, detail);

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        var member = Assert.Single(result.Members);
        Assert.Equal(organizationUserId, member.OrganizationUserId);
        Assert.Equal(userId, member.UserId);
        Assert.Equal("Test User", member.Name);
        Assert.Equal("user@example.com", member.Email);
        Assert.True(member.HasRecentLogin);
        Assert.True(member.HasExtensionInstalled);
        Assert.Equal(12, member.VaultItemCount);
        Assert.Equal(3, member.SharedItemCount);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_QueriesRepositoryWithRequestedOrganization(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupDetails(sutProvider, organizationId);

        // Act
        await sutProvider.Sut.GetMemberAdoptionReportAsync(
            new MemberAdoptionReportRequest { OrganizationId = organizationId });

        // Assert
        await sutProvider.GetDependency<IMemberAdoptionReportRepository>()
            .Received(1)
            .GetMemberAdoptionDetailsByOrganizationIdAsync(organizationId);
    }

    private static MemberAdoptionReportDetail CreateDetail(
        DateTime? lastActivityDate,
        bool hasRedeemedSponsorship = false) =>
        new()
        {
            OrganizationUserId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = "user@example.com",
            LastActivityDate = lastActivityDate,
            HasRedeemedSponsorship = hasRedeemedSponsorship
        };

    private static void SetupDetails(
        SutProvider<MemberAdoptionReportQuery> sutProvider,
        Guid organizationId,
        params MemberAdoptionReportDetail[] details) =>
        sutProvider.GetDependency<IMemberAdoptionReportRepository>()
            .GetMemberAdoptionDetailsByOrganizationIdAsync(organizationId)
            .Returns(details);
}
