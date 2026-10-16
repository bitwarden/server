using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

public class CreatePartnershipReconciliationExportCommandTests
{
    private static readonly DateTime _now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _retention = TimeSpan.FromHours(6);

    [Fact]
    public async Task CreateAsync_StoresPendingExportForPartnership()
    {
        var sutProvider = CreateSutProvider();
        var partnershipId = Guid.NewGuid();

        var export = await sutProvider.Sut.CreateAsync(partnershipId);

        Assert.NotEqual(Guid.Empty, export.Id);
        Assert.Equal(partnershipId, export.OrganizationPartnershipId);
        Assert.Equal(PartnershipReconciliationExportStatus.Pending, export.Status);
        Assert.Null(export.RecordCount);
        Assert.Null(export.BlobName);
        Assert.Null(export.CompletionDate);
        Assert.Equal(_now, export.CreationDate);
        Assert.Equal(_now, export.RevisionDate);
        await sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>()
            .Received(1).CreateAsync(export);
    }

    [Fact]
    public async Task CreateAsync_ExpiresAfterRetention()
    {
        var sutProvider = CreateSutProvider();

        var export = await sutProvider.Sut.CreateAsync(Guid.NewGuid());

        Assert.Equal(_now.Add(_retention), export.ExpirationDate);
    }

    private static SutProvider<CreatePartnershipReconciliationExportCommand> CreateSutProvider()
    {
        var sutProvider = new SutProvider<CreatePartnershipReconciliationExportCommand>()
            .SetDependency(new GlobalSettings
            {
                Partnerships = new GlobalSettings.PartnershipSettings { ReconciliationExportRetention = _retention },
            })
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>()
            .CreateAsync(Arg.Any<OrganizationPartnershipReconciliationExport>())
            .Returns(call => call.Arg<OrganizationPartnershipReconciliationExport>());
        return sutProvider;
    }
}
