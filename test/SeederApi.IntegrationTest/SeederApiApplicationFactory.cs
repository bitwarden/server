using Bit.Core.Services;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;

namespace Bit.SeederApi.IntegrationTest;

public class SeederApiApplicationFactory : WebApplicationFactoryBase<Startup>
{
    public SeederApiApplicationFactory()
    {
        TestDatabase = new SqliteTestDatabase();
        _configureTestServices.Add(serviceCollection =>
        {
            serviceCollection.AddSingleton<IPlayIdService, NeverPlayIdServices>();
            serviceCollection.AddHttpContextAccessor();
        });
    }

    public void ConfigureAuth(string username, string password)
    {
        UpdateConfiguration(builder =>
        {
            builder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "seederSettings:Username", username},
                { "seederSettings:Password", password}
            });
        });
    }
}
