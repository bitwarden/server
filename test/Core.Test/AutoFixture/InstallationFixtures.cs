using AutoFixture;
using TableModel = Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.GlobalSettingsFixtures;
using AutoMapper;
using Bit.Core.Models.EntityFramework;
using Bit.Core.Models;
using System.Collections.Generic;
using Bit.Core.Enums;
using AutoFixture.Kernel;
using System;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;

namespace Bit.Core.Test.AutoFixture.InstallationFixtures
{
    internal class InstallationBuilder: ISpecimenBuilder
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

    internal class EfInstallation: ICustomization 
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

