using System.Security.Claims;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;

namespace Bit.Subscriptions.User.Handlers;

internal sealed class GetAccountSubscriptionPreviewHandler(
    IUserService userService,
    IGetSubscriptionPreviewQuery getSubscriptionPreviewQuery)
{
    public async Task<SubscriptionPreview> HandleAsync(ClaimsPrincipal principal)
    {
        var user = await userService.GetUserByPrincipalAsync(principal)
            ?? throw new NotFoundException();

        return await getSubscriptionPreviewQuery.Run(user)
            ?? throw new NotFoundException();
    }
}
