namespace Bit.Subscriptions.User.Models.Requests;

internal record GetSubscriptionPurchasePreviewRequest(
    short? AdditionalStorage,
    string[]? Coupons,
    string? Country,
    string? PostalCode);
