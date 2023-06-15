using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSignUp;

public class OrganizationSignUpCommand : IOrganizationSignUpCommand
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationService _organizationService;
    private readonly IPolicyService _policyService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationSignUpValidationStrategy _organizationSignUpValidationStrategy;

    public OrganizationSignUpCommand(
        IPaymentService paymentService
        , ICurrentContext currentContext
        , IOrganizationService organizationService
        , IPolicyService policyService
        , IReferenceEventService referenceEventService
        , IOrganizationUserRepository organizationUserRepository
        , IOrganizationSignUpValidationStrategy organizationSignUpValidationStrategy)
    {
        _paymentService = paymentService;
        _currentContext = currentContext;
        _organizationService = organizationService;
        _policyService = policyService;
        _referenceEventService = referenceEventService;
        _organizationUserRepository = organizationUserRepository;
        _organizationSignUpValidationStrategy = organizationSignUpValidationStrategy;
    }

    public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup signup,
        bool provider = false)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == signup.Plan).ToList();

        // if (!_featureService.IsEnabled(FeatureFlagKeys.SecretManagerGaBilling, _currentContext) &&
        //     !signup.UseSecretsManager)
        // {
        //     plans = StaticStore.PasswordManagerPlans.Where(p => p.Type == signup.Plan).ToList();
        // }
        // else
        // {
        //     
        // }
        foreach (var plan in plans)
        {
            if (plan is not { LegacyYear: null })
            {
                throw new BadRequestException("Invalid plan selected.");
            }

            if (plan.Disabled)
            {
                throw new BadRequestException("Plan not found.");
            }

            ValidateOrganizationUpgradeParameters(plan, signup, _organizationSignUpValidationStrategy);
        }

        if (!provider)
        {
            var anySingleOrgPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(signup.Owner.Id, PolicyType.SingleOrg);
            if (anySingleOrgPolicies)
            {
                throw new BadRequestException("You may not create an organization. You belong to an organization " +
                                              "which has a policy that prohibits you from being a member of any other organization.");
            }
        }

        var passwordManagerPlan = plans.FirstOrDefault(x => x.BitwardenProduct == BitwardenProductType.PasswordManager);
        var secretsManagerPlan = plans.FirstOrDefault(x => x.BitwardenProduct == BitwardenProductType.SecretsManager);

        var organization = new Organization
        {
            // Pre-generate the org id so that we can save it with the Stripe subscription..
            Id = CoreHelpers.GenerateComb(),
            Name = signup.Name,
            BillingEmail = signup.BillingEmail,
            BusinessName = signup.BusinessName,
            PlanType = passwordManagerPlan.Type,
            Seats = (short)(passwordManagerPlan.BaseSeats + signup.AdditionalSeats),
            MaxCollections = passwordManagerPlan.MaxCollections,
            MaxStorageGb = !passwordManagerPlan.BaseStorageGb.HasValue ?
                (short?)null : (short)(passwordManagerPlan.BaseStorageGb.Value + signup.AdditionalStorageGb),
            UsePolicies = passwordManagerPlan.HasPolicies,
            UseSso = passwordManagerPlan.HasSso,
            UseGroups = passwordManagerPlan.HasGroups,
            UseEvents = passwordManagerPlan.HasEvents,
            UseDirectory = passwordManagerPlan.HasDirectory,
            UseTotp = passwordManagerPlan.HasTotp,
            Use2fa = passwordManagerPlan.Has2fa,
            UseApi = passwordManagerPlan.HasApi,
            UseResetPassword = passwordManagerPlan.HasResetPassword,
            SelfHost = passwordManagerPlan.HasSelfHost,
            UsersGetPremium = passwordManagerPlan.UsersGetPremium || signup.PremiumAccessAddon,
            UseCustomPermissions = passwordManagerPlan.HasCustomPermissions,
            UseScim = passwordManagerPlan.HasScim,
            Plan = passwordManagerPlan.Name,
            Gateway = null,
            ReferenceData = signup.Owner.ReferenceData,
            Enabled = true,
            LicenseKey = CoreHelpers.SecureRandomString(20),
            PublicKey = signup.PublicKey,
            PrivateKey = signup.PrivateKey,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created,
            SmSeats = (short)(secretsManagerPlan.BaseSeats + signup.AdditionalSmSeats),
            SmServiceAccounts =
            (short)(secretsManagerPlan.BaseServiceAccount + signup.AdditionalServiceAccount)
        };

        if (passwordManagerPlan.Type == PlanType.Free && !provider)
        {
            var adminCount =
                await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id);
            if (adminCount > 0)
            {
                throw new BadRequestException("You can only be an admin of one free organization.");
            }
        }
        else if (passwordManagerPlan.Type != PlanType.Free)
        {
            await _paymentService.PurchaseOrganizationWithProductsAsync(organization, signup.PaymentMethodType.Value,
                signup.PaymentToken, plans, signup.AdditionalStorageGb, signup.AdditionalSeats,
                signup.PremiumAccessAddon, signup.TaxInfo, provider);
        }

        var ownerId = provider ? default : signup.Owner.Id;
        var returnValue = await _organizationService.SignUpAsync(organization, ownerId, signup.OwnerKey, signup.CollectionName, true);
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.Signup, organization, _currentContext)
            {
                PlanName = passwordManagerPlan.Name,
                PlanType = passwordManagerPlan.Type,
                Seats = returnValue.Item1.Seats,
                Storage = returnValue.Item1.MaxStorageGb,
                SmSeats = returnValue.Item1.SmSeats,
                ServiceAccounts = returnValue.Item1.SmServiceAccounts
            });
        return returnValue;
    }

    private static void ValidateOrganizationUpgradeParameters(Plan plan, OrganizationUpgrade upgrade
        , IOrganizationSignUpValidationStrategy strategy)
    {
        strategy = plan.BitwardenProduct switch
        {
            BitwardenProductType.PasswordManager => new PasswordManagerSignUpValidationStrategy(),
            _ => new SecretsManagerSignUpValidationStrategy()
        };

        strategy.Validate(plan, upgrade);
    }

}
