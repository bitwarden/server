using Bit.Core.Services;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;

namespace Bit.SeederApi.IntegrationTest;

public class SeederApiApplicationFactory : WebApplicationFactoryBase<Program>
{
    public SeederApiApplicationFactory()
    {
        TestDatabase = new SqliteTestDatabase();
        _configureTestServices.Add(serviceCollection =>
        {
            serviceCollection.AddSingleton<IPlayIdService, NeverPlayIdServices>();
        });
    }
}
