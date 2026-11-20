using System.Security.Cryptography.X509Certificates;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Bit.SharedWeb.Certificates;

/// <summary>
/// The certificates that the settings of one SSO role configure in each slot.
/// <see cref="HasActiveCertificateSettingsSource"/> is true when the role's root fields or promoted future slot name a
/// certificate. <see cref="Active"/> is null when <see cref="HasActiveCertificateSettingsSource"/> is false.
/// </summary>
public sealed record SsoCertificateSlots(
    X509Certificate2? Active,
    IReadOnlyList<X509Certificate2> Staged,
    IReadOnlyList<X509Certificate2> Retired,
    bool HasActiveCertificateSettingsSource)
{
    /// <summary>
    /// Loads the staged certificate, then the retired certificates, then the active certificate of one SSO role.
    /// With no active source, does not load an active certificate.
    /// </summary>
    /// <param name="root">The root settings of the role.</param>
    /// <param name="future">The future slot of the role.</param>
    /// <param name="retired">The retired list of the role.</param>
    /// <param name="rootPath">The setting path of <paramref name="root"/>.</param>
    /// <param name="retiredPath">The setting path of <paramref name="retired"/>.</param>
    /// <param name="role">The certificate role. The error message names it.</param>
    /// <exception cref="InvalidOperationException">A cloud host that is not a Development host has no active source,
    /// or a configured certificate did not load, or a configured certificate does not have an RSA key.</exception>
    public static SsoCertificateSlots Load(IWebHostEnvironment env, GlobalSettings globalSettings,
        ICertificateSettings root, GlobalSettings.FutureCertificateSettings future,
        IEnumerable<GlobalSettings.RetiredCertificateSettings> retired, string rootPath, string retiredPath,
        CertificateRole role)
    {
        var hasActiveSource = CoreHelpers.HasActiveCertificateSettingsSource(root, future);
        if (!hasActiveSource && !globalSettings.SelfHosted && !env.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"No {role.GetDisplayName()} certificate is configured. A cloud host must set {rootPath}.");
        }

        var staged = CoreHelpers.GetStagedCertificate(globalSettings, future, $"{rootPath}:futureCertificate");
        IReadOnlyList<X509Certificate2> stagedList = staged == null ? [] : [staged];
        var retiredList = CoreHelpers.GetRetiredCertificates(globalSettings, retired, retiredPath);

        if (!hasActiveSource)
        {
            return new SsoCertificateSlots(null, stagedList, retiredList,
                HasActiveCertificateSettingsSource: false);
        }

        var active = CoreHelpers.GetActiveCertificate(globalSettings, root, future, rootPath, required: true);
        return new SsoCertificateSlots(active, stagedList, retiredList, HasActiveCertificateSettingsSource: true);
    }
}
