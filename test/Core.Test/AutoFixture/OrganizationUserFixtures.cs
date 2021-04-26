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
using Bit.Core.Models.Data;
using System.Text.Json;
using Bit.Core.Test.AutoFixture.UserFixtures;

namespace Bit.Core.Test.AutoFixture.OrganizationUserFixtures
{
    internal class OrganizationUserBuilder: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.OrganizationUser))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            var orgUser = fixture.WithAutoNSubstitutions().Create<TableModel.OrganizationUser>();
            var orgUserPermissions = fixture.WithAutoNSubstitutions().Create<Permissions>();
            orgUser.Permissions = JsonSerializer.Serialize(orgUserPermissions, new JsonSerializerOptions() {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            return orgUser;
        }
    }

   internal class EfOrganizationUser: ICustomization 
   {
      public void Customize(IFixture fixture)
      {
         fixture.Customizations.Add(new GlobalSettingsBuilder());
         fixture.Customizations.Add(new OrganizationUserBuilder());
         fixture.Customizations.Add(new OrganizationBuilder());
         fixture.Customizations.Add(new UserBuilder());
         fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationUserRepository>());
         fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
         fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
      }
   }

    internal class EfOrganizationUserAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfOrganizationUserAutoDataAttribute() : base(new SutProviderCustomization(), new EfOrganizationUser())
        { }
    }

    internal class InlineEfOrganizationUserAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfOrganizationUserAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfOrganizationUser) }, values)
        { }
    }
}
