using Bit.Core.Services;
using Bit.IntegrationTestCommon.Factories;

namespace Bit.SeederApi.IntegrationTest;

public class SeederApiApplicationFactory : WebApplicationFactoryBase<Program>
{
    public SeederApiApplicationFactory()
    {
        _configureTestServices.Add(serviceCollection =>
        {
            serviceCollection.AddSingleton<IPlayIdService, NeverPlayIdServices>();
        });
    }
}
