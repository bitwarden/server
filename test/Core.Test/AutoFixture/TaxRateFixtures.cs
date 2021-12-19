using System;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.Relays;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Test.AutoFixture.TaxRateFixtures
{
    internal class TaxRateBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.TaxRate))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            fixture.Customizations.Insert(0, new MaxLengthStringRelay());
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.TaxRate>();
            return obj;
        }
    }

    internal class EfTaxRate : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new TaxRateBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<TaxRateRepository>());
        }
    }

    internal class EfTaxRateAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfTaxRateAutoDataAttribute() : base(new SutProviderCustomization(), new EfTaxRate())
        { }
    }

    internal class InlineEfTaxRateAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfTaxRateAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfTaxRate) }, values)
        { }
    }
}

