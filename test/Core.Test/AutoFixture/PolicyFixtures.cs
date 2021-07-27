using System;
using System.Reflection;
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
using System.Reflection;
using AutoFixture.Xunit2;

namespace Bit.Core.Test.AutoFixture.PolicyFixtures
{
    internal class Policy : ICustomization
    {
        public PolicyType Type { get; set; }

        public Policy(PolicyType type)
        {
            Type = type;
        }
        
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Core.Models.Table.Policy>(composer => composer
                .With(o => o.OrganizationId, Guid.NewGuid())
                .With(o => o.Type, Type)
                .With(o => o.Enabled, true));
        }
    }

    public class PolicyAttribute : CustomizeAttribute
    {
        private readonly PolicyType _type;

        public PolicyAttribute(PolicyType type)
        {
            _type = type;
        }

        public override ICustomization GetCustomization(ParameterInfo parameter)
        {
            return new Policy(_type);
        }
    }
    
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
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
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
