using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture.Relays;
using Bit.Test.Common.AutoFixture;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class CollectionBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(Collection))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        fixture.Customizations.Insert(0, new MaxLengthStringRelay());
        var obj = fixture.WithAutoNSubstitutions().Create<Collection>();
        return obj;
    }
}
