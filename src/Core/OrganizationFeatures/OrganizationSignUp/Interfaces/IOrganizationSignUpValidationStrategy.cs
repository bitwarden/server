using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;

public interface IOrganizationSignUpValidationStrategy
{
    void Validate(Models.StaticStore.Plan plan, OrganizationUpgrade upgrade);
}
