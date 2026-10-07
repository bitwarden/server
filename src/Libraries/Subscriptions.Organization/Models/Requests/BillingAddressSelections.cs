namespace Bit.Subscriptions.Organization.Models.Requests;

internal record BillingAddressSelections(string? Country, string? PostalCode, TaxIdSelection? TaxId);
