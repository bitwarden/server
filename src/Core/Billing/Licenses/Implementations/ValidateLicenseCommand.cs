#nullable enable
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Common;
using Bit.Core.Services;
using Bit.Core.Settings;

// ReSharper disable once CheckNamespace
namespace Bit.Core.Billing.Licenses;

public class ValidateLicenseCommand
{
    public required ILicense License { get; init; }

    public User? User { get; init; }
}

public class ValidateLicenseCommandHandler(IGlobalSettings globalSettings, ILicensingService licensingService)
    : IValidateLicenseCommandHandler
{
    public Result Handle(ValidateLicenseCommand command) => command.License switch
    {
        OrganizationLicense license => ValidateOrganizationLicense(license, out var errors)
            ? Result.Success()
            : Result.Failure(errors),
        UserLicense license when command.User != null =>
            ValidateUserLicense(license, command.User!, out var errors)
                ? Result.Success()
                : Result.Failure(errors),
        UserLicense => throw new NotSupportedException("Attempted to validate a user license without a user."),
        _ => throw new NotSupportedException("License type is not supported.")
    };

    private bool ValidateOrganizationLicense(OrganizationLicense license, out IEnumerable<string> errors)
    {
        var errorMessages = new List<string>();

        if (!license.Enabled)
        {
            errorMessages.Add("Your cloud-hosted organization is currently disabled.");
        }

        if (license.Issued > DateTime.UtcNow)
        {
            errorMessages.Add("The license hasn't been issued yet.");
        }

        if (license.Expires < DateTime.UtcNow)
        {
            errorMessages.Add("The license has expired.");
        }

        if (license.ValidLicenseVersion)
        {
            errorMessages.Add($"Version {license.Version} is not supported.");
        }

        if (license.InstallationId != globalSettings.Installation.Id)
        {
            errorMessages.Add("The installation ID does not match the current installation.");
        }

        if (!license.SelfHost)
        {
            errorMessages.Add("The license does not allow for on-premise hosting of organizations.");
        }

        if (license.LicenseType != LicenseType.Organization)
        {
            errorMessages.Add("Premium licenses cannot be applied to an organization. " +
                                     "Upload this license from your personal account settings page.");
        }

        if (!licensingService.VerifyLicenseSignature(license))
        {
            errorMessages.Add("The license signature verification failed.");
        }

        if (errorMessages.Count > 0)
        {
            errors = errorMessages;
            return false;
        }

        errors = Array.Empty<string>();

        return true;
    }

    private bool ValidateUserLicense(UserLicense license, User user, out IEnumerable<string> errors)
    {
        var errorMessages = new List<string>();

        if (license.Issued > DateTime.UtcNow)
        {
            errorMessages.Add("The license hasn't been issued yet.");
        }

        if (license.Expires < DateTime.UtcNow)
        {
            errorMessages.Add("The license has expired.");
        }

        if (license.ValidLicenseVersion)
        {
            throw new NotSupportedException($"Version {license.Version} is not supported.");
        }

        if (!user.EmailVerified)
        {
            errorMessages.Add("The user's email is not verified.");
        }

        if (!user.Email.Equals(license.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            errorMessages.Add("The user's email does not match the license email.");
        }

        if (license.LicenseType != LicenseType.User)
        {
            errorMessages.Add("Organization licenses cannot be applied to a user. " +
                                     "Upload this license from the Organization settings page.");
        }

        if (!licensingService.VerifyLicenseSignature(license))
        {
            errorMessages.Add("The license signature verification failed.");
        }

        if (errorMessages.Count > 0)
        {
            errors = errorMessages;
            return false;
        }

        errors = Array.Empty<string>();
        return true;
    }
}
