using System.Linq;
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

namespace Bit.Core.Test.AutoFixture.UserFixtures
{
    internal class UserBuilder: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.User))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            var providers = fixture.Create<Dictionary<TwoFactorProviderType, TwoFactorProvider>>();
            var user = new Fixture().WithAutoNSubstitutions().Create<TableModel.User>();
            user.SetTwoFactorProviders(providers);
            return user;
        }
    }

   internal class EfUser: ICustomization 
   {
      public void Customize(IFixture fixture)
      {

         fixture.Customizations.Add(new UserBuilder());
         fixture.Customizations.Add(new GlobalSettingsBuilder());
         fixture.Customize<IMapper>(x => x.FromFactory(() => 
            new MapperConfiguration(cfg => cfg.AddProfile<UserMapperProfile>()).CreateMapper()));
      }
   }

    internal class EfUserAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfUserAutoDataAttribute() : base(new SutProviderCustomization(), new EfUser())
        { }
    }

    internal class InlineEfUserAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfUserAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfUser) }, values)
        { }
    }
}
