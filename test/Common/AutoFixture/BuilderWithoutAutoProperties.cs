using AutoFixture;
using AutoFixture.Kernel;

namespace Bit.Test.Common.AutoFixture;

public class BuilderWithoutAutoProperties : ISpecimenBuilder
{
    private readonly Type _type;

    public BuilderWithoutAutoProperties(Type type)
    {
        _type = type;
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != _type)
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        // This is the equivalent of _fixture.Build<_type>().OmitAutoProperties().Create(request, context), but no overload for
        // Build(Type type) exists.
        dynamic reflectedComposer = typeof(Fixture)
            .GetMethod("Build")
            .MakeGenericMethod(_type)
            .Invoke(fixture, null);
        return reflectedComposer.OmitAutoProperties().Create(request, context);
    }
}

public class BuilderWithoutAutoProperties<T> : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context) =>
        new BuilderWithoutAutoProperties(typeof(T)).Create(request, context);
}
