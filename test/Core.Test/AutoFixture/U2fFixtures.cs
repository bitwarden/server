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
using Bit.Core.Test.AutoFixture.Relays;
using Bit.Core.Test.AutoFixture.UserFixtures;

namespace Bit.Core.Test.AutoFixture.U2fFixtures
{
    internal class U2fBuilder: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.U2f))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            fixture.Customizations.Add(new MaxLengthStringRelay());
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.U2f>();
            return obj;
        }
    }

    internal class EfU2f: ICustomization 
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new U2fBuilder());
            fixture.Customizations.Add(new UserBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<U2fRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        }
    }

    internal class EfU2fAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfU2fAutoDataAttribute() : base(new SutProviderCustomization(), new EfU2f())
        { }
    }

    internal class InlineEfU2fAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfU2fAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfU2f) }, values)
        { }
    }
}

