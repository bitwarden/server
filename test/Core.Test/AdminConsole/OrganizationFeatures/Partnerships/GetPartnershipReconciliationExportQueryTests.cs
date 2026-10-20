using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

public class GetPartnershipReconciliationExportQueryTests
{
    private const string DownloadUrl = "https://storage.example.com/export.ndjson?sig=abc";
    private static readonly DateTime _now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _partnershipId = Guid.NewGuid();

    [Fact]
    public async Task GetAsync_Missing_ReturnsNotFound()
    {
        var sutProvider = CreateSutProvider();

        var result = await sutProvider.Sut.GetAsync(_partnershipId, Guid.NewGuid());

        Assert.IsType<ReconciliationExportNotFound>(result.AsError);
    }

    [Fact]
    public async Task GetAsync_AnotherPartnershipsExport_ReturnsNotFoundWithoutUrl()
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangeExport(sutProvider, PartnershipReconciliationExportStatus.Completed, _now.AddHours(1),
            partnershipId: Guid.NewGuid());

        var result = await sutProvider.Sut.GetAsync(_partnershipId, export.Id);

        Assert.IsType<ReconciliationExportNotFound>(result.AsError);
        await sutProvider.GetDependency<IPartnershipReconciliationExportStorageService>()
            .DidNotReceiveWithAnyArgs().GetDownloadUrlAsync(default!);
    }

    [Theory]
    [InlineData(PartnershipReconciliationExportStatus.Pending)]
    [InlineData(PartnershipReconciliationExportStatus.Running)]
    [InlineData(PartnershipReconciliationExportStatus.Failed)]
    public async Task GetAsync_NotCompleted_ReturnsExportWithoutUrl(PartnershipReconciliationExportStatus status)
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangeExport(sutProvider, status, _now.AddHours(1));

        var result = await sutProvider.Sut.GetAsync(_partnershipId, export.Id);

        Assert.Same(export, result.AsSuccess.Export);
        Assert.Null(result.AsSuccess.DownloadUrl);
    }

    [Fact]
    public async Task GetAsync_CompletedWithinRetention_ReturnsFreshUrl()
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangeExport(sutProvider, PartnershipReconciliationExportStatus.Completed, _now.AddHours(1));

        var result = await sutProvider.Sut.GetAsync(_partnershipId, export.Id);

        Assert.Equal(DownloadUrl, result.AsSuccess.DownloadUrl);
    }

    [Fact]
    public async Task GetAsync_CompletedPastRetention_ReturnsExportWithoutUrl()
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangeExport(sutProvider, PartnershipReconciliationExportStatus.Completed, _now);

        var result = await sutProvider.Sut.GetAsync(_partnershipId, export.Id);

        Assert.Null(result.AsSuccess.DownloadUrl);
    }

    private static SutProvider<GetPartnershipReconciliationExportQuery> CreateSutProvider()
    {
        var sutProvider = new SutProvider<GetPartnershipReconciliationExportQuery>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IPartnershipReconciliationExportStorageService>()
            .GetDownloadUrlAsync(Arg.Any<string>()).Returns(DownloadUrl);
        return sutProvider;
    }

    private static OrganizationPartnershipReconciliationExport ArrangeExport(
        SutProvider<GetPartnershipReconciliationExportQuery> sutProvider,
        PartnershipReconciliationExportStatus status, DateTime expirationDate, Guid? partnershipId = null)
    {
        var export = new OrganizationPartnershipReconciliationExport
        {
            Id = Guid.NewGuid(),
            OrganizationPartnershipId = partnershipId ?? _partnershipId,
            Status = status,
            BlobName = "partnership/export.ndjson",
            ExpirationDate = expirationDate,
        };
        sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>()
            .GetByIdAsync(export.Id).Returns(export);
        return export;
    }
}
