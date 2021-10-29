using AutoFixture;
using TableModel = Bit.Core.Models.Table;
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
using AutoFixture.Xunit2;
using System.Reflection;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

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
            if (type == typeof(OrganizationUser))
            {
                var fixture = new Fixture();
                var orgUser = fixture.WithAutoNSubstitutions().Create<TableModel.OrganizationUser>();
                var orgUserPermissions = fixture.WithAutoNSubstitutions().Create<Permissions>();
                orgUser.Permissions = JsonSerializer.Serialize(orgUserPermissions, new JsonSerializerOptions() {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
                return orgUser;
            }
            else if (type == typeof(List<OrganizationUser>))
            {
                var fixture = new Fixture();
                var orgUsers = fixture.WithAutoNSubstitutions().CreateMany<TableModel.OrganizationUser>(2);
                foreach (var orgUser in orgUsers)
                {
                    var providers = fixture.Create<Dictionary<TwoFactorProviderType, TwoFactorProvider>>();
                    var orgUserPermissions = fixture.WithAutoNSubstitutions().Create<Permissions>();
                    orgUser.Permissions = JsonSerializer.Serialize(orgUserPermissions, new JsonSerializerOptions() {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    });
                }
                return orgUsers;
            }
            return new NoSpecimen();
        }
    }
    
    internal class OrganizationUser : ICustomization
    {
        public OrganizationUserStatusType Status { get; set; }
        public OrganizationUserType Type { get; set; }

        public OrganizationUser(OrganizationUserStatusType status, OrganizationUserType type)
        {
            Status = status;
            Type = type;
        }
        
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Core.Models.Table.OrganizationUser>(composer => composer
                .With(o => o.Type, Type)
                .With(o => o.Status, Status));
        }
    }

    public class OrganizationUserAttribute : CustomizeAttribute
    {
        private readonly OrganizationUserStatusType _status;
        private readonly OrganizationUserType _type;

        public OrganizationUserAttribute(
            OrganizationUserStatusType status = OrganizationUserStatusType.Confirmed,
            OrganizationUserType type = OrganizationUserType.User)
        {
            _status = status;
            _type = type;
        }

        public override ICustomization GetCustomization(ParameterInfo parameter)
        {
            return new OrganizationUser(_status, _type);
        }
    }

   internal class EfOrganizationUser: ICustomization 
   {
      public void Customize(IFixture fixture)
      {
         fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
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
