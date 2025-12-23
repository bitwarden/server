using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit3;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Identity.Test.AutoFixture;

internal class OrganizationUserWithDefaultPermissionsCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<OrganizationUser>(composer => composer
            // On OrganizationUser, Permissions can be JSON data (as string) or sometimes null.
            // Entity APIs should prevent it from being anything else.
            // An un-modified fixture for OrganizationUser will return a bare string Permissions{guid}
            // in the member, throwing a JsonException on deserialization of a bare string.
            .With(organizationUser => organizationUser.Permissions, CoreHelpers.ClassToJsonData(new Permissions())));
    }
}

public class OrganizationUserWithDefaultPermissionsAttribute : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameter) => new OrganizationUserWithDefaultPermissionsCustomization();
}
