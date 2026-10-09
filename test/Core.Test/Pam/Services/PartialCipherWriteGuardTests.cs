using System.Text.Json;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Pam.Services;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Services;

[SutProviderCustomize]
public class PartialCipherWriteGuardTests
{
    private const string Enc = "2.AAAA|BBBB|CCCC";

    // Serialized the way CipherRequestModel.ToCipher builds Data: PascalCase, nulls omitted.
    private static string Serialize(CipherData data) =>
        JsonSerializer.Serialize(data, data.GetType(), JsonHelpers.IgnoreWritingNull);

    private static CipherLoginData FullLogin() => new()
    {
        Name = Enc,
        Username = Enc,
        Password = Enc,
        PasswordRevisionDate = DateTime.UtcNow,
        Uris = [new CipherLoginData.CipherLoginUriData { Uri = Enc }],
    };

    // A save built from a partial: name and URIs re-encrypted, every withheld field blank.
    private static CipherLoginData PartialBuiltLogin() => new()
    {
        Name = Enc,
        Uris = [new CipherLoginData.CipherLoginUriData { Uri = Enc }],
        Fields = [],
        AutofillOnPageLoad = false,
    };

    [Theory]
    [InlineData("""{"Name":"2.A|B|C","PasswordRevisionDate":"2026-10-09T12:00:00Z"}""", false)]
    [InlineData("""{"Name":"2.A|B|C","Notes":""}""", false)]
    [InlineData("""{"Name":"2.A|B|C","Type":0}""", false)]
    [InlineData("""{"name":"2.A|B|C","uris":[{"uri":"2.A|B|C","match":null}]}""", false)]
    [InlineData("""{"Name":"2.A|B|C","Notes":"2.A|B|C"}""", true)]
    [InlineData("""{"notes":"2.A|B|C"}""", true)]
    [InlineData("""{"Name":"2.A|B|C","Fields":[{"Type":0,"Name":"2.A|B|C"}]}""", true)]
    [InlineData("""{"Name":"2.A|B|C","PasswordHistory":[{"Password":"2.A|B|C","LastUsedDate":"2026-10-09T12:00:00Z"}]}""", false)]
    [InlineData("""{"Name":"2.A|B|C","Number":"2.A|B|C","Brand":null}""", true)]
    [InlineData("", false)]
    public void HasSecretContent_ReadsOnlyValuesAPartialWithholds(string data, bool expected)
        => Assert.Equal(expected, PartialCipherWriteGuard.HasSecretContent(data));

    [Theory, BitAutoData]
    public async Task EnsureNotPartialShapedAsync_PartialBuiltWrite_Throws(
        SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher)
    {
        Arrange(sutProvider, cipher);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.EnsureNotPartialShapedAsync(cipher));
    }

    [Theory, BitAutoData]
    public async Task EnsureNotPartialShapedAsync_FullWrite_PassesWithoutReadingTheCipher(
        SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher)
    {
        Arrange(sutProvider, cipher);
        cipher.Data = Serialize(FullLogin());

        await sutProvider.Sut.EnsureNotPartialShapedAsync(cipher);

        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task EnsureNotPartialShapedAsync_StoredRowMissing_Passes(
        SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher)
    {
        Arrange(sutProvider, cipher);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns((Cipher?)null);

        await sutProvider.Sut.EnsureNotPartialShapedAsync(cipher);
    }

    [Theory, BitAutoData]
    public async Task EnsureNotPartialShapedAsync_BlobEncryptedWrite_DoesNotLookUp(
        SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher)
    {
        Arrange(sutProvider, cipher);
        cipher.Data = """{"format_version":1,"data":"2.A|B|C"}""";

        await sutProvider.Sut.EnsureNotPartialShapedAsync(cipher);

        await DidNotLookUp(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task EnsureNotPartialShapedAsync_StoredHasNoSecrets_Passes(
        SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher)
    {
        Arrange(sutProvider, cipher, stored: Serialize(PartialBuiltLogin()));

        await sutProvider.Sut.EnsureNotPartialShapedAsync(cipher);
    }

    [Theory]
    [BitAutoData(FeatureFlagKeys.Pam, false)]
    [BitAutoData(FeatureFlagKeys.PamDisablePartialCipherWriteGuard, true)]
    public async Task EnsureNotPartialShapedAsync_Disabled_DoesNotLookUp(
        string flag, bool value, SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher)
    {
        Arrange(sutProvider, cipher);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(flag).Returns(value);

        await sutProvider.Sut.EnsureNotPartialShapedAsync(cipher);

        await DidNotLookUp(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task EnsureNotPartialShapedAsync_OrgWithoutPam_DoesNotReadTheCipher(
        SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher)
    {
        Arrange(sutProvider, cipher);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(cipher.OrganizationId!.Value)
            .Returns(new OrganizationAbility { UsePam = false });

        await sutProvider.Sut.EnsureNotPartialShapedAsync(cipher);

        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task EnsureNotPartialShapedAsync_PersonalCipher_DoesNotLookUp(
        SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher)
    {
        Arrange(sutProvider, cipher);
        cipher.OrganizationId = null;

        await sutProvider.Sut.EnsureNotPartialShapedAsync(cipher);

        await DidNotLookUp(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task EnsureNotPartialShapedAsync_NewCipher_DoesNotLookUp(
        SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher)
    {
        Arrange(sutProvider, cipher);
        cipher.Id = default;

        await sutProvider.Sut.EnsureNotPartialShapedAsync(cipher);

        await DidNotLookUp(sutProvider);
    }

    // PAM on, kill switch off (unstubbed, as for a key LaunchDarkly doesn't hold), PAM org, stored full, incoming
    // partial-built.
    private static void Arrange(SutProvider<PartialCipherWriteGuard> sutProvider, Cipher cipher, string? stored = null)
    {
        cipher.Data = Serialize(PartialBuiltLogin());
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.Pam).Returns(true);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(cipher.OrganizationId!.Value)
            .Returns(new OrganizationAbility { UsePam = true });
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipher.Id)
            .Returns(new Cipher { Id = cipher.Id, Data = stored ?? Serialize(FullLogin()) });
    }

    private static async Task DidNotLookUp(SutProvider<PartialCipherWriteGuard> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .DidNotReceiveWithAnyArgs().GetOrganizationAbilityAsync(default);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }
}
