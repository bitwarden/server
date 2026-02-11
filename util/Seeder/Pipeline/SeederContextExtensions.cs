using Bit.Core.Entities;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Convenience extension methods for resolving common services from <see cref="SeederContext.Services"/>.
/// Minimizes churn in step implementations when transitioning from direct property access to DI.
/// </summary>
internal static class SeederContextExtensions
{
    internal static IPasswordHasher<User> GetPasswordHasher(this SeederContext context) =>
        context.Services.GetRequiredService<IPasswordHasher<User>>();

    internal static IManglerService GetMangler(this SeederContext context) =>
        context.Services.GetRequiredService<IManglerService>();

    internal static ISeedReader GetSeedReader(this SeederContext context) =>
        context.Services.GetRequiredService<ISeedReader>();
}
