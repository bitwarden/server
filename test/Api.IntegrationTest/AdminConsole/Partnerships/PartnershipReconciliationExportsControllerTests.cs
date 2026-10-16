using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public class PartnershipReconciliationExportsControllerTests : IClassFixture<ApiApplicationFactory>
{
    private const string Path = "/partnerships/v1/reconciliation-exports";

    private readonly ApiApplicationFactory _factory;

    public PartnershipReconciliationExportsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.UpdateConfiguration("globalSettings:partnerships:externalIdHashKey", "integration-test-hash-key");
        _factory.ConfigureServices(services =>
        {
            services.RemoveAll<IPartnershipReconciliationExportStorageService>();
            services.AddSingleton<IPartnershipReconciliationExportStorageService,
                FakeReconciliationExportStorageService>();
        });
        PartnershipTestHelpers.EnablePartnerSponsorships(_factory);
    }

    [Fact]
    public async Task Post_Returns202WithPendingExport()
    {
        using var client = (await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory,
            PartnershipStatus.Active)).Client;

        var response = await client.PostAsync(Path, null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal(["object", "id", "status"], body.EnumerateObject().Select(p => p.Name));
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal("pending", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_Pending_ReturnsStatusWithoutDownloadUrl()
    {
        using var client = (await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory,
            PartnershipStatus.Active)).Client;
        var id = await CreateExportAsync(client);

        var response = await client.GetAsync($"{Path}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("pending", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("downloadUrl", out _));
        Assert.False(body.TryGetProperty("recordCount", out _));
    }

    [Fact]
    public async Task Get_Completed_ReturnsRecordCountAndDownloadUrl()
    {
        var (client, partnership) = await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory,
            PartnershipStatus.Active);
        using var _ = client;
        await CreateEntitlementAsync(partnership!, "customer-1");
        var id = await CreateExportAsync(client);

        await _factory.GetService<IProcessPartnershipReconciliationExportsCommand>().ProcessAsync();
        var response = await client.GetAsync($"{Path}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal("completed", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("recordCount").GetInt32());
        var blobName = $"{partnership!.Id}/{id}.ndjson";
        Assert.Equal(FakeReconciliationExportStorageService.UrlFor(blobName),
            body.GetProperty("downloadUrl").GetString());
        Assert.True(body.GetProperty("expiresAt").GetDateTime() > DateTime.UtcNow);
        var storage = (FakeReconciliationExportStorageService)_factory
            .GetService<IPartnershipReconciliationExportStorageService>();
        var line = JsonDocument.Parse(storage.Read(blobName).Trim()).RootElement;
        Assert.Equal("customer-1", line.GetProperty("externalId").GetString());
    }

    [Fact]
    public async Task Get_AnotherPartnershipsExport_Returns404()
    {
        using var owner = (await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory,
            PartnershipStatus.Active)).Client;
        using var other = (await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory,
            PartnershipStatus.Active)).Client;
        var id = await CreateExportAsync(owner);

        var response = await other.GetAsync($"{Path}/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", (await ReadBodyAsync(response)).GetProperty("code").GetString());
    }

    private static async Task<Guid> CreateExportAsync(HttpClient client)
    {
        var response = await client.PostAsync(Path, null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await ReadBodyAsync(response)).GetProperty("id").GetGuid();
    }

    private Task<OrganizationPartnershipEntitlement> CreateEntitlementAsync(
        OrganizationPartnership partnership, string externalId) =>
        _factory.GetService<IOrganizationPartnershipEntitlementRepository>().CreateAsync(
            new OrganizationPartnershipEntitlement
            {
                OrganizationPartnershipId = partnership.Id,
                ExternalId = externalId,
                ExternalIdHash = string.Empty,
                State = PartnershipEntitlementState.Provisioned,
                SponsoredPlanType = SponsoredPlanType.Premium,
                LastAppliedEffectiveDate = DateTime.UtcNow,
            });

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
}

/// <summary>
/// Keeps export files in memory and issues predictable download URLs.
/// </summary>
public class FakeReconciliationExportStorageService : IPartnershipReconciliationExportStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public static string UrlFor(string blobName) => $"https://storage.example.com/{blobName}?sig=fake";

    public string Read(string blobName) => Encoding.UTF8.GetString(_files[blobName]);

    public Task<Stream> OpenWriteAsync(string blobName) =>
        Task.FromResult<Stream>(new CommitOnDisposeStream(bytes => _files[blobName] = bytes));

    public Task<string?> GetDownloadUrlAsync(string blobName) => Task.FromResult<string?>(UrlFor(blobName));

    public Task DeleteAsync(string blobName)
    {
        _files.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }

    private sealed class CommitOnDisposeStream(Action<byte[]> commit) : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                commit(ToArray());
            }
            base.Dispose(disposing);
        }
    }
}
