using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public interface IOrganizationConnectionValidatorFactory
    {
        IOrganizationConnectionValidator GetValidator(OrganizationConnectionType organizationConnectionType);
    }
}
