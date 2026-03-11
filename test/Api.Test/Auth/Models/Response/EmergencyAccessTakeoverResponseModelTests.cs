using Bit.Api.Auth.Models.Response;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response;

public class EmergencyAccessTakeoverResponseModelTests
{
    [Theory]
    [BitAutoData]
    public void Constructor_EmergencyAccessNull_ThrowsArgumentNullException(User grantor)
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EmergencyAccessTakeoverResponseModel(null, grantor));
        Assert.Equal("emergencyAccess", exception.ParamName);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_ValidInputs_SetsAllPropertiesCorrectly(
        EmergencyAccess emergencyAccess, User grantor)
    {
        var model = new EmergencyAccessTakeoverResponseModel(emergencyAccess, grantor);

        Assert.Equal(emergencyAccess.KeyEncrypted, model.KeyEncrypted);
        Assert.Equal(grantor.Kdf, model.Kdf);
        Assert.Equal(grantor.KdfIterations, model.KdfIterations);
        Assert.Equal(grantor.KdfMemory, model.KdfMemory);
        Assert.Equal(grantor.KdfParallelism, model.KdfParallelism);
        Assert.Equal(grantor.GetMasterPasswordSalt(), model.Salt);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_Salt_EqualsGrantorEmailLowercasedAndTrimmed(
        EmergencyAccess emergencyAccess, User grantor)
    {
        grantor.Email = "  TEST@Example.COM  ";

        var model = new EmergencyAccessTakeoverResponseModel(emergencyAccess, grantor);

        Assert.Equal("test@example.com", model.Salt);
    }

    [Theory]
    [InlineData("user@domain.com", "user@domain.com")]
    [InlineData("USER@DOMAIN.COM", "user@domain.com")]
    [InlineData("  user@domain.com  ", "user@domain.com")]
    [InlineData("  USER@DOMAIN.COM  ", "user@domain.com")]
    public void Constructor_SaltWithVariousEmailFormats_NormalizesCorrectly(
        string email, string expectedSalt)
    {
        var emergencyAccess = new EmergencyAccess
        {
            Id = Guid.NewGuid(),
            KeyEncrypted = "test-key-encrypted"
        };
        var grantor = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            SecurityStamp = "security-stamp",
            ApiKey = "api-key"
        };

        var model = new EmergencyAccessTakeoverResponseModel(emergencyAccess, grantor);

        Assert.Equal(expectedSalt, model.Salt);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithPBKDF2_SetsKdfTypeCorrectly(
        EmergencyAccess emergencyAccess, User grantor)
    {
        grantor.Kdf = KdfType.PBKDF2_SHA256;
        grantor.KdfIterations = 600000;
        grantor.KdfMemory = null;
        grantor.KdfParallelism = null;

        var model = new EmergencyAccessTakeoverResponseModel(emergencyAccess, grantor);

        Assert.Equal(KdfType.PBKDF2_SHA256, model.Kdf);
        Assert.Equal(600000, model.KdfIterations);
        Assert.Null(model.KdfMemory);
        Assert.Null(model.KdfParallelism);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithArgon2id_SetsAllKdfPropertiesCorrectly(
        EmergencyAccess emergencyAccess, User grantor)
    {
        grantor.Kdf = KdfType.Argon2id;
        grantor.KdfIterations = 3;
        grantor.KdfMemory = 64;
        grantor.KdfParallelism = 4;

        var model = new EmergencyAccessTakeoverResponseModel(emergencyAccess, grantor);

        Assert.Equal(KdfType.Argon2id, model.Kdf);
        Assert.Equal(3, model.KdfIterations);
        Assert.Equal(64, model.KdfMemory);
        Assert.Equal(4, model.KdfParallelism);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_SetsObjectTypeCorrectly(
        EmergencyAccess emergencyAccess, User grantor)
    {
        var model = new EmergencyAccessTakeoverResponseModel(emergencyAccess, grantor);

        Assert.Equal("emergencyAccessTakeover", model.Object);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_CustomObjectName_SetsObjectTypeCorrectly(
        EmergencyAccess emergencyAccess, User grantor)
    {
        var model = new EmergencyAccessTakeoverResponseModel(emergencyAccess, grantor, "customObject");

        Assert.Equal("customObject", model.Object);
    }
}
