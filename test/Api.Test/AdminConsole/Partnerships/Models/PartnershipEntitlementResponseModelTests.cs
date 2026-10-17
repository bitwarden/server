using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships.Models;

public class PartnershipEntitlementResponseModelTests
{
    private static readonly JsonSerializerOptions _apiOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Serialize_FullyPopulated_MatchesTheExportRecordFieldForField()
    {
        var entitlement = CreateCanceledEntitlement();

        var response = Serialize(new PartnershipEntitlementResponseModel(entitlement));
        var record = JsonDocument.Parse(JsonSerializer.Serialize(PartnershipReconciliationExportRecord.From(entitlement),
            PartnershipReconciliationExportRecord.SerializerOptions)).RootElement;

        Assert.Equal(
            record.EnumerateObject().Select(p => p.Name).Prepend("object"),
            response.EnumerateObject().Select(p => p.Name));
        foreach (var property in record.EnumerateObject())
        {
            Assert.Equal(property.Value.GetRawText(), response.GetProperty(property.Name).GetRawText());
        }
        Assert.Equal("entitlement", response.GetProperty("object").GetString());
    }

    [Fact]
    public void Serialize_Provisioned_OmitsUnsetFields()
    {
        var entitlement = new OrganizationPartnershipEntitlement
        {
            ExternalId = "customer-1",
            State = PartnershipEntitlementState.Provisioned,
            SponsoredPlanType = SponsoredPlanType.Premium,
        };

        var response = Serialize(new PartnershipEntitlementResponseModel(entitlement));

        Assert.Equal(
            ["object", "externalId", "state", "planType", "metadata", "lastAppliedAt", "createdAt", "updatedAt"],
            response.EnumerateObject().Select(p => p.Name));
        Assert.Equal("provisioned", response.GetProperty("state").GetString());
        Assert.Equal("premium", response.GetProperty("planType").GetString());
    }

    [Fact]
    public void Serialize_BoundEntitlement_NeverIncludesTheUserId()
    {
        var entitlement = CreateCanceledEntitlement();

        var json = JsonSerializer.Serialize(new PartnershipEntitlementResponseModel(entitlement), _apiOptions);

        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(entitlement.UserId!.Value.ToString(), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_ProvisionResultWithActivationLink_IncludesOutcomeAndLink()
    {
        var entitlement = CreateCanceledEntitlement();
        var expiresAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var result = new ProvisionPartnershipEntitlementResult(entitlement, Created: false, Applied: false,
            PartnershipEntitlementAppliedReasons.StaleTransition, entitlement.LastAppliedEffectiveDate);

        var response = Serialize(new PartnershipEntitlementResponseModel(result,
            new PartnershipActivationLink("https://vault.example.com/#/partner-activation?token=t", "t", expiresAt)));

        Assert.False(response.GetProperty("applied").GetBoolean());
        Assert.Equal("stale_transition", response.GetProperty("appliedReason").GetString());
        Assert.Equal("https://vault.example.com/#/partner-activation?token=t",
            response.GetProperty("activationUrl").GetString());
        Assert.Equal(expiresAt, response.GetProperty("activationUrlExpiresAt").GetDateTime());
    }

    [Fact]
    public void Serialize_ProvisionResultWithoutActivationLink_OmitsLinkAndReason()
    {
        var entitlement = CreateCanceledEntitlement();
        var result = new ProvisionPartnershipEntitlementResult(entitlement, Created: false, Applied: false, null,
            entitlement.LastAppliedEffectiveDate);

        var response = Serialize(new PartnershipEntitlementResponseModel(result, activationLink: null));

        Assert.False(response.GetProperty("applied").GetBoolean());
        Assert.False(response.TryGetProperty("appliedReason", out _));
        Assert.False(response.TryGetProperty("activationUrl", out _));
        Assert.False(response.TryGetProperty("activationUrlExpiresAt", out _));
    }

    private static JsonElement Serialize(PartnershipEntitlementResponseModel model) =>
        JsonDocument.Parse(JsonSerializer.Serialize(model, _apiOptions)).RootElement;

    private static OrganizationPartnershipEntitlement CreateCanceledEntitlement()
    {
        var date = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Unspecified);
        var entitlement = new OrganizationPartnershipEntitlement
        {
            ExternalId = "customer-1",
            State = PartnershipEntitlementState.Canceled,
            SponsoredPlanType = SponsoredPlanType.Premium,
            UserId = Guid.NewGuid(),
            AccountRef = Guid.NewGuid(),
            BoundDate = date,
            SuspendedDate = date.AddDays(1),
            CanceledDate = date.AddDays(2),
            ResumeWindowExpirationDate = date.AddDays(32),
            LastAppliedEffectiveDate = date.AddDays(2),
            CreationDate = date.AddDays(-1),
            RevisionDate = date.AddDays(2),
        };
        entitlement.SetMetadata(new Dictionary<string, string> { ["Region"] = "EU", ["tier"] = "gold" });
        return entitlement;
    }
}
