namespace Bit.Subscriptions.User.Models.Requests;

internal record GetPremiumPurchasePreviewRequest(
    short? AdditionalStorage,
    string[]? Coupons,
    string? Country,
    string? PostalCode);
