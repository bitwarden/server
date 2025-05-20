using Bit.Core.Arch;

namespace Bit.Api;

public class ApiService : BitService
{
    public override string RoutePrefix => "api";

    public override IServiceCollection AddServices(IServiceCollection services)
    {
        // TODO: Do things
        return services;
    }

    public override void MapEndpoints(RouteGroupBuilder builder)
    {
        //
    }
}
