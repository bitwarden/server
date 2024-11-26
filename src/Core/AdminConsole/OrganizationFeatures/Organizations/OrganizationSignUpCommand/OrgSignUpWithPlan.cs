using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand;

public record OrgSignUpWithPlan(OrganizationSignup Signup, Plan Plan);
