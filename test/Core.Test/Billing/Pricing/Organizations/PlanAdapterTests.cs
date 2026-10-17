using System.Text.Json;
using Bit.Core.Billing.Pricing.Organizations;
using Xunit;

namespace Bit.Core.Test.Billing.Pricing.Organizations;

public class PlanAdapterTests
{
    private const string _privilegedControlsJson = """
        {
            "seats": {
                "type": "scalable",
                "provided": 0,
                "price": 72,
                "stripePriceId": "privileged-controls-enterprise-seat-annually"
            },
            "defaultSeatMinimum": 10,
            "promotionalSeatMinimums": [4, 6, 8]
        }
        """;

    [Fact]
    public void Constructor_WithPrivilegedControls_MapsSeatStripePriceId()
    {
        var plan = new PlanAdapter(CreateEnterprisePlan(_privilegedControlsJson));

        Assert.Equal("privileged-controls-enterprise-seat-annually", plan.PrivilegedControls.StripeSeatPlanId);
    }

    [Fact]
    public void Constructor_WithPrivilegedControls_MapsSeatPrice()
    {
        var plan = new PlanAdapter(CreateEnterprisePlan(_privilegedControlsJson));

        Assert.Equal(72M, plan.PrivilegedControls.SeatPrice);
    }

    [Fact]
    public void Constructor_WithPrivilegedControls_MapsBaseSeats()
    {
        var plan = new PlanAdapter(CreateEnterprisePlan(_privilegedControlsJson));

        Assert.Equal(0, plan.PrivilegedControls.BaseSeats);
    }

    [Fact]
    public void Constructor_WithPrivilegedControls_MapsDefaultSeatMinimum()
    {
        var plan = new PlanAdapter(CreateEnterprisePlan(_privilegedControlsJson));

        Assert.Equal(10, plan.PrivilegedControls.DefaultSeatMinimum);
    }

    [Fact]
    public void Constructor_WithPrivilegedControls_MapsPromotionalSeatMinimums()
    {
        var plan = new PlanAdapter(CreateEnterprisePlan(_privilegedControlsJson));

        Assert.Equal(new[] { 4, 6, 8 }, plan.PrivilegedControls.PromotionalSeatMinimums);
    }

    [Fact]
    public void Constructor_WithPrivilegedControls_SupportsPrivilegedControls()
    {
        var plan = new PlanAdapter(CreateEnterprisePlan(_privilegedControlsJson));

        Assert.True(plan.SupportsPrivilegedControls);
    }

    [Fact]
    public void Constructor_WithNullPrivilegedControls_DoesNotSupportPrivilegedControls()
    {
        var plan = new PlanAdapter(CreateEnterprisePlan("null"));

        Assert.False(plan.SupportsPrivilegedControls);
    }

    [Fact]
    public void Constructor_WithNullPrivilegedControls_LeavesPrivilegedControlsNull()
    {
        var plan = new PlanAdapter(CreateEnterprisePlan("null"));

        Assert.Null(plan.PrivilegedControls);
    }

    [Fact]
    public void Constructor_WithoutPrivilegedControlsProperty_DoesNotSupportPrivilegedControls()
    {
        var plan = new PlanAdapter(CreateEnterprisePlan(privilegedControlsJson: null));

        Assert.False(plan.SupportsPrivilegedControls);
    }

    [Fact]
    public void Constructor_WithPrivilegedControls_LeavesPasswordManagerUnchanged()
    {
        var withPrivilegedControls = new PlanAdapter(CreateEnterprisePlan(_privilegedControlsJson));
        var withoutPrivilegedControls = new PlanAdapter(CreateEnterprisePlan("null"));

        Assert.Equal(withoutPrivilegedControls.PasswordManager, withPrivilegedControls.PasswordManager);
    }

    [Fact]
    public void Constructor_WithPrivilegedControls_LeavesSecretsManagerUnchanged()
    {
        var withPrivilegedControls = new PlanAdapter(CreateEnterprisePlan(_privilegedControlsJson));
        var withoutPrivilegedControls = new PlanAdapter(CreateEnterprisePlan("null"));

        Assert.Equal(withoutPrivilegedControls.SecretsManager, withPrivilegedControls.SecretsManager);
    }

    private static Plan CreateEnterprisePlan(string? privilegedControlsJson)
    {
        var privilegedControlsProperty = privilegedControlsJson != null
            ? $@"""privilegedControls"": {privilegedControlsJson},"
            : string.Empty;

        var json = $$"""
            {
                "lookupKey": "enterprise-annually",
                "name": "Enterprise",
                "tier": "enterprise",
                "cadence": "annually",
                "available": true,
                "features": [],
                "seats": {
                    "type": "scalable",
                    "provided": 0,
                    "price": 72,
                    "stripePriceId": "enterprise-seat-annually"
                },
                "secretsManager": {
                    "seats": {
                        "type": "scalable",
                        "provided": 0,
                        "price": 144,
                        "stripePriceId": "secrets-manager-enterprise-seat-annually"
                    },
                    "serviceAccounts": {
                        "type": "scalable",
                        "provided": 50,
                        "price": 12,
                        "stripePriceId": "secrets-manager-service-account-annually"
                    }
                },
                {{privilegedControlsProperty}}
                "canUpgradeTo": [],
                "additionalData": {
                    "nameLocalizationKey": "planNameEnterprise",
                    "descriptionLocalizationKey": "planDescEnterprise"
                }
            }
            """;

        return JsonSerializer.Deserialize<Plan>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
