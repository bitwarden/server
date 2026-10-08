using Bit.Core.AdminConsole.Entities;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Entities;

public class OrganizationPartnershipEntitlementTests
{
    [Fact]
    public void ComputeExternalIdHash_SameInputs_ReturnsSameHash()
    {
        var partnershipId = Guid.NewGuid();

        Assert.Equal(
            OrganizationPartnershipEntitlement.ComputeExternalIdHash(partnershipId, "cust_8827341"),
            OrganizationPartnershipEntitlement.ComputeExternalIdHash(partnershipId, "cust_8827341"));
    }

    [Fact]
    public void ComputeExternalIdHash_DifferentPartnerships_ReturnsDifferentHashes()
    {
        Assert.NotEqual(
            OrganizationPartnershipEntitlement.ComputeExternalIdHash(Guid.NewGuid(), "cust_8827341"),
            OrganizationPartnershipEntitlement.ComputeExternalIdHash(Guid.NewGuid(), "cust_8827341"));
    }

    [Fact]
    public void ComputeExternalIdHash_IsSixtyFourHexCharacters()
    {
        var hash = OrganizationPartnershipEntitlement.ComputeExternalIdHash(Guid.NewGuid(), "cust_8827341");

        Assert.Matches("^[0-9A-F]{64}$", hash);
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
