using Bit.Api.KeyManagement.Queries;

namespace Bit.Api.KeyManagement;

#nullable enable

public static class Registrations
{
    public static void AddKeyManagementQueries(this IServiceCollection services)
    {
        services.AddTransient<IUserAccountKeysQuery, UserAccountKeysQuery>();
    }
}
