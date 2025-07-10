using Microsoft.Extensions.DependencyInjection;

namespace Bit.IntegrationTestCommon;

#nullable enable

public interface ITestDatabase
{
    public void AddDatabase(IServiceCollection serviceCollection);

    public void Migrate(IServiceCollection serviceCollection);

    public void Dispose();

    public void ModifyGlobalSettings(Dictionary<string, string?> config)
    {
        // Default implementation does nothing
    }
}
