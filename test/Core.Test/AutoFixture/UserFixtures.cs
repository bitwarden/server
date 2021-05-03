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
            if (type == typeof(TableModel.User))
            {
                var fixture = new Fixture();
                var providers = fixture.Create<Dictionary<TwoFactorProviderType, TwoFactorProvider>>();
                var user = fixture.Create<TableModel.User>();
                user.SetTwoFactorProviders(providers);

                return user;
            }
            else if (type == typeof(List<TableModel.User>))
            {
                var fixture = new Fixture();
                var users = fixture.WithAutoNSubstitutions().CreateMany<TableModel.User>(2);
                foreach (var user in users)
                {
                    var providers = fixture.Create<Dictionary<TwoFactorProviderType, TwoFactorProvider>>();
                    user.SetTwoFactorProviders(providers);
                }
                return users;
            }

            return new NoSpecimen();
        }
    }

   internal class EfUser: ICustomization 
   {
      public void Customize(IFixture fixture)
      {
         fixture.Customizations.Add(new GlobalSettingsBuilder());
         fixture.Customizations.Add(new UserBuilder());
         fixture.Customizations.Add(new OrganizationBuilder());
         fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
         fixture.Customizations.Add(new EfRepositoryListBuilder<SsoUserRepository>());
         fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
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
