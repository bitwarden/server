using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand.Validation;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Mappings;

public static class OrganizationMappings
{
    public static OrgSignUpWithPlan WithPlan(this OrganizationSignup signup) =>
        new(signup, StaticStore.GetPlan(signup.Plan));

    public static InvalidResult<OrgSignUpWithPlan> ToInvalidResult(this OrgSignUpWithPlan signUpWithPlan, string message) =>
        new(signUpWithPlan, message);

    public static InvalidResult<OrgSignUpWithPlan> ToValidResult(this OrgSignUpWithPlan signUpWithPlan) =>
        new(signUpWithPlan);

    public static Organization ToEntity(this OrgSignUpWithPlan signup, DateTimeOffset utcNow) => new()
    {
        Id = CoreHelpers.GenerateComb(),
        Name = signup.Signup.Name,
        BillingEmail = signup.Signup.BillingEmail,
        BusinessName = signup.Signup.BusinessName,
        PlanType = signup.Plan.Type,
        Seats = (short)(signup.Plan.PasswordManager.BaseSeats + signup.Signup.AdditionalSeats),
        MaxCollections = signup.Plan.PasswordManager.MaxCollections,
        MaxStorageGb = signup.Plan.PasswordManager.BaseStorageGb.HasValue
            ? (short)(signup.Plan.PasswordManager.BaseStorageGb.Value + signup.Signup.AdditionalStorageGb)
            : (short?)null,
        UsePolicies = signup.Plan.HasPolicies,
        UseSso = signup.Plan.HasSso,
        UseGroups = signup.Plan.HasGroups,
        UseEvents = signup.Plan.HasEvents,
        UseDirectory = signup.Plan.HasDirectory,
        UseTotp = signup.Plan.HasTotp,
        Use2fa = signup.Plan.Has2fa,
        UseApi = signup.Plan.HasApi,
        UseResetPassword = signup.Plan.HasResetPassword,
        SelfHost = signup.Plan.HasSelfHost,
        UsersGetPremium = signup.Plan.UsersGetPremium || signup.Signup.PremiumAccessAddon,
        UseCustomPermissions = signup.Plan.HasCustomPermissions,
        UseScim = signup.Plan.HasScim,
        Plan = signup.Plan.Name,
        Gateway = null,
        ReferenceData = signup.Signup.Owner.ReferenceData,
        Enabled = true,
        LicenseKey = CoreHelpers.SecureRandomString(20),
        PublicKey = signup.Signup.PublicKey,
        PrivateKey = signup.Signup.PrivateKey,
        CreationDate = utcNow.UtcDateTime,
        RevisionDate = utcNow.UtcDateTime,
        Status = OrganizationStatusType.Created,
        UsePasswordManager = true,
        UseSecretsManager = signup.Signup.UseSecretsManager,
        SmSeats = signup.Signup.UseSecretsManager
            ? signup.Plan.SecretsManager.BaseSeats + signup.Signup.AdditionalSmSeats
            : null,
        SmServiceAccounts = signup.Signup.UseSecretsManager
            ? signup.Plan.SecretsManager.BaseServiceAccount + signup.Signup.AdditionalServiceAccounts.GetValueOrDefault()
            : null
    };
}
