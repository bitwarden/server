using Bit.Core.Entities;

namespace Bit.Core.Models.Data.Organizations;

public class OrganizationAbility
{
    public OrganizationAbility() { }

    public OrganizationAbility(Organization organization)
    {
        Id = organization.Id;
        UseEvents = organization.UseEvents;
        Use2fa = organization.Use2fa;
        Using2fa = organization.Use2fa && organization.TwoFactorProviders != null &&
            organization.TwoFactorProviders != "{}";
        UsersGetPremium = organization.UsersGetPremium;
        Enabled = organization.Enabled;
        UseSso = organization.UseSso;
        UseKeyConnector = organization.UseKeyConnector;
        UseScim = organization.UseScim;
        UseResetPassword = organization.UseResetPassword;
    }

    public Guid Id { get; set; }
    public bool UseEvents { get; set; }
    public bool Use2fa { get; set; }
    public bool Using2fa { get; set; }
    public bool UsersGetPremium { get; set; }
    public bool Enabled { get; set; }
    public bool UseSso { get; set; }
    public bool UseKeyConnector { get; set; }
    public bool UseScim { get; set; }
    public bool UseResetPassword { get; set; }
}
