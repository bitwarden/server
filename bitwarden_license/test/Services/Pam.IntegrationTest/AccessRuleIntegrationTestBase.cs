using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.IntegrationTest;

/// <summary>
/// Base for the PAM integration tests: the Api host over SQLite, the PAM flag on, and an enterprise organization.
/// Setup runs per test, so a test that turns a flag off does not leak into its siblings.
/// </summary>
public abstract class AccessRuleIntegrationTestBase : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _emailPrefix;

    protected AccessRuleIntegrationTestBase(ApiApplicationFactory factory, string emailPrefix)
    {
        Factory = factory;
        // Every PAM group sits behind a feature flag, so without a substitute feature service nothing is routable.
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

    protected async Task LoginAsProviderForOrganizationAsync()
    {
        var providerEmail = $"provider-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(providerEmail);

        await Factory.GetService<ICreateProviderCommand>()
            .CreateBusinessUnitAsync(
                new Provider { Name = "provider", Type = ProviderType.BusinessUnit },
                providerEmail,
                PlanType.EnterpriseAnnually2023,
                10);

        var providerUserAccount = await Factory.GetService<IUserRepository>().GetByEmailAsync(providerEmail);
        var providerUser = (await Factory.GetService<IProviderUserRepository>()
            .GetManyByUserAsync(providerUserAccount!.Id)).First();

        await Factory.GetService<IProviderOrganizationRepository>().CreateAsync(new ProviderOrganization
        {
            ProviderId = providerUser.ProviderId,
            OrganizationId = Organization.Id,
            Key = null,
            Settings = null
        });

        await LoginHelper.LoginAsync(providerEmail);
    }
}
