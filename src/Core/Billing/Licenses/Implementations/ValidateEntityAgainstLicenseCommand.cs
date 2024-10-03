#nullable enable
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Models.Common;
using Bit.Core.Settings;

// ReSharper disable once CheckNamespace
namespace Bit.Core.Billing.Licenses;

public class ValidateEntityAgainstLicenseCommand
{
    public required ILicense License { get; init; }
    public Organization? Organization { get; init; }
    public User? User { get; init; }
}

public class ValidateEntityAgainstLicenseCommandHandler(IGlobalSettings globalSettings)
    : IValidateEntityAgainstLicenseCommandHandler
{
    public Result Handle(ValidateEntityAgainstLicenseCommand command) => command.License switch
    {
        OrganizationLicense license when command.Organization != null =>
            ValidateOrganizationDataAgainstLicense(command.Organization!, license)
                ? Result.Success()
                : Result.Failure("Invalid data"),
        UserLicense license when command.User != null =>
            ValidateUserDataAgainstLicense(command.User!, license)
                ? Result.Success()
                : Result.Failure("Invalid data."),
        OrganizationLicense => throw new NotSupportedException("Attempted to validate an organization against a " +
            "license without providing an organization."),
        UserLicense => throw new NotSupportedException("Attempted to validate a user against a license without " +
            "providing a user."),
        _ => throw new NotSupportedException("License type is not supported.")
    };

    private bool ValidateOrganizationDataAgainstLicense(Organization organization, OrganizationLicense license)
    {
        if (license.Issued > DateTime.UtcNow || license.Expires < DateTime.UtcNow)
        {
            return false;
        }

        if (!license.ValidLicenseVersion)
        {
            throw new NotSupportedException($"Version {license.Version} is not supported.");
        }

        var valid =
            globalSettings.Installation.Id == license.InstallationId &&
            organization.LicenseKey != null && organization.LicenseKey.Equals(license.LicenseKey) &&
            organization.Enabled == license.Enabled &&
            organization.PlanType == license.PlanType &&
            organization.Seats == license.Seats &&
            organization.MaxCollections == license.MaxCollections &&
            organization.UseGroups == license.UseGroups &&
            organization.UseDirectory == license.UseDirectory &&
            organization.UseTotp == license.UseTotp &&
            organization.SelfHost == license.SelfHost &&
            organization.Name.Equals(license.Name);

        if (valid && license.Version >= 2)
        {
            valid = organization.UsersGetPremium == license.UsersGetPremium;
        }

        if (valid && license.Version >= 3)
        {
            valid = organization.UseEvents == license.UseEvents;
        }

        if (valid && license.Version >= 4)
        {
            valid = organization.Use2fa == license.Use2fa;
        }

        if (valid && license.Version >= 5)
        {
            valid = organization.UseApi == license.UseApi;
        }

        if (valid && license.Version >= 6)
        {
            valid = organization.UsePolicies == license.UsePolicies;
        }

        if (valid && license.Version >= 7)
        {
            valid = organization.UseSso == license.UseSso;
        }

        if (valid && license.Version >= 8)
        {
            valid = organization.UseResetPassword == license.UseResetPassword;
        }

        if (valid && license.Version >= 9)
        {
            valid = organization.UseKeyConnector == license.UseKeyConnector;
        }

        if (valid && license.Version >= 10)
        {
            valid = organization.UseScim == license.UseScim;
        }

        if (valid && license.Version >= 11)
        {
            valid = organization.UseCustomPermissions == license.UseCustomPermissions;
        }

        /*
         * license.Version 12 added ExpirationWithoutDatePeriod, but that property is informational only and is not saved to the
         * Organization object. It's validated as part of the hash but does not need to be validated here.
         */
        if (valid && license.Version >= 13)
        {
            valid = organization.UseSecretsManager == license.UseSecretsManager &&
                    organization.UsePasswordManager == license.UsePasswordManager &&
                    organization.SmSeats == license.SmSeats &&
                    organization.SmServiceAccounts == license.SmServiceAccounts;
        }

        /*
         * Version 14 added LimitCollectionCreationDeletion and Version 15 added AllowAdminAccessToAllCollectionItems,
         * however these are just user settings, and it is not worth failing validation if they mismatch.
         * They are intentionally excluded.
         */

        return valid;
    }

    private static bool ValidateUserDataAgainstLicense(User user, UserLicense license)
    {
        if (license.Issued > DateTime.UtcNow || license.Expires < DateTime.UtcNow)
        {
            return false;
        }

        if (!license.ValidLicenseVersion)
        {
            throw new NotSupportedException($"Version {license.Version} is not supported.");
        }

        return user.LicenseKey != null && user.LicenseKey.Equals(license.LicenseKey) &&
               user.Premium == license.Premium &&
               user.Email.Equals(license.Email, StringComparison.InvariantCultureIgnoreCase);
    }
}
