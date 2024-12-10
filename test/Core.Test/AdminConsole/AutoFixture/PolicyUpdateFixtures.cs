using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

internal class PolicyUpdateCustomization(PolicyType type, bool enabled) : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<PolicyUpdate>(composer => composer
            .With(o => o.Type, type)
            .With(o => o.Enabled, enabled)
            .With(o => o.PerformedBy, new StandardUser(Guid.NewGuid(), false)));
    }
}

public class PolicyUpdateAttribute(PolicyType type, bool enabled = true) : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new PolicyUpdateCustomization(type, enabled);
    }
}
