using System.Net;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.AdminConsole.Entities.Provider;

public class Provider : ITableObject<Guid>, ISubscriber
{
    public Guid Id { get; set; }

    /// <summary>
    /// This value is HTML encoded. For display purposes use the method DisplayName() instead.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// This value is HTML encoded. For display purposes use the method DisplayBusinessName() instead.
    /// </summary>
    public string? BusinessName { get; set; }
    public string? BusinessAddress1 { get; set; }
    public string? BusinessAddress2 { get; set; }
    public string? BusinessAddress3 { get; set; }
    public string? BusinessCountry { get; set; }
    public string? BusinessTaxNumber { get; set; }
    public string? BillingEmail { get; set; }
    public string? BillingPhone { get; set; }
    public ProviderStatusType Status { get; set; }
    public bool UseEvents { get; set; }
    public ProviderType Type { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
    public GatewayType? Gateway { get; set; }
    public string? GatewayCustomerId { get; set; }
    public string? GatewaySubscriptionId { get; set; }

    public string? BillingEmailAddress() => BillingEmail?.ToLowerInvariant().Trim();

    public string? BillingName() => DisplayBusinessName();

    public string? SubscriberName() => DisplayName();

    public string BraintreeCustomerIdPrefix() => "p";

    public string BraintreeIdField() => "provider_id";

    public string BraintreeCloudRegionField() => "region";

    public bool IsOrganization() => false;

    public bool IsUser() => false;

    public string SubscriberType() => "Provider";

    public bool IsExpired() => false;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }

    /// <summary>
    /// Returns the name of the provider, HTML decoded ready for display.
    /// </summary>
    public string? DisplayName()
    {
        return WebUtility.HtmlDecode(Name);
    }

    /// <summary>
    /// Returns the business name of the provider, HTML decoded ready for display.
    /// </summary>
    public string? DisplayBusinessName()
    {
        return WebUtility.HtmlDecode(BusinessName);
    }
}
