using System.Reflection;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.Models;
using Bit.Core.Billing.Licenses.Services.Implementations;
using Bit.Core.Models.Business;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Billing.Licenses.Services.Implementations;

public class OrganizationLicenseClaimsFactoryTests
{
    [Theory, BitAutoData]
    public async Task GenerateClaims_CreatesClaimsForAllConstants(Organization organization)
    {
        // This test ensures that when a constant is added to OrganizationLicenseConstants,
        // it is also added to the OrganizationLicenseClaimsFactory to generate claims.
        // This is the second step in the license synchronization pipeline:
        // Property → Constant → Claim → Extraction → Application

        // 1. Populate all nullable properties to ensure claims can be generated
        // The factory only adds claims for properties that have values
        organization.Name = "Test Organization";
        organization.BillingEmail = "billing@test.com";
        organization.BusinessName = "Test Business";
        organization.Plan = "Enterprise";
        organization.LicenseKey = "test-license-key";
        organization.Seats = 100;
        organization.MaxCollections = 50;
        organization.MaxStorageGb = 10;
        organization.SmSeats = 25;
        organization.SmServiceAccounts = 10;
        organization.ExpirationDate = DateTime.UtcNow.AddYears(1); // Ensure org is not expired

        // Create a LicenseContext with a minimal SubscriptionInfo to trigger conditional claims
        // ExpirationWithoutGracePeriod is only generated for active, non-trial, annual subscriptions
        var licenseContext = new LicenseContext
        {
            InstallationId = Guid.NewGuid(),
            SubscriptionInfo = new SubscriptionInfo
            {
                Subscription = new SubscriptionInfo.BillingSubscription(null!)
                {
                    TrialEndDate = DateTime.UtcNow.AddDays(-30), // Trial ended in the past
                    PeriodStartDate = DateTime.UtcNow,
                    PeriodEndDate = DateTime.UtcNow.AddDays(365), // Annual subscription (>180 days)
                    Status = "active"
                }
            }
        };

        // 2. Generate claims
        var factory = new OrganizationLicenseClaimsFactory();
        var claims = await factory.GenerateClaims(organization, licenseContext);

        // 3. Get all constants from OrganizationLicenseConstants
        var allConstants = typeof(OrganizationLicenseConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null) as string)
            .ToHashSet();

        // 4. Get claim types from generated claims
        var generatedClaimTypes = claims.Select(c => c.Type).ToHashSet();

        // 5. Find constants that don't have corresponding claims
        var constantsWithoutClaims = allConstants
            .Except(generatedClaimTypes)
            .OrderBy(c => c)
            .ToList();

        // 6. Build error message with guidance
        var errorMessage = "";
        if (constantsWithoutClaims.Any())
        {
            errorMessage = $"The following constants in OrganizationLicenseConstants are NOT generated as claims in OrganizationLicenseClaimsFactory:\n";
            errorMessage += string.Join("\n", constantsWithoutClaims.Select(c => $"  - {c}"));
            errorMessage += "\n\nPlease add the following claims to OrganizationLicenseClaimsFactory.GenerateClaims():\n";
            foreach (var constant in constantsWithoutClaims)
            {
                errorMessage += $"  new(nameof(OrganizationLicenseConstants.{constant}), entity.{constant}.ToString()),\n";
            }
            errorMessage += "\nNote: If the property is nullable, you may need to add it conditionally.";
        }

        // 7. Assert - if this fails, the error message guides the developer to add claim generation
        Assert.True(
            !constantsWithoutClaims.Any(),
            $"\n{errorMessage}");
    }
}
