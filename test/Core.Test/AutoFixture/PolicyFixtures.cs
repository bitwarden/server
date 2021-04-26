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

namespace Bit.Core.Test.AutoFixture.PolicyFixtures
{
    internal class PolicyBuilder: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.Policy))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.Policy>();
            return obj;
        }
    }

    internal class EfPolicy: ICustomization 
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new PolicyBuilder());
            fixture.Customizations.Add(new OrganizationBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<PolicyRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        }
    }

    internal class EfPolicyAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfPolicyAutoDataAttribute() : base(new SutProviderCustomization(), new EfPolicy())
        { }
    }

    internal class InlineEfPolicyAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfPolicyAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfPolicy) }, values)
        { }
    }
}

