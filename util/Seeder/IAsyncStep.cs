using Bit.Seeder.Pipeline;

namespace Bit.Seeder;

/// <summary>
/// Asynchronous counterpart to <see cref="IStep"/> for pipeline steps that do real I/O
/// (e.g. calling external services like Stripe). Implementors return a Task that the
/// <see cref="RecipeExecutor"/> awaits.
/// </summary>
public interface IAsyncStep
{
    Task ExecuteAsync(SeederContext context);
}
