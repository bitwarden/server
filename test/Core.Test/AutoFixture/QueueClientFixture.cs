#nullable enable
using AutoFixture;
using AutoFixture.Kernel;
using Azure.Storage.Queues;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;

namespace Bit.Core.Test.AutoFixture;

public class QueueClientBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        var type = request as Type;
        if (type == typeof(QueueClient))
        {
            return Substitute.For<QueueClient>();
        }

        return new NoSpecimen();
    }
}

public class QueueClientCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new QueueClientFixture();
}

public class QueueClientFixture : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new QueueClientBuilder());
    }
}
