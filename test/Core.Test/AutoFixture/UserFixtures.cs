using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Models;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.UserFixtures;

public class UserBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == typeof(User))
        {
            var fixture = new Fixture();
            var providers = fixture.Create<Dictionary<TwoFactorProviderType, TwoFactorProvider>>();
            var user = fixture.WithAutoNSubstitutions().Create<User>();
            user.SetTwoFactorProviders(providers);
            return user;
        }
        else if (type == typeof(List<User>))
        {
            var fixture = new Fixture();
            var users = fixture.WithAutoNSubstitutions().CreateMany<User>(2);
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

internal class UserCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new UserFixture();
}

public class UserFixture : ICustomization
{
    public virtual void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new UserBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
    }
}
