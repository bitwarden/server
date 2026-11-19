using Bitwarden.Server.Sdk.Features;

namespace Bit.Invoicing;

/// <summary>
/// Feature flag keys owned by <c>Bit.Invoicing</c>. It currently defines no flags and is kept so the
/// library can register future ones: <see cref="InvoicingServiceCollectionExtensions.AddInvoicing"/>
/// registers whatever is defined here as known flags, so consumers can gate on them without depending
/// on <c>Core</c> for the key.
/// </summary>
[FlagKeyCollection]
public static partial class InvoicingFeatureFlags
{
}
