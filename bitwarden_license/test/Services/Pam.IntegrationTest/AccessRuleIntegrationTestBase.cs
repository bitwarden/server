using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.IntegrationTest;

/// <summary>
/// Shared harness for the <c>organizations/{orgId}/access-rules</c> integration tests: the Api host over SQLite, the
/// PAM feature flag on, and an enterprise organization whose owner can be logged in.
/// </summary>
/// <remarks>
/// PAM ships under <c>bitwarden_license</c>, so its integration tests live here. Api.IntegrationTest is referenced
/// only for the host fixture and the organization/login helpers, the same way Billing.IntegrationTest consumes them.
/// <para>
/// Each test class gets its own <see cref="ApiApplicationFactory"/> — and so its own database — because xUnit scopes
/// <see cref="IClassFixture{T}"/> per class. Setup runs per test, since xUnit constructs a fresh test-class instance
/// for every test, which is what keeps a test that flips the feature flag from leaking into its siblings.
/// </para>
/// </remarks>
public abstract class AccessRuleIntegrationTestBase : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _emailPrefix;

    protected AccessRuleIntegrationTestBase(ApiApplicationFactory factory, string emailPrefix)
    {
        Factory = factory;
        // Every PAM group sits behind RequireFeature(FeatureFlagKeys.Pam), so without a substituted feature service
        // the whole surface is unroutable and every assertion below would pass for the wrong reason.
        Factory.SubstituteService<IFeatureService>(_ => { });
        Client = factory.CreateClient();
        LoginHelper = new LoginHelper(factory, Client);
        FeatureService = factory.GetService<IFeatureService>();
        _emailPrefix = emailPrefix;
    }

    protected ApiApplicationFactory Factory { get; }

    protected HttpClient Client { get; }

    protected LoginHelper LoginHelper { get; }

    protected IFeatureService FeatureService { get; }

    protected Organization Organization { get; private set; } = null!;

    protected string OwnerEmail { get; private set; } = null!;

    protected string AccessRulesUrl => AccessRulesUrlFor(Organization.Id);

    public virtual async Task InitializeAsync()
    {
        FeatureService.IsEnabled(FeatureFlagKeys.Pam).Returns(true);

        OwnerEmail = $"{_emailPrefix}-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(OwnerEmail);
        (Organization, _) = await OrganizationTestHelpers.SignUpAsync(Factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: OwnerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected static string AccessRulesUrlFor(Guid organizationId) => $"organizations/{organizationId}/access-rules";

    protected string AccessRuleUrl(Guid id) => $"{AccessRulesUrl}/{id}";
}
