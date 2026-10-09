namespace Bit.Subscriptions.Organization.Models.Requests;

// Matches the client's existing request builder.
internal record PreviewOrganizationSubscriptionPurchaseRequest(
    PurchaseSelections? Purchase,
    BillingAddressSelections? BillingAddress);
