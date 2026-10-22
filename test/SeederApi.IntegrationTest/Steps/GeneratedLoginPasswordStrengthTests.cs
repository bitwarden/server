using System.Text.Json;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.RustSDK;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Steps;

/// <summary>
/// Verifies that the cipher generation steps spread login passwords over every strength of the default
/// password distribution when logins lead the cipher type mix, as they do in the default mix. The generator
/// comes from <see cref="InitGeneratorStep"/>, which builds it without a cipher count, as the preset path does.
/// Only pooled passwords on logins without their own key are counted: those are the ones a pool lookup can
/// classify and the org or user key can decrypt.
/// </summary>
public sealed class GeneratedLoginPasswordStrengthTests
{
    // 300 logins: enough pooled, user-key logins to reach every strength (100 ciphers miss VeryStrong)
    private const int _cipherCount = 500;

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

        AssertLoginsCoverAllFiveStrengths(context.Ciphers, context.RequireOrgKey());
    }

    [Fact]
    public void GeneratePersonalCiphersStep_LoginsLeadTheTypeMix_LoginsCoverAllFiveStrengths()
    {
        var context = SeederStepTestHelpers.NewContext(new SeederSettings());
        InitGeneratorStep.FromDomain(SeederStepTestHelpers.TestDomain).Execute(context);
        var userKey = RustSdkService.GenerateUserKeys("personal-logins@example.com", "asdfasdfasdf").Key;
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.Empty, userKey));

        new GeneratePersonalCiphersStep(_cipherCount, _loginsLeadTheMix).Execute(context);

        AssertLoginsCoverAllFiveStrengths(context.Ciphers, userKey);
    }

    private static void AssertLoginsCoverAllFiveStrengths(IEnumerable<Cipher> ciphers, string encryptionKey)
    {
        var passwords = ciphers
            .Where(cipher => cipher.Type == CipherType.Login)
            // An item with its own key needs that key unwrapped first; the org or user key alone cannot decrypt it
            .Where(cipher => cipher.Key is null)
            .Select(cipher => JsonSerializer.Deserialize<CipherLoginData>(cipher.Data)!.Password!)
            .Select(encrypted => RustSdkService.DecryptString(encrypted, encryptionKey))
            .ToList();

        Assert.All(Enum.GetValues<PasswordStrength>(), strength =>
            Assert.Contains(passwords, password => Passwords.GetByStrength(strength).Contains(password)));
    }
}
