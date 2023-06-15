using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;

public interface IOrganizationSignUpCommand
{
    Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup signup,
        bool provider = false);
}
