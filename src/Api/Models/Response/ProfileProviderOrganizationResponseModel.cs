using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response;

public class ProfileProviderOrganizationResponseModel : ProfileOrganizationResponseModel
{
    public ProfileProviderOrganizationResponseModel(ProviderUserOrganizationDetails organization)
        : base("profileProviderOrganization")
    {
        Id = organization.OrganizationId.ToString();
        Name = organization.Name;
        UsePolicies = organization.UsePolicies;
        UseSso = organization.UseSso;
        UseKeyConnector = organization.UseKeyConnector;
        UseScim = organization.UseScim;
        UseGroups = organization.UseGroups;
        UseDirectory = organization.UseDirectory;
        UseEvents = organization.UseEvents;
        UseTotp = organization.UseTotp;
        Use2fa = organization.Use2fa;
        UseApi = organization.UseApi;
        UseResetPassword = organization.UseResetPassword;
        UsersGetPremium = organization.UsersGetPremium;
        UseCustomPermissions = organization.UseCustomPermissions;
        UseActivateAutofillPolicy = organization.PlanType == PlanType.EnterpriseAnnually ||
            organization.PlanType == PlanType.EnterpriseMonthly;
        SelfHost = organization.SelfHost;
        Seats = organization.Seats;
        MaxCollections = organization.MaxCollections;
        MaxStorageGb = organization.MaxStorageGb;
        Key = organization.Key;
        HasPublicAndPrivateKeys = organization.PublicKey != null && organization.PrivateKey != null;
        Status = OrganizationUserStatusType.Confirmed; // Provider users are always confirmed
        Type = OrganizationUserType.Owner; // Provider users behave like Owners
        Enabled = organization.Enabled;
        SsoBound = false;
        Identifier = organization.Identifier;
        Permissions = new Permissions();
        ResetPasswordEnrolled = false;
        UserId = organization.UserId?.ToString();
        ProviderId = organization.ProviderId?.ToString();
        ProviderName = organization.ProviderName;
        PlanProductType = StaticStore.GetPasswordManagerPlan(organization.PlanType).Product;
    }
}
