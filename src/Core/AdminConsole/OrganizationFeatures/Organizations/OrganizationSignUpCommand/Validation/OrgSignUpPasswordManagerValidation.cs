namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand.Validation;

public class OrgSignUpPasswordManagerValidation(PlanValidation planValidation) : IValidation<OrgSignUpWithPlan>
{
    public IValidationResult<OrgSignUpWithPlan> Validate(OrgSignUpWithPlan request)
    {
        return planValidation.Validate(request);
    }
}
