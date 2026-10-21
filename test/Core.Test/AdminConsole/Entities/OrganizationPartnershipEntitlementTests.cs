using Bit.Core.AdminConsole.Entities;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Entities;

public class OrganizationPartnershipEntitlementTests
{
    private static readonly byte[] _hashKey = "test-hash-key"u8.ToArray();

    [Fact]
    public void ComputeExternalIdHash_SameInputs_ReturnsSameHash()
    {
        var partnershipId = Guid.NewGuid();

        Assert.Equal(
            OrganizationPartnershipEntitlement.ComputeExternalIdHash(_hashKey, partnershipId, "cust_8827341"),
            OrganizationPartnershipEntitlement.ComputeExternalIdHash(_hashKey, partnershipId, "cust_8827341"));
    }

    [Fact]
    public void ComputeExternalIdHash_DifferentPartnerships_ReturnsDifferentHashes()
    {
        Assert.NotEqual(
            OrganizationPartnershipEntitlement.ComputeExternalIdHash(_hashKey, Guid.NewGuid(), "cust_8827341"),
            OrganizationPartnershipEntitlement.ComputeExternalIdHash(_hashKey, Guid.NewGuid(), "cust_8827341"));
    }

    [Fact]
    public void ComputeExternalIdHash_DifferentKeys_ReturnsDifferentHashes()
    {
        var partnershipId = Guid.NewGuid();

        Assert.NotEqual(
            OrganizationPartnershipEntitlement.ComputeExternalIdHash(_hashKey, partnershipId, "cust_8827341"),
            OrganizationPartnershipEntitlement.ComputeExternalIdHash("other-key"u8.ToArray(), partnershipId, "cust_8827341"));
    }

    [Fact]
    public void ComputeExternalIdHash_IsSixtyFourHexCharacters()
    {
        var hash = OrganizationPartnershipEntitlement.ComputeExternalIdHash(_hashKey, Guid.NewGuid(), "cust_8827341");

        Assert.Matches("^[0-9A-F]{64}$", hash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetExternalIdHashKey_NotConfigured_Throws(string? key)
    {
        var settings = new GlobalSettings.PartnershipSettings { ExternalIdHashKey = key! };

        Assert.Throws<InvalidOperationException>(() => settings.GetExternalIdHashKey());
    }

    [Fact]
    public void Metadata_RoundTrips()
    {
        var entitlement = new OrganizationPartnershipEntitlement();
        var metadata = new Dictionary<string, string> { ["productSku"] = "PARTNER_PREMIUM_SECURITY" };

        entitlement.SetMetadata(metadata);

        Assert.Equal(metadata, entitlement.GetMetadata());
    }

    [Fact]
    public void SetMetadata_Empty_StoresNull()
    {
        var entitlement = new OrganizationPartnershipEntitlement();

        entitlement.SetMetadata(new Dictionary<string, string>());

        Assert.Null(entitlement.Metadata);
        Assert.Empty(entitlement.GetMetadata());
    }

    [Fact]
    public void SetNewAccountRef_ReplacesExistingValue()
    {
        var entitlement = new OrganizationPartnershipEntitlement();
        entitlement.SetNewAccountRef();
        var first = entitlement.AccountRef;

        entitlement.SetNewAccountRef();

        Assert.NotNull(first);
        Assert.NotEqual(first, entitlement.AccountRef);
    }

    [Fact]
    public void SetNewId_KeepsExistingId()
    {
        var id = Guid.NewGuid();
        var entitlement = new OrganizationPartnershipEntitlement { Id = id };

        entitlement.SetNewId();

        Assert.Equal(id, entitlement.Id);
    }
}
