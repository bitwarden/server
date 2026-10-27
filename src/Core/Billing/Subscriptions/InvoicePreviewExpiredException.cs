using Bit.Core.Billing.Constants;
using Bit.Core.Exceptions;

namespace Bit.Core.Billing.Subscriptions;

public sealed class InvoicePreviewExpiredException() : BadRequestException(
    BillingErrorKeys.InvoicePreviewExpired,
    "Your invoice preview is out of date. Please refresh it and try again.");
