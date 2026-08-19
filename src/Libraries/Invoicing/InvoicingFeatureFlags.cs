using Bitwarden.Server.Sdk.Features;

namespace Bit.Invoicing;

/// <summary>
/// Feature flag keys owned by <c>Bit.Invoicing</c> and the subscription features layered on it.
/// <see cref="InvoicingServiceCollectionExtensions.AddInvoicing"/> registers these as known flags,
/// so consumers gate on them without depending on <c>Core</c> for the key.
/// </summary>
[FlagKeyCollection]
public static partial class InvoicingFeatureFlags
{
    /// <summary>Gates the preview-driven cart surface (the subscription preview endpoints).</summary>
    public const string PM36631_PreviewDrivenCart = "pm-36631-preview-driven-cart";
}
