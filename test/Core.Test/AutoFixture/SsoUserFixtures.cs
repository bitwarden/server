using AutoFixture;
using AutoMapper;
using Bit.Core.Models.EntityFramework;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.GlobalSettingsFixtures;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.UserFixtures;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Test.AutoFixture.SsoUserFixtures
{
   internal class EfSsoUser: ICustomization 
   {
      public void Customize(IFixture fixture)
      {
         fixture.Customizations.Add(new GlobalSettingsBuilder());
         fixture.Customize<IMapper>(x => x.FromFactory(() => 
            new MapperConfiguration(cfg => {
                cfg.AddProfile<SsoUserMapperProfile>();
                cfg.AddProfile<UserMapperProfile>();
                cfg.AddProfile<OrganizationMapperProfile>();
            }).CreateMapper()));
         fixture.Customizations.Add(new UserBuilder());
         fixture.Customizations.Add(new OrganizationBuilder());
         fixture.Customize<TableModel.SsoUser>(composer => composer.Without(ou => ou.Id));
      }
   }

    internal class EfSsoUserAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfSsoUserAutoDataAttribute() : base(new SutProviderCustomization(), new EfSsoUser())
        { }
    }

    internal class InlineEfSsoUserAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfSsoUserAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfSsoUser) }, values)
        { }
    }
}
