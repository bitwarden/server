using System;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Test.AutoFixture.InstallationFixtures
{
    internal class InstallationBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.Installation))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.Installation>();
            return obj;
        }
    }

    internal class EfInstallation : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new InstallationBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<InstallationRepository>());
        }
    }

    internal class EfInstallationAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfInstallationAutoDataAttribute() : base(new SutProviderCustomization(), new EfInstallation())
        { }
    }

    internal class InlineEfInstallationAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfInstallationAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfInstallation) }, values)
        { }
    }
}

