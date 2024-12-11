using System.Linq.Expressions;
using AutoFixture.Dsl;

namespace Bit.Core.Test.AutoFixture;

public static class AutoFixtureExtensions
{
    /// <summary>
    /// Registers that a writable Guid property should be assigned a random value that is derived from the given seed.
    /// </summary>
    /// <remarks>
    /// This can be used to generate random Guids that are deterministic based on the seed and thus can be re-used for
    /// different entities that share the same identifiers. e.g. Collections, CollectionUsers, and CollectionGroups can
    /// all have the same Guids generate for their "collection id" properties.
    /// </remarks>
    /// <param name="composer"></param>
    /// <param name="propertyPicker">The Guid property to register</param>
    /// <param name="seed">The random seed to use for random Guid generation</param>
    public static IPostprocessComposer<T> WithGuidFromSeed<T>(
        this IPostprocessComposer<T> composer,
        Expression<Func<T, Guid>> propertyPicker,
        int seed
    )
    {
        var rnd = new Random(seed);
        return composer.With(
            propertyPicker,
            () =>
            {
                // While not as random/unique as Guid.NewGuid(), this is works well enough for testing purposes.
                var bytes = new byte[16];
                rnd.NextBytes(bytes);
                return new Guid(bytes);
            }
        );
    }

    /// <summary>
    /// Registers that a writable property should be assigned a value from the given list.
    /// </summary>
    /// <remarks>
    /// The value will be assigned in the order that the list is enumerated. Values will wrap around to the beginning
    /// should the end of the list be reached.
    /// </remarks>
    /// <param name="composer"></param>
    /// <param name="propertyPicker"></param>
    /// <param name="values"></param>
    public static IPostprocessComposer<T> WithValueFromList<T, TValue>(
        this IPostprocessComposer<T> composer,
        Expression<Func<T, TValue>> propertyPicker,
        ICollection<TValue> values
    )
    {
        var index = 0;
        return composer.With(
            propertyPicker,
            () =>
            {
                var value = values.ElementAt(index);
                index = (index + 1) % values.Count;
                return value;
            }
        );
    }
}
