using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole;

public class IntegrationTestBase(ApiApplicationFactory factory) : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    protected ApiApplicationFactory Factory;
    protected HttpClient Client;

    public virtual Task InitializeAsync()
    {
        Factory = factory;
        Client = Factory.CreateClient();

        return Task.CompletedTask;
    }

    protected void InitializationWithFeaturesEnabled(params string[] featuresToEnable)
    {
        Factory = factory;

        Factory.SubstituteService<IFeatureService>(featureService =>
        {
            foreach (var feature in featuresToEnable)
            {
                featureService.IsEnabled(feature).Returns(true);
            }
        });

        Client = Factory.CreateClient();
    }

    public virtual Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }

}
