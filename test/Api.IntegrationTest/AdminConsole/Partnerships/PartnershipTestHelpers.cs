using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public static class PartnershipTestHelpers
{
    /// <summary>
    /// Turns on <see cref="FeatureFlagKeys.PartnerSponsorships"/>. Call before the factory creates its first client.
    /// </summary>
    public static void EnablePartnerSponsorships(ApiApplicationFactory factory) =>
        factory.SubstituteService<IFeatureService>(featureService =>
            featureService.IsEnabled(FeatureFlagKeys.PartnerSponsorships).Returns(true));

    /// <summary>
    /// Creates an organization, optionally gives it a partnership with <paramref name="status"/>, and returns a client
    /// authenticated with the organization's API key.
    /// </summary>
    public static async Task<(HttpClient Client, OrganizationPartnership? Partnership)> CreateOrganizationClientAsync(
        ApiApplicationFactory factory, PartnershipStatus? status)
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await factory.LoginWithNewAccount(ownerEmail);
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        OrganizationPartnership? partnership = null;
        if (status is { } partnershipStatus)
        {
            partnership = await factory.GetService<IOrganizationPartnershipRepository>().CreateAsync(
                new OrganizationPartnership
                {
                    OrganizationId = organization.Id,
                    Name = "Integration Test Partner",
                    Status = partnershipStatus,
                });
        }

        var client = factory.CreateClient();
        await new LoginHelper(factory, client).LoginWithOrganizationApiKeyAsync(organization.Id);
        return (client, partnership);
    }
}
