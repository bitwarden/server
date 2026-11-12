using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Convenience extension members for resolving common services from <see cref="SeederContext.Services"/>.
/// Minimizes churn in step implementations when transitioning from direct property access to DI.
/// </summary>
internal static class SeederContextExtensions
{
    extension(SeederContext context)
    {
        internal IPasswordHasher<User> PasswordHasher =>
            context.Services.GetRequiredService<IPasswordHasher<User>>();

        internal IManglerService Mangler =>
            context.Services.GetRequiredService<IManglerService>();

        internal ILogger<T>? GetLogger<T>() =>
            context.Services.GetService<ILogger<T>>();

        internal ISeedReader SeedReader =>
            context.Services.GetRequiredService<ISeedReader>();

        internal IAttachmentStorageService AttachmentStorageService =>
            context.Services.GetRequiredService<IAttachmentStorageService>();

        internal SeederSettings Settings =>
            context.Services.GetRequiredService<SeederSettings>();

        internal string Password =>
            context.Settings.Password ?? Factories.UserSeeder.DefaultPassword;

        internal int KdfIterations =>
            context.Settings.KdfIterations;

        internal string? OrgNameOverride =>
            context.Settings.OrgNameOverride;

        internal string? OwnerEmailOverride =>
            context.Settings.OwnerEmailOverride;

        /// <summary>
        /// Resolves the optional progress reporter. Returns null when no UI is attached.
        /// Callers should null-check before constructing events to keep the no-reporter path allocation-free.
        /// </summary>
        internal IProgress<SeederProgressEvent>? Progress =>
            context.Services.GetService<IProgress<SeederProgressEvent>>();
    }
}

/// <summary>
/// Runtime settings for a seeding operation, registered in DI.
/// </summary>
/// <param name="Password">Override for all seeded account passwords. Null falls back to <see cref="Factories.UserSeeder.DefaultPassword"/>.</param>
/// <param name="KdfIterations">KDF iteration count for all seeded users.</param>
/// <param name="OrgNameOverride">When set, replaces the fixture/preset-supplied organization name before the mangler runs. Literal without <c>--mangle</c>; gets a unique prefix (e.g. <c>abc12345-MyOrg</c>) with it.</param>
/// <param name="OwnerEmailOverride">When set, replaces the default <c>owner@&lt;domain&gt;</c> owner email before the mangler runs. Literal without <c>--mangle</c>; with mangling the email becomes <c>&lt;mangleId&gt;+&lt;local&gt;@&lt;domain&gt;</c> — note this is a structurally distinct mailbox, not standard plus-addressing (which would strip the suffix back to the original).</param>
internal sealed record SeederSettings(
    string? Password = null,
    int KdfIterations = 5_000,
    string? OrgNameOverride = null,
    string? OwnerEmailOverride = null);
