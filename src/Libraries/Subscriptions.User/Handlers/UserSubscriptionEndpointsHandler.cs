using System.Security.Claims;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Commands;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Models.Requests;

namespace Bit.Subscriptions.User.Handlers;

internal sealed class UserSubscriptionEndpointsHandler(
    IUserService userService,
    IBuildInvoicePreviewForPremiumOrgUpgradeCommand buildInvoicePreviewForPremiumOrgUpgradeCommand)
{
    public async Task<InvoicePreview> PreviewPremiumOrgUpgradeAsync(
        ClaimsPrincipal principal,
        PreviewInvoiceForPremiumOrgUpgradeRequest request)
    {
        // Minimal APIs cannot use the MVC [InjectUser] filter, so resolve the user from the principal here.
        var user = await userService.GetUserByPrincipalAsync(principal)
            ?? throw new UnauthorizedAccessException();

        var (planType, billingAddress) = request.ToDomain();

        return await buildInvoicePreviewForPremiumOrgUpgradeCommand.Run(user, planType, billingAddress);
    }
}
