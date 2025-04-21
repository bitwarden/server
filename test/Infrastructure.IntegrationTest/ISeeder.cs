namespace Bit.Infrastructure.IntegrationTest;

/// <summary>
/// An interface that can be implemented to run custom seeding logic. The implementation needs to be registered through
/// the <see cref="SeedConfigurationAttribute{T}"/> with a name that can be referenced on <see cref="DatabaseDataAttribute"/>.
/// </summary>
/// <remarks>
/// The implementation can optionally implement <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/> for running
/// cleanup code.
/// </remarks>
public interface ISeeder
{
    Task SeedAsync(SeedContext context);
}
