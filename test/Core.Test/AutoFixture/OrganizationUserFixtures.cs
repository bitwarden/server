using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Test.AutoFixture.OrganizationUserFixtures
{
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
}
