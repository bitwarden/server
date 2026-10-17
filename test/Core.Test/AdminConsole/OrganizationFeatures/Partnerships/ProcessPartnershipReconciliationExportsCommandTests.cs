using System.Text;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

public class ProcessPartnershipReconciliationExportsCommandTests
{
    private static readonly DateTime _now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _retention = TimeSpan.FromHours(6);

    [Fact]
    public async Task ProcessAsync_Pending_WritesOneLinePerEntitlementWithPartnerFields()
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangePending(sutProvider);
        var entitlement = CreateEntitlement(export);
        ArrangePages(sutProvider, export, [entitlement]);
        var output = ArrangeOutput(sutProvider);

        await sutProvider.Sut.ProcessAsync();

        var line = Assert.Single(ReadLines(output));
        Assert.Equal(
            ["externalId", "state", "planType", "accountRef", "metadata", "boundAt", "suspendedAt", "canceledAt",
                "resumeWindowExpiresAt", "lastAppliedAt", "createdAt", "updatedAt"],
            line.EnumerateObject().Select(p => p.Name));
        Assert.Equal("customer-1", line.GetProperty("externalId").GetString());
        Assert.Equal("active", line.GetProperty("state").GetString());
        Assert.Equal("premium", line.GetProperty("planType").GetString());
        Assert.Equal(entitlement.AccountRef, line.GetProperty("accountRef").GetGuid());
        Assert.Equal("gold", line.GetProperty("metadata").GetProperty("tier").GetString());
        Assert.Equal("2026-10-01T00:00:00Z", line.GetProperty("boundAt").GetString());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("canceledAt").ValueKind);
    }

    [Fact]
    public async Task ProcessAsync_Pending_NeverWritesTheBoundUserId()
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangePending(sutProvider);
        var entitlement = CreateEntitlement(export);
        ArrangePages(sutProvider, export, [entitlement]);
        var output = ArrangeOutput(sutProvider);

        await sutProvider.Sut.ProcessAsync();

        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.DoesNotContain(entitlement.UserId!.Value.ToString(), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userId", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_FullPage_ReadsNextPageAfterLastId()
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangePending(sutProvider);
        var firstPage = Enumerable.Range(0, ProcessPartnershipReconciliationExportsCommand.PageSize)
            .Select(_ => CreateEntitlement(export)).ToList();
        var secondPage = new List<OrganizationPartnershipEntitlement> { CreateEntitlement(export) };
        var repository = sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>();
        repository.GetPageByOrganizationPartnershipIdAsync(export.OrganizationPartnershipId, null,
            ProcessPartnershipReconciliationExportsCommand.PageSize).Returns(firstPage);
        repository.GetPageByOrganizationPartnershipIdAsync(export.OrganizationPartnershipId, firstPage[^1].Id,
            ProcessPartnershipReconciliationExportsCommand.PageSize).Returns(secondPage);
        var output = ArrangeOutput(sutProvider);

        await sutProvider.Sut.ProcessAsync();

        Assert.Equal(firstPage.Count + 1, ReadLines(output).Count);
        Assert.Equal(firstPage.Count + 1, export.RecordCount);
    }

    [Fact]
    public async Task ProcessAsync_Pending_MarksCompletedWithCountBlobAndRetention()
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangePending(sutProvider);
        ArrangePages(sutProvider, export, [CreateEntitlement(export), CreateEntitlement(export)]);
        ArrangeOutput(sutProvider);

        var result = await sutProvider.Sut.ProcessAsync();

        Assert.Equal(1, result.Completed);
        Assert.Equal(PartnershipReconciliationExportStatus.Completed, export.Status);
        Assert.Equal(2, export.RecordCount);
        Assert.Equal($"{export.OrganizationPartnershipId}/{export.Id}.ndjson", export.BlobName);
        Assert.Equal(_now, export.CompletionDate);
        Assert.Equal(_now.Add(_retention), export.ExpirationDate);
        await sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>()
            .Received(1).ReplaceAsync(export);
    }

    [Fact]
    public async Task ProcessAsync_ClaimedElsewhere_DoesNotWrite()
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangePending(sutProvider);
        sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>()
            .StartIfPendingAsync(export.Id, _now).Returns(false);

        var result = await sutProvider.Sut.ProcessAsync();

        Assert.Equal(0, result.Completed + result.Failed);
        await sutProvider.GetDependency<IPartnershipReconciliationExportStorageService>()
            .DidNotReceiveWithAnyArgs().OpenWriteAsync(default!);
    }

    [Fact]
    public async Task ProcessAsync_WriteThrows_MarksFailedAndDeletesPartialFile()
    {
        var sutProvider = CreateSutProvider();
        var export = ArrangePending(sutProvider);
        ArrangeOutput(sutProvider);
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetPageByOrganizationPartnershipIdAsync(default, default, default).ReturnsForAnyArgs(
                Task.FromException<ICollection<OrganizationPartnershipEntitlement>>(
                    new InvalidOperationException("boom")));

        var result = await sutProvider.Sut.ProcessAsync();

        Assert.Equal(1, result.Failed);
        Assert.Equal(PartnershipReconciliationExportStatus.Failed, export.Status);
        Assert.Null(export.BlobName);
        Assert.Null(export.RecordCount);
        Assert.Equal(_now.Add(_retention), export.ExpirationDate);
        await sutProvider.GetDependency<IPartnershipReconciliationExportStorageService>()
            .Received(1).DeleteAsync($"{export.OrganizationPartnershipId}/{export.Id}.ndjson");
        await sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>()
            .Received(1).ReplaceAsync(export);
    }

    [Fact]
    public async Task ProcessAsync_WriteThrows_LogsExportIdWithoutExceptionText()
    {
        const string sensitive = "external id customer-secret-42";
        var sutProvider = CreateSutProvider();
        var export = ArrangePending(sutProvider);
        ArrangeOutput(sutProvider);
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetPageByOrganizationPartnershipIdAsync(default, default, default).ReturnsForAnyArgs(
                Task.FromException<ICollection<OrganizationPartnershipEntitlement>>(
                    new InvalidOperationException(sensitive)));

        await sutProvider.Sut.ProcessAsync();

        var logged = string.Join("\n", sutProvider.GetDependency<ILogger<ProcessPartnershipReconciliationExportsCommand>>()
            .ReceivedCalls()
            .SelectMany(call => call.GetArguments())
            .Select(argument => argument?.ToString()));
        Assert.Contains(export.Id.ToString(), logged);
        Assert.DoesNotContain(sensitive, logged);
    }

    [Fact]
    public async Task ProcessAsync_Expired_DeletesFileAndExport()
    {
        var sutProvider = CreateSutProvider();
        var export = new OrganizationPartnershipReconciliationExport
        {
            Id = Guid.NewGuid(),
            OrganizationPartnershipId = Guid.NewGuid(),
            Status = PartnershipReconciliationExportStatus.Completed,
            BlobName = "stored-name",
            ExpirationDate = _now,
        };
        sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>()
            .GetManyExpiredAsync(_now).Returns([export]);

        var result = await sutProvider.Sut.ProcessAsync();

        Assert.Equal(1, result.Deleted);
        await sutProvider.GetDependency<IPartnershipReconciliationExportStorageService>()
            .Received(1).DeleteAsync($"{export.OrganizationPartnershipId}/{export.Id}.ndjson");
        await sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>()
            .Received(1).DeleteAsync(export);
    }

    [Fact]
    public async Task ProcessAsync_ExpiredFileDeleteThrows_KeepsExportAndStillProcessesPending()
    {
        var sutProvider = CreateSutProvider();
        var expired = new OrganizationPartnershipReconciliationExport
        {
            Id = Guid.NewGuid(),
            OrganizationPartnershipId = Guid.NewGuid(),
            ExpirationDate = _now,
        };
        var repository = sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>();
        repository.GetManyExpiredAsync(_now).Returns([expired]);
        sutProvider.GetDependency<IPartnershipReconciliationExportStorageService>()
            .DeleteAsync($"{expired.OrganizationPartnershipId}/{expired.Id}.ndjson")
            .ThrowsAsync(new InvalidOperationException());
        var pending = ArrangePending(sutProvider);
        ArrangePages(sutProvider, pending, []);
        ArrangeOutput(sutProvider);

        var result = await sutProvider.Sut.ProcessAsync();

        Assert.Equal(0, result.Deleted);
        Assert.Equal(1, result.Completed);
        await repository.DidNotReceive().DeleteAsync(expired);
    }

    private static SutProvider<ProcessPartnershipReconciliationExportsCommand> CreateSutProvider()
    {
        var sutProvider = new SutProvider<ProcessPartnershipReconciliationExportsCommand>()
            .SetDependency(new GlobalSettings
            {
                Partnerships = new GlobalSettings.PartnershipSettings { ReconciliationExportRetention = _retention },
            })
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        var repository = sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>();
        repository.GetManyExpiredAsync(Arg.Any<DateTime>()).Returns([]);
        repository.GetManyPendingAsync().Returns([]);
        return sutProvider;
    }

    private static OrganizationPartnershipReconciliationExport ArrangePending(
        SutProvider<ProcessPartnershipReconciliationExportsCommand> sutProvider)
    {
        var export = new OrganizationPartnershipReconciliationExport
        {
            Id = Guid.NewGuid(),
            OrganizationPartnershipId = Guid.NewGuid(),
            Status = PartnershipReconciliationExportStatus.Pending,
            CreationDate = _now.AddMinutes(-1),
            ExpirationDate = _now.AddHours(1),
        };
        var repository = sutProvider.GetDependency<IOrganizationPartnershipReconciliationExportRepository>();
        repository.GetManyPendingAsync().Returns([export]);
        repository.StartIfPendingAsync(export.Id, _now).Returns(true);
        return export;
    }

    private static void ArrangePages(
        SutProvider<ProcessPartnershipReconciliationExportsCommand> sutProvider,
        OrganizationPartnershipReconciliationExport export, List<OrganizationPartnershipEntitlement> entitlements) =>
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetPageByOrganizationPartnershipIdAsync(export.OrganizationPartnershipId, null,
                ProcessPartnershipReconciliationExportsCommand.PageSize)
            .Returns(entitlements);

    private static MemoryStream ArrangeOutput(SutProvider<ProcessPartnershipReconciliationExportsCommand> sutProvider)
    {
        var output = new MemoryStream();
        sutProvider.GetDependency<IPartnershipReconciliationExportStorageService>()
            .OpenWriteAsync(Arg.Any<string>()).Returns(output);
        return output;
    }

    private static List<JsonElement> ReadLines(MemoryStream output) =>
        Encoding.UTF8.GetString(output.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement)
            .ToList();

    private static OrganizationPartnershipEntitlement CreateEntitlement(
        OrganizationPartnershipReconciliationExport export)
    {
        var entitlement = new OrganizationPartnershipEntitlement
        {
            Id = Guid.NewGuid(),
            OrganizationPartnershipId = export.OrganizationPartnershipId,
            ExternalId = "customer-1",
            ExternalIdHash = "hash",
            State = PartnershipEntitlementState.Active,
            SponsoredPlanType = SponsoredPlanType.Premium,
            UserId = Guid.NewGuid(),
            BoundDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
            LastAppliedEffectiveDate = new DateTime(2026, 10, 1),
            CreationDate = new DateTime(2026, 9, 30),
            RevisionDate = new DateTime(2026, 10, 1),
        };
        entitlement.SetNewAccountRef();
        entitlement.SetMetadata(new Dictionary<string, string> { ["tier"] = "gold" });
        return entitlement;
    }
}
