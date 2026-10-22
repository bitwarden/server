namespace Bit.Subscriptions.Organization.Models.Requests;

internal record PreviewOrganizationRestartRequest
{
    public BillingAddressSelections? BillingAddress { get; init; }
}
