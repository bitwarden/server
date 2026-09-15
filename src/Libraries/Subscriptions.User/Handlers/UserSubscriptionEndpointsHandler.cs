using System.Security.Claims;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Commands;
using Bit.Subscriptions.User.Models.Requests;

namespace Bit.Subscriptions.User.Handlers;

internal sealed class UserSubscriptionEndpointsHandler(
    IUserService userService,
    IPreviewPremiumUpgradeCommand previewPremiumUpgradeCommand)
{
    public async Task<InvoicePreview> PreviewPremiumUpgradeAsync(ClaimsPrincipal principal, PreviewPremiumUpgradeRequest request)
    {
        var user = await userService.GetUserByPrincipalAsync(principal)
            ?? throw new UnauthorizedAccessException();

        return await previewPremiumUpgradeCommand.Run(user, request);
    }
}
