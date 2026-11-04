using System.Security.Claims;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Models.Requests;
using Bit.Subscriptions.Organization.Queries;

namespace Bit.Subscriptions.Organization.Handlers;

internal sealed class PreviewOrganizationSubscriptionPurchaseHandler(
    IUserService userService,
    IPreviewOrganizationSubscriptionPurchaseQuery previewOrganizationSubscriptionPurchaseQuery)
{
    public async Task<InvoicePreview> HandleAsync(ClaimsPrincipal principal, PreviewOrganizationSubscriptionPurchaseRequest request)
    {
        var user = await userService.GetUserByPrincipalAsync(principal)
            ?? throw new UnauthorizedAccessException();

        return await previewOrganizationSubscriptionPurchaseQuery.Run(user, request);
    }
}
