using System;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.Relays;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Test.AutoFixture.EventFixtures
{
    internal class EventBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.Event))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            fixture.Customizations.Insert(0, new MaxLengthStringRelay());
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.Event>();
            return obj;
        }
    }

    internal class EfEvent : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new EventBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<EventRepository>());
        }
    }

    internal class EfEventAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfEventAutoDataAttribute() : base(new SutProviderCustomization(), new EfEvent())
        { }
    }

    internal class InlineEfEventAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfEventAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfEvent) }, values)
        { }
    }
}

