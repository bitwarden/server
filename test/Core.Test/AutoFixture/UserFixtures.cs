using System;
using System.Collections.Generic;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Test.AutoFixture.UserFixtures
{
    internal class UserBuilder : ISpecimenBuilder
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
                var user = fixture.WithAutoNSubstitutions().Create<TableModel.User>();
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

    internal class UserFixture : ICustomization
    {
        public virtual void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new UserBuilder());
            fixture.Customizations.Add(new OrganizationBuilder());
        }
    }

    internal class EfUser : UserFixture
    {
        public override void Customize(IFixture fixture)
        {
            base.Customize(fixture);
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
