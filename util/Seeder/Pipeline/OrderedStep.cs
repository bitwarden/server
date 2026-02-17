namespace Bit.Seeder.Pipeline;

/// <summary>
/// Wraps an <see cref="IStep"/> with an order index for keyed DI registration
/// where GetKeyedServices does not guarantee order.
/// </summary>
internal sealed class OrderedStep(IStep inner, int order) : IStep
{
    internal int Order { get; } = order;

    public void Execute(SeederContext context) => inner.Execute(context);
}
