using Bit.Core.Services;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Billing-specific Admin host flows on top of the shared <see cref="AdminApplicationFactory"/>.
/// </summary>
public static class AdminApplicationFactoryExtensions
{
    /// <summary>
    /// Posts to the Admin business-unit conversion endpoint for the given
    /// organization and returns the invitation token that would otherwise have
    /// been emailed to the provider admin.
    /// </summary>
    public static async Task<string> InitializeBusinessUnitConversionAsync(
        this AdminApplicationFactory admin, HttpClient adminSession, Guid organizationId, string providerAdminEmail)
    {
        var mailService = admin.Services.GetRequiredService<IMailService>();

        var response = await adminSession.PostAsync(
            $"/organizations/billing/{organizationId}/business-unit",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                { "ProviderAdminEmail", providerAdminEmail },
            }));
        await Assert.SuccessResponseAsync(response);

        var token = mailService.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMailService.SendBusinessUnitConversionInviteAsync))
            .Where(c => (string?)c.GetArguments()[2] == providerAdminEmail)
            .Select(c => (string?)c.GetArguments()[1])
            .LastOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail($"No business-unit conversion token captured for {providerAdminEmail}.");
        }

        return token;
    }
}
