using System.ComponentModel.DataAnnotations;
using System.Net;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities.Provider;

/// <summary>
/// An entity that can manage multiple client organizations on behalf of their owners - for example, an IT
/// services business.
/// </summary>
/// <remarks>
/// Members are associated with the provider via <see cref="ProviderUser"/> and client organizations via
/// <see cref="ProviderOrganization"/>. There are different types as providers as described in <see cref="ProviderType"/>.
/// </remarks>
public class Provider : ITableObject<Guid>, ISubscriber
{
    /// <summary>
    /// A unique identifier for the provider.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// This value is HTML encoded. For display purposes use the method DisplayName() instead.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// This value is HTML encoded. For display purposes use the method DisplayBusinessName() instead.
    /// </summary>
    public string? BusinessName { get; set; }
    /// <summary>
    /// The first line of the provider's business address.
    /// </summary>
    public string? BusinessAddress1 { get; set; }
    /// <summary>
    /// The second line of the provider's business address.
    /// </summary>
    public string? BusinessAddress2 { get; set; }
    /// <summary>
    /// The third line of the provider's business address.
    /// </summary>
    public string? BusinessAddress3 { get; set; }
    /// <summary>
    /// The two-letter ISO country code of the provider's business address.
    /// </summary>
    public string? BusinessCountry { get; set; }
    /// <summary>
    /// The provider's tax identification number.
    /// </summary>
    public string? BusinessTaxNumber { get; set; }
    /// <summary>
    /// The billing email address for the provider.
    /// </summary>
    public string? BillingEmail { get; set; }
    /// <summary>
    /// The billing phone number for the provider.
    /// </summary>
    public string? BillingPhone { get; set; }
    /// <summary>
    /// The current status of the provider, representing its lifecycle state.
    /// </summary>
    public ProviderStatusType Status { get; set; }
    /// <summary>
    /// If true, event logging is enabled for the provider.
    /// </summary>
    public bool UseEvents { get; set; }
    /// <summary>
    /// The type of provider, which determines its capabilities and billing model.
    /// </summary>
    public ProviderType Type { get; set; }
    /// <summary>
    /// If true, the provider is active. If false, the provider and its managed organizations are disabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// The date the provider was created.
    /// </summary>
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    /// <summary>
    /// The date the provider was last updated.
    /// </summary>
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
    /// <summary>
    /// The payment gateway used for billing.
    /// </summary>
    public GatewayType? Gateway { get; set; }
    /// <summary>
    /// The provider's customer ID in the payment gateway.
    /// </summary>
    [MaxLength(50)]
    public string? GatewayCustomerId { get; set; }
    /// <summary>
    /// The provider's subscription ID in the payment gateway.
    /// </summary>
    [MaxLength(50)]
    public string? GatewaySubscriptionId { get; set; }
    /// <summary>
    /// A discount ID applied to the provider's subscription in the payment gateway.
    /// </summary>
    public string? DiscountId { get; set; }

    /// <inheritdoc/>
    public string? BillingEmailAddress() => BillingEmail?.ToLowerInvariant().Trim();

    /// <inheritdoc/>
    public string? BillingName() => DisplayBusinessName();

    /// <inheritdoc/>
    public string? SubscriberName() => DisplayName();

    /// <inheritdoc/>
    public string BraintreeCustomerIdPrefix() => "p";

    /// <inheritdoc/>
    public string BraintreeIdField() => "provider_id";

    /// <inheritdoc/>
    public string BraintreeCloudRegionField() => "region";

    /// <inheritdoc/>
    public bool IsOrganization() => false;

    /// <inheritdoc/>
    public bool IsUser() => false;

    /// <inheritdoc/>
    public string SubscriberType() => "Provider";

    /// <inheritdoc/>
    public bool IsExpired() => false;

    /// <summary>
    /// Initializes <see cref="Id"/> to a new COMB GUID.
    /// </summary>
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
