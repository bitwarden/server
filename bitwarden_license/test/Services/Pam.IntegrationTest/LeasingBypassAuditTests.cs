using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.IntegrationTest;

/// <summary>
/// The two leasing bypasses the server audits: a collection change that ungates an item (PAM-203) and the whole-vault
/// export (PAM-204).
/// </summary>
public class LeasingBypassAuditTests(ApiApplicationFactory factory)
    : AccessRuleIntegrationTestBase(factory, "pam-bypass-audit")
{
    private Collection _gating = null!;
    private Collection _otherGating = null!;
    private Collection _plain = null!;
    private Collection _otherPlain = null!;
    private Guid _ownerId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        FeatureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging).Returns(false);

        // Rules gate only in an organization with Privileged Controls. Exempting the Owner from leasing on the "/admin"
        // endpoints means a refused move is not what these tests see.
        Organization.UsePam = true;
        Organization.AllowAdminAccessToAllCollectionItems = true;
        await Factory.GetService<IOrganizationRepository>().UpsertAsync(Organization);
        await Factory.GetService<IOrganizationAbilityCacheService>()
            .UpsertOrganizationAbilityAsync(Organization, CancellationToken.None);

        _gating = await OrganizationTestHelpers.CreateCollectionAsync(Factory, Organization.Id, "Gated");
        _otherGating = await OrganizationTestHelpers.CreateCollectionAsync(Factory, Organization.Id, "Also gated");
        _plain = await OrganizationTestHelpers.CreateCollectionAsync(Factory, Organization.Id, "Plain");
        _otherPlain = await OrganizationTestHelpers.CreateCollectionAsync(Factory, Organization.Id, "Also plain");
        _ownerId = (await Factory.GetService<IUserRepository>().GetByEmailAsync(OwnerEmail))!.Id;

        await LoginHelper.LoginAsync(OwnerEmail);
        var rule = await Client.PostAsJsonAsync(AccessRulesUrl, new
        {
            name = "Production database",
            enabled = true,
            conditions = JsonNode.Parse("""[{"kind":"human_approval","approverCount":1}]"""),
            collections = new[] { _gating.Id, _otherGating.Id },
        });
        rule.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MovingAGatedItemToAPlainCollection_RecordsItAsUngated()
    {
        var cipher = await CreateCipherAsync(_gating.Id);

        var response = await Client.PutAsJsonAsync($"ciphers/{cipher.Id}/collections-admin",
            new { collectionIds = new[] { _plain.Id } });
        response.EnsureSuccessStatusCode();

        var row = Assert.Single(await GetTrailAsync("credentialUngated"))!;
        Assert.Equal(cipher.Id, row["cipherId"]!.GetValue<Guid>());
        Assert.Equal(_plain.Id, row["collectionId"]!.GetValue<Guid>());
        Assert.Equal(_ownerId, row["actorId"]!.GetValue<Guid>());
        Assert.False(row["incomplete"]!.GetValue<bool>());
    }

    [Fact]
    public async Task MovingAGatedItemToTwoPlainCollections_RecordsNoCollection()
    {
        var cipher = await CreateCipherAsync(_gating.Id);

        var response = await Client.PutAsJsonAsync($"ciphers/{cipher.Id}/collections-admin",
            new { collectionIds = new[] { _plain.Id, _otherPlain.Id } });
        response.EnsureSuccessStatusCode();

        var row = Assert.Single(await GetTrailAsync("credentialUngated"))!;
        Assert.Equal(cipher.Id, row["cipherId"]!.GetValue<Guid>());
        Assert.True(row["collectionId"] == null || row["collectionId"]!.GetValue<Guid?>() == null);
    }

    [Fact]
    public async Task MovingAGatedItemToAnotherGatingCollection_RecordsNothing()
    {
        var cipher = await CreateCipherAsync(_gating.Id);

        var response = await Client.PutAsJsonAsync($"ciphers/{cipher.Id}/collections-admin",
            new { collectionIds = new[] { _otherGating.Id } });
        response.EnsureSuccessStatusCode();

        Assert.Empty(await GetTrailAsync("credentialUngated"));
    }

    [Fact]
    public async Task AddingCollectionsInBulk_RecordsOnlyTheItemsItUngated()
    {
        var gated = await CreateCipherAsync(_gating.Id);
        var alreadyPlain = await CreateCipherAsync(_plain.Id);

        var response = await Client.PostAsJsonAsync("ciphers/bulk-collections", new
        {
            organizationId = Organization.Id,
            cipherIds = new[] { gated.Id, alreadyPlain.Id },
            collectionIds = new[] { _plain.Id },
            removeCollections = false,
        });
        response.EnsureSuccessStatusCode();

        var row = Assert.Single(await GetTrailAsync("credentialUngated"))!;
        Assert.Equal(gated.Id, row["cipherId"]!.GetValue<Guid>());
        Assert.Equal(_plain.Id, row["collectionId"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task RemovingTheLastGatingCollectionInBulk_RecordsTheItemWithNoCollection()
    {
        var cipher = await CreateCipherAsync(_gating.Id);

        var response = await Client.PostAsJsonAsync("ciphers/bulk-collections", new
        {
            organizationId = Organization.Id,
            cipherIds = new[] { cipher.Id },
            collectionIds = new[] { _gating.Id },
            removeCollections = true,
        });
        response.EnsureSuccessStatusCode();

        var row = Assert.Single(await GetTrailAsync("credentialUngated"))!;
        Assert.Equal(cipher.Id, row["cipherId"]!.GetValue<Guid>());
        Assert.True(row["collectionId"] == null || row["collectionId"]!.GetValue<Guid?>() == null);
    }

    [Fact]
    public async Task ExportingTheWholeVault_CarryingAGatedItem_RecordsTheExport()
    {
        await CreateCipherAsync(_gating.Id);
        await CreateCipherAsync(_plain.Id);

        var response = await Client.GetAsync($"organizations/{Organization.Id}/export");
        response.EnsureSuccessStatusCode();

        var row = Assert.Single(await GetTrailAsync("vaultExported"))!;
        Assert.Equal(_ownerId, row["actorId"]!.GetValue<Guid>());
        Assert.True(row["cipherId"] == null || row["cipherId"]!.GetValue<Guid?>() == null);
        Assert.False(row["incomplete"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ExportingTheWholeVault_CarryingNoGatedItem_RecordsNothing()
    {
        await CreateCipherAsync(_plain.Id);

        var response = await Client.GetAsync($"organizations/{Organization.Id}/export");
        response.EnsureSuccessStatusCode();

        Assert.Empty(await GetTrailAsync("vaultExported"));
    }

    [Fact]
    public async Task ExportingTheWholeVault_InAnOrganizationWithoutPrivilegedControls_RecordsNothing()
    {
        Organization.UsePam = false;
        await Factory.GetService<IOrganizationRepository>().UpsertAsync(Organization);
        await Factory.GetService<IOrganizationAbilityCacheService>()
            .UpsertOrganizationAbilityAsync(Organization, CancellationToken.None);
        await CreateCipherAsync(_gating.Id);

        var response = await Client.GetAsync($"organizations/{Organization.Id}/export");
        response.EnsureSuccessStatusCode();

        Assert.Empty(await GetTrailAsync("vaultExported"));
    }

    private async Task<Cipher> CreateCipherAsync(params Guid[] collectionIds)
    {
        var cipher = new Cipher { Type = CipherType.Login, OrganizationId = Organization.Id, Data = "{}" };
        await Factory.GetService<ICipherRepository>().CreateAsync(cipher, collectionIds);
        return cipher;
    }

    private async Task<JsonArray> GetTrailAsync(string kind)
    {
        var response = await Client.GetAsync($"organizations/{Organization.Id}/audit?kind={kind}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["data"]!.AsArray();
    }
}
