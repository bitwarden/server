#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Mappings;
using Bit.Core.Billing.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand.Validation;

public interface IPlanValidation : IValidation<OrgSignUpWithPlan> { }

public class PlanValidation : IPlanValidation
{
    public IValidationResult<OrgSignUpWithPlan> Validate(OrgSignUpWithPlan request)
    {
        if (ValidatePasswordManager(request) is InvalidResult<OrgSignUpWithPlan> invalidPasswordManagerResult)
        {
            return invalidPasswordManagerResult;
        }

        if (ValidateSecretsManager(request) is InvalidResult<OrgSignUpWithPlan> invalidSecretsManagerResult)
        {
            return invalidSecretsManagerResult;
        }

        return ValidatePasswordManager(request);
    }

    private static IValidationResult<OrgSignUpWithPlan> ValidatePlan(OrgSignUpWithPlan organizationSignUpWithPlan, string productName)
    {
        if (organizationSignUpWithPlan.Plan is null)
        {
            return organizationSignUpWithPlan.ToInvalidResult($"{productName} Plan was null.");
        }

        if (organizationSignUpWithPlan.Plan.Disabled)
        {
            return organizationSignUpWithPlan.ToInvalidResult($"{productName} Plan is not found.");
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
            return organizationSignUpWithPlan.ToInvalidResult("You can't subtract Password Manager seats!");
        }

        if (organizationSignUpWithPlan.Plan.PasswordManager.HasAdditionalStorageOption &&
            organizationSignUpWithPlan.Signup.AdditionalStorageGb > 0)
        {
            return organizationSignUpWithPlan.ToInvalidResult("Plan does not allow additional storage.");
        }

        if (organizationSignUpWithPlan.Signup.AdditionalStorageGb < 0)
        {
            return organizationSignUpWithPlan.ToInvalidResult("You can't subtract storage!");
        }

        if (organizationSignUpWithPlan.Plan.PasswordManager.HasPremiumAccessOption &&
            organizationSignUpWithPlan.Signup.PremiumAccessAddon)
        {
            return organizationSignUpWithPlan.ToInvalidResult("This plan does not allow you to buy the premium access addon.");
        }

        if (organizationSignUpWithPlan.Plan.PasswordManager.HasAdditionalSeatsOption &&
            organizationSignUpWithPlan.Signup.AdditionalSeats > 0)
        {
            return organizationSignUpWithPlan.ToInvalidResult("Plan does not allow additional users.");
        }

        if (!organizationSignUpWithPlan.Plan.PasswordManager.HasAdditionalSeatsOption &&
            organizationSignUpWithPlan.Plan.PasswordManager.MaxAdditionalSeats.HasValue &&
            organizationSignUpWithPlan.Plan.PasswordManager.MaxAdditionalSeats.Value <
            organizationSignUpWithPlan.Signup.AdditionalSeats)
        {
            return organizationSignUpWithPlan.ToInvalidResult($"Selected plan allows a maximum of " +
                                                              $"{organizationSignUpWithPlan.Plan.PasswordManager.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }

        return organizationSignUpWithPlan.ToValidResult();
    }

    public static IValidationResult<OrgSignUpWithPlan> ValidateSecretsManager(OrgSignUpWithPlan request)
    {
        if (request.Signup.UseSecretsManager)
        {
            if (request.Signup.IsFromProvider)
            {
                return request.ToInvalidResult("Organizations with a Managed Service Provider do not support Secrets Manager.");
            }

            if (request.Plan.SupportsSecretsManager is false)
            {
                return request.ToInvalidResult("Invalid Secrets Manager plan selected.");
            }

            if (ValidatePlan(request, "Secrets Manager") is InvalidResult<OrgSignUpWithPlan> invalidResult)
            {
                return invalidResult;
            }

            if (request.Signup.AdditionalSmSeats < 0)
            {
                return request.ToInvalidResult("You can't subtract Secrets Manager seats!");
            }

            if (request.Signup.AdditionalSmSeats + request.Plan.SecretsManager.BaseSeats <= 0)
            {
                return request.ToInvalidResult("You do not have any Secrets Manager seats!");
            }

            if (request.Signup.AdditionalServiceAccounts > 0 &&
                request.Plan.SecretsManager.HasAdditionalServiceAccountOption is false)
            {
                return request.ToInvalidResult("Plan does not allow additional Machine Accounts");
            }

            if ((request.Plan.ProductTier is ProductTierType.TeamsStarter && request.Signup.AdditionalSmSeats is > 0)
                || (request.Plan.ProductTier is not ProductTierType.TeamsStarter && request.Signup.AdditionalSmSeats is > 0))
            {
                return request.ToInvalidResult("You cannot have more Secrets Manager seats than Password Manager seats.");
            }

            if (request.Signup.AdditionalServiceAccounts is > 0)
            {
                return request.ToInvalidResult("You can't subtract Machine Accounts!");
            }

            switch (request.Plan.SecretsManager.HasAdditionalSeatsOption)
            {
                case false when request.Signup.AdditionalSmSeats is > 0:
                    return request.ToInvalidResult("Plan does not allow additional users.");
                case true when request.Plan.SecretsManager.MaxAdditionalSeats.HasValue &&
                    request.Signup.AdditionalSmSeats > request.Plan.SecretsManager.MaxAdditionalSeats.Value:
                    return request.ToInvalidResult("Selected plan allows a maximum of " +
                                                   $"{request.Plan.SecretsManager.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
            }
        }

        return request.ToValidResult();
    }
}
