#nullable enable
namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand.Validation;

public interface IPlanValidation : IValidation<OrgSignUpWithPlan> { }

public class PlanValidation : IPlanValidation
{
    public IValidationResult<OrgSignUpWithPlan> Validate(OrgSignUpWithPlan signUpWithPlan)
    {
        if (ValidatePasswordManager(signUpWithPlan) is InvalidResult<OrgSignUpWithPlan> invalidPasswordManagerResult)
        {
            return invalidPasswordManagerResult;
        }

        if (ValidateSecretsManager(signUpWithPlan) is InvalidResult<OrgSignUpWithPlan> invalidSecretsManagerResult)
        {
            return invalidSecretsManagerResult;
        }

        return ValidatePasswordManager(signUpWithPlan);
    }

    private static IValidationResult<OrgSignUpWithPlan> ValidatePlan(OrgSignUpWithPlan organizationSignUpWithPlan, string productName)
    {
        if (organizationSignUpWithPlan.Plan is null)
        {
            return new InvalidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan, $"{productName} Plan was null.");
        }

        if (organizationSignUpWithPlan.Plan.Disabled)
        {
            return new InvalidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan,
                $"{productName} Plan is not found.");
        }

        return new ValidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan);
    }

    public static IValidationResult<OrgSignUpWithPlan> ValidatePasswordManager(OrgSignUpWithPlan organizationSignUpWithPlan)
    {
        if (ValidatePlan(organizationSignUpWithPlan, "Password Manager") is InvalidResult<OrgSignUpWithPlan>
            invalidResult)
        {
            return invalidResult;
        }

        if (organizationSignUpWithPlan.Signup.AdditionalSeats < 0)
        {
            return new InvalidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan, "You can't subtract Password Manager seats!");
        }

        if (organizationSignUpWithPlan.Signup.AdditionalSeats < 0)
        {
            return new InvalidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan,
                "You can't subtract Password Manager seats!");
        }

        if (organizationSignUpWithPlan.Plan.PasswordManager.HasAdditionalStorageOption &&
            organizationSignUpWithPlan.Signup.AdditionalStorageGb > 0)
        {
            return new InvalidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan,
                "Plan does not allow additional storage.");
        }

        if (organizationSignUpWithPlan.Signup.AdditionalStorageGb < 0)
        {
            return new InvalidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan, "You can't subtract storage!");
        }

        if (organizationSignUpWithPlan.Plan.PasswordManager.HasPremiumAccessOption &&
            organizationSignUpWithPlan.Signup.PremiumAccessAddon)
        {
            return new InvalidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan,
                "This plan does not allow you to buy the premium access addon.");
        }

        if (organizationSignUpWithPlan.Plan.PasswordManager.HasAdditionalSeatsOption &&
            organizationSignUpWithPlan.Signup.AdditionalSeats > 0)
        {
            return new InvalidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan,
                "Plan does not allow additional users.");
        }

        if (!organizationSignUpWithPlan.Plan.PasswordManager.HasAdditionalSeatsOption &&
            organizationSignUpWithPlan.Plan.PasswordManager.MaxAdditionalSeats.HasValue &&
            organizationSignUpWithPlan.Plan.PasswordManager.MaxAdditionalSeats.Value <
            organizationSignUpWithPlan.Signup.AdditionalSeats)
        {
            return new InvalidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan, $"Selected plan allows a maximum of " +
                $"{organizationSignUpWithPlan.Plan.PasswordManager.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }

        return new ValidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan);
    }

    public static IValidationResult<OrgSignUpWithPlan> ValidateSecretsManager(OrgSignUpWithPlan organizationSignUpWithPlan)
    {
        return new ValidResult<OrgSignUpWithPlan>(organizationSignUpWithPlan);
    }
}
