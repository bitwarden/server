using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews.Models;
using Microsoft.Extensions.Logging;

namespace Bit.Subscriptions.User.Queries;

/// <summary>Post-call checks shared by the purchase preview queries.</summary>
internal static class PurchasePreviewGuard
{
    /// <summary>User-safe message for catalog faults the caller cannot fix (409).</summary>
    internal const string CatalogFaultMessage = "The plan could not be previewed. Please contact support for assistance.";

    internal static InvoicePreview RequireSeats(InvoicePreview preview, ILogger logger, Guid userId, IEnumerable<string> priceIds)
    {
        if (preview.PasswordManager.Seats is not null)
        {
            return preview;
        }

        logger.LogError(
            "Purchase preview for user ({UserId}) resolved no Password Manager seats line. Prices={PriceIds}",
            userId, string.Join(",", priceIds));
        throw new ConflictException(CatalogFaultMessage);
    }
}
