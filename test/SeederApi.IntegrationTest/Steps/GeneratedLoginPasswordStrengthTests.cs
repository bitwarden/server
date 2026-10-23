using System.Text.Json;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.RustSDK;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Generators;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Steps;

/// <summary>
/// Verifies that the cipher generation steps spread login passwords over every strength of the default
/// password distribution when logins lead the cipher type mix, as they do in the default mix. The generator
/// comes from <see cref="InitGeneratorStep"/>, which builds it without a cipher count, as the preset path does.
/// Each login's password must be the pooled or the unique password of the strength its position selects. Logins
/// with their own key are skipped: the org or user key cannot decrypt them.
/// </summary>
public sealed class GeneratedLoginPasswordStrengthTests
{
    // 300 ciphers leave 4 keyless logins in the rarest strength (VeryStrong) and 12 or more in the others; 250 leave 2
    private const int _cipherCount = 300;

    // The default mix's 60% login share and position, without SSH keys: composing the first SSH key cipher
    // builds a pool of 500 RSA keys, which takes about 30 seconds.
    private static readonly Distribution<CipherType> _loginsLeadTheMix = new(
        (CipherType.Login, 0.60),
        (CipherType.SecureNote, 0.40));

    [Fact]
    public void GenerateCiphersStep_LoginsLeadTheTypeMix_LoginsCoverAllFiveStrengths()
    {
        var context = SeederStepTestHelpers.NewContext(new SeederSettings());
        SeederStepTestHelpers.PreloadOrganization(context);
        InitGeneratorStep.FromDomain(SeederStepTestHelpers.TestDomain).Execute(context);

        new GenerateCiphersStep(_cipherCount, _loginsLeadTheMix).Execute(context);

        AssertLoginsCoverAllFiveStrengths(context, context.Ciphers, context.RequireOrgKey());
    }

    [Fact]
    public void GeneratePersonalCiphersStep_LoginsLeadTheTypeMix_LoginsCoverAllFiveStrengths()
    {
        var context = SeederStepTestHelpers.NewContext(new SeederSettings());
        InitGeneratorStep.FromDomain(SeederStepTestHelpers.TestDomain).Execute(context);
        var userKey = RustSdkService.GenerateUserKeys("personal-logins@example.com", "asdfasdfasdf").Key;
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.Empty, userKey));

        new GeneratePersonalCiphersStep(_cipherCount, _loginsLeadTheMix).Execute(context);

        AssertLoginsCoverAllFiveStrengths(context, context.Ciphers, userKey);
    }

    private static void AssertLoginsCoverAllFiveStrengths(
        SeederContext context,
        IReadOnlyList<Cipher> ciphers,
        string encryptionKey)
    {
        // Logins lead the mix, so a login's position in the cipher list is its login index
        var loginCount = CipherComposer.CountLogins(_loginsLeadTheMix, _cipherCount);
        var generator = context.RequireGenerator();
        var strengthsSeen = new HashSet<PasswordStrength>();

        foreach (var (cipher, index) in ciphers.Select((cipher, index) => (cipher, index)))
        {
            // An item with its own key needs that key unwrapped first; the org or user key alone cannot decrypt it
            if (cipher.Type != CipherType.Login || cipher.Key is not null)
            {
                continue;
            }

            var strength = PasswordDistributions.Realistic.Select(index, loginCount);
            var encrypted = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data)!.Password!;
            var password = RustSdkService.DecryptString(encrypted, encryptionKey);

            var isPooled = password == Passwords.GetPassword(index, strength);
            var isUnique = UniquePasswordGenerator.Supports(strength)
                && password == generator.UniquePassword.GenerateByIndex(index, strength);
            Assert.True(isPooled || isUnique, $"The login at index {index} has neither the pooled nor the unique password of strength {strength}");

            strengthsSeen.Add(strength);
        }

        Assert.Equal(Enum.GetValues<PasswordStrength>().Order(), strengthsSeen.Order());
    }
}
