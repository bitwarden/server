using Bit.Seeder.Pipeline;

namespace Bit.Seeder;

/// <summary>
/// An asynchronous unit of pipeline work. Register with <c>RecipeBuilder.AddAsyncStep</c>.
/// </summary>
/// <remarks>
/// Parallel to <see cref="IStep"/> rather than derived from it: a step is either synchronous or
/// asynchronous, never both.
/// <para>
/// <strong>Steps are awaited strictly sequentially.</strong> Never dispatch steps with
/// <c>Task.WhenAll</c> or any other concurrent scheduling. <see cref="SeederContext"/> and the
/// pipeline's progress ticker are not thread-safe, and later steps read state earlier steps write.
/// </para>
/// </remarks>
public interface IAsyncStep
{
    Task ExecuteAsync(SeederContext context);
}
