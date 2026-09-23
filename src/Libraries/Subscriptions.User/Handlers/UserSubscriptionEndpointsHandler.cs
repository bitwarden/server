using System.Security.Claims;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Models.Requests;
using Bit.Subscriptions.User.Queries;

namespace Bit.Subscriptions.User.Handlers;

internal sealed class UserSubscriptionEndpointsHandler(
    IUserService userService,
    IGetSubscriptionUpgradePreviewQuery getSubscriptionUpgradePreviewQuery)
{
    public async Task<InvoicePreview> GetUpgradePreviewAsync(ClaimsPrincipal principal, GetSubscriptionUpgradePreviewRequest request)
    {
        var user = await userService.GetUserByPrincipalAsync(principal)
            ?? throw new UnauthorizedAccessException();

        return await getSubscriptionUpgradePreviewQuery.Run(user, request);
    }
}
