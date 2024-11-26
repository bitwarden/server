namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand.Validation;

public class OrgSignUpPasswordManagerValidation(PlanValidation planValidation) : IValidation<OrgSignUpWithPlan>
{
    public IValidationResult<OrgSignUpWithPlan> Validate(OrgSignUpWithPlan signUpWithPlan)
    {
        return planValidation.Validate(signUpWithPlan);
    }
}
