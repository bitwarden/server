using System.Globalization;
using System.Web;
using Azure.Storage.Blobs;
using Bit.Core.AdminConsole.Services.Implementations;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Services;

public class AzurePartnershipReconciliationExportStorageServiceTests
{
    private const string DevConnectionString = "UseDevelopmentStorage=true";
    private static readonly DateTimeOffset _now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetDownloadUrlAsync_ExpiresAfterConfiguredLifetime()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(15));

        var url = await sut.GetDownloadUrlAsync("partnership/export.ndjson");

        var expiresOn = DateTimeOffset.Parse(Query(url)["se"]!, CultureInfo.InvariantCulture);
        Assert.Equal(_now.AddMinutes(15), expiresOn);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_IsReadOnly()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(15));

        var url = await sut.GetDownloadUrlAsync("partnership/export.ndjson");

        Assert.Equal("r", Query(url)["sp"]);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_TargetsTheExportBlobInTheExportContainer()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(15));

        var url = await sut.GetDownloadUrlAsync("partnership/export.ndjson");

        Assert.EndsWith($"/{AzurePartnershipReconciliationExportStorageService.ContainerName}/partnership/export.ndjson",
            new Uri(url!).AbsolutePath);
    }

    private static AzurePartnershipReconciliationExportStorageService CreateSut(TimeSpan lifetime)
    {
        var containerClient = new BlobServiceClient(DevConnectionString)
            .GetBlobContainerClient(AzurePartnershipReconciliationExportStorageService.ContainerName);
        return new AzurePartnershipReconciliationExportStorageService(containerClient, lifetime,
            new FakeTimeProvider(_now));
    }

    private static System.Collections.Specialized.NameValueCollection Query(string? url) =>
        HttpUtility.ParseQueryString(new Uri(url!).Query);
}
