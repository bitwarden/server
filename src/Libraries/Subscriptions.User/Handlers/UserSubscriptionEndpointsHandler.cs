using System.Security.Claims;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
using Bit.Subscriptions.User.Models.Requests;
using Bit.Subscriptions.User.Queries;

namespace Bit.Subscriptions.User.Handlers;

internal sealed class UserSubscriptionEndpointsHandler(
    IUserService userService,
    IGetSubscriptionUpgradePreviewQuery getSubscriptionUpgradePreviewQuery,
    IGetSubscriptionPreviewQuery getSubscriptionPreviewQuery)
{
    public async Task<InvoicePreview> GetUpgradePreviewAsync(ClaimsPrincipal principal, GetSubscriptionUpgradePreviewRequest request)
    {
        var user = await userService.GetUserByPrincipalAsync(principal)
            ?? throw new UnauthorizedAccessException();

        return await getSubscriptionUpgradePreviewQuery.Run(user, request);
    }

    public async Task<SubscriptionPreview> GetPreviewAsync(ClaimsPrincipal principal)
    {
        var user = await userService.GetUserByPrincipalAsync(principal)
            ?? throw new NotFoundException();

        return await getSubscriptionPreviewQuery.Run(user)
            ?? throw new NotFoundException();
    }
}
