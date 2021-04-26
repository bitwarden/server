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
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Core.Test.AutoFixture.TransactionFixtures;
using AutoFixture.DataAnnotations;
using Bit.Core.Test.AutoFixture.Relays;

namespace Bit.Core.Test.AutoFixture.EmergencyAccessFixtures
{
    internal class EmergencyAccessBuilder: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.EmergencyAccess))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            fixture.Customizations.Insert(0, new MaxLengthStringRelay());
            var obj = fixture.Create<TableModel.EmergencyAccess>();
            return obj;
        }
    }

    internal class EfEmergencyAccess: ICustomization 
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new EmergencyAccessBuilder());
            fixture.Customizations.Add(new UserBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<EmergencyAccessRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        }
    }

    internal class EfEmergencyAccessAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfEmergencyAccessAutoDataAttribute() : base(new SutProviderCustomization(), new EfEmergencyAccess())
        { }
    }

    internal class InlineEfEmergencyAccessAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfEmergencyAccessAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfEmergencyAccess) }, values)
        { }
    }
}

