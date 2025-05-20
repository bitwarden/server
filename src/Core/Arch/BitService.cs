#nullable enable

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Arch;

public abstract class BitService
{
    public abstract string RoutePrefix { get; }
    public abstract IServiceCollection AddServices(IServiceCollection services);
    public abstract void MapEndpoints(RouteGroupBuilder builder);
}
