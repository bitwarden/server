using System.Security.Claims;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Models.Requests;
using Bit.Subscriptions.User.Queries;

namespace Bit.Subscriptions.User.Handlers;

internal sealed class GetAccountOrganizationPurchasePreviewHandler(
    IUserService userService,
    IGetOrganizationPurchasePreviewQuery getOrganizationPurchasePreviewQuery)
{
    public async Task<InvoicePreview> HandleAsync(ClaimsPrincipal principal, GetOrganizationPurchasePreviewRequest request)
    {
        var user = await userService.GetUserByPrincipalAsync(principal)
            ?? throw new UnauthorizedAccessException();

        return await getOrganizationPurchasePreviewQuery.Run(user, request);
    }
}
