using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Models.Business;

public class UserLicenseTests
{
    /// <summary>
    /// Known good GetDataBytes output for hash data (forHash: true) for UserLicense version 1.
    /// This value was verified to be correct on initial implementation and serves as a regression baseline.
    /// NOTE: License versions are now frozen. Use the JWT Token property to add new claims instead of incrementing the version.
    /// </summary>
    private const string _knownGoodUserLicenseHashData = "license:user|Email:test@example.com|Expires:1736208000|Id:12300000-0000-0000-0000-000000000789|LicenseKey:myUserLicenseKey|MaxStorageGb:10|Name:Test User|Premium:true|Trial:false|Version:1";

    /// <summary>
    /// Known good GetDataBytes output for signature data (forHash: false) for UserLicense version 1.
    /// This value was verified to be correct on initial implementation and serves as a regression baseline.
    /// NOTE: License versions are now frozen. Use the JWT Token property to add new claims instead of incrementing the version.
    /// </summary>
    private const string _knownGoodUserLicenseSignatureData = "license:user|Email:test@example.com|Expires:1736208000|Hash:oZEopNmWvWQNE3Lnsh/LP2OPo6+IHxjTcpdIse/viQk=|Id:12300000-0000-0000-0000-000000000789|Issued:1758888041|LicenseKey:myUserLicenseKey|MaxStorageGb:10|Name:Test User|Premium:true|Refresh:1735603200|Trial:false|Version:1";

    /// <summary>
    /// Regression test that verifies GetDataBytes output for hash data (forHash: true) remains stable for UserLicense version 1.
    /// This protects against accidental changes to the data format that would break backward compatibility.
    /// If this test fails, it means the hash data format has changed and existing licenses may no longer validate.
    /// </summary>
    [Fact]
    public void UserLicense_GetDataBytes_HashData_Version1()
    {
        var license = CreateDeterministicUserLicense();
        var actualHashData = System.Text.Encoding.UTF8.GetString(license.GetDataBytes(forHash: true));
        Assert.Equal(_knownGoodUserLicenseHashData, actualHashData);
    }

    /// <summary>
    /// Regression test that verifies GetDataBytes output for signature data (forHash: false) remains stable for UserLicense version 1.
    /// This protects against accidental changes to the data format that would break backward compatibility.
    /// If this test fails, it means the signature data format has changed and existing licenses may no longer validate.
    /// </summary>
    [Fact]
    public void UserLicense_GetDataBytes_SignatureData_Version1()
    {
        var license = CreateDeterministicUserLicense();
        var actualSignatureData = System.Text.Encoding.UTF8.GetString(license.GetDataBytes(forHash: false));
        Assert.Equal(_knownGoodUserLicenseSignatureData, actualSignatureData);
    }

    /// <summary>
    /// Validates that the UserLicense version remains frozen at version 1.
    /// License versions should no longer be incremented. Use the JWT Token property to add new claims instead.
    /// If this test fails, it means someone attempted to add version 2 support, which is no longer allowed.
    /// </summary>
    [Fact]
    public void UserLicense_CurrentVersion_ShouldRemainFrozen()
    {
        const int expectedMaxVersion = 1;

        var user = CreateDeterministicUser();
        var subscriptionInfo = CreateDeterministicSubscriptionInfo();
        var mockLicensingService = CreateMockLicensingService();

        // Verify that version 2 is NOT supported (should throw NotSupportedException)
        var exception = Assert.Throws<NotSupportedException>(() =>
            new UserLicense(user, subscriptionInfo, mockLicensingService, version: 2));

        // If the exception message changes or we don't get an exception, fail with helpful guidance
        if (exception == null)
        {
            var errorMessage = $@"
ERROR: UserLicense now supports version 2 or higher

License versions are now frozen and should not be incremented.

Instead of incrementing the version:
- Use the JWT Token property to add new claims
- Add your new capabilities as claims in the Token
- This allows for more flexible licensing without breaking backward compatibility

If you believe you need to change the version for a valid reason, please discuss with the team first.
";
            Assert.Fail(errorMessage);
        }

        // Verify we still support version 1
        var license = new UserLicense(user, subscriptionInfo, mockLicensingService, version: expectedMaxVersion);
        Assert.NotNull(license);
    }

    /// <summary>
    /// Creates a deterministic UserLicense for testing hash values.
    /// All property values are fixed to ensure reproducible hashes.
    /// </summary>
    private static UserLicense CreateDeterministicUserLicense()
    {
        var user = CreateDeterministicUser();
        var subscriptionInfo = CreateDeterministicSubscriptionInfo();
        var mockLicensingService = CreateMockLicensingService();

        var license = new UserLicense(user, subscriptionInfo, mockLicensingService, version: 1);

        // Override timestamps to deterministic values (constructor sets them to DateTime.UtcNow)
        license.Issued = new DateTime(2025, 9, 26, 12, 0, 41, DateTimeKind.Utc); // Corresponds to 1759502041 Unix timestamp
        license.Refresh = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc); // Corresponds to 1735603200 Unix timestamp

        // Recalculate hash with the deterministic Issued/Refresh values
        license.Hash = Convert.ToBase64String(license.ComputeHash());
        license.Signature = Convert.ToBase64String(mockLicensingService.SignLicense(license));

        return license;
    }

    /// <summary>
    /// Creates a User with deterministic property values for reproducible testing.
    /// </summary>
    private static User CreateDeterministicUser()
    {
        return new User
        {
            Id = new Guid("12300000-0000-0000-0000-000000000789"),
            Name = "Test User",
            Email = "test@example.com",
            LicenseKey = "myUserLicenseKey",
            Premium = true,
            MaxStorageGb = 10,
            PremiumExpirationDate = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Creates a SubscriptionInfo with deterministic dates for reproducible testing.
    /// </summary>
    private static SubscriptionInfo CreateDeterministicSubscriptionInfo()
    {
        var stripeSubscription = new Subscription
        {
            Status = "active",
            TrialStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TrialEnd = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Items = new StripeList<SubscriptionItem>
            {
                Data = [
                    new SubscriptionItem
                    {
                        CurrentPeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        CurrentPeriodEnd = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc)
                    }
                ]
            }
        };

        return new SubscriptionInfo
        {
            UpcomingInvoice = new SubscriptionInfo.BillingUpcomingInvoice
            {
                Date = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc)
            },
            Subscription = new SubscriptionInfo.BillingSubscription(stripeSubscription)
        };
    }

    /// <summary>
    /// Creates a mock ILicensingService that returns a deterministic signature.
    /// </summary>
    private static ILicensingService CreateMockLicensingService()
    {
        var mockService = Substitute.For<ILicensingService>();
        mockService.SignLicense(Arg.Any<ILicense>())
            .Returns([0x00, 0x01, 0x02, 0x03]); // Dummy signature for hash testing
        return mockService;
    }
}
