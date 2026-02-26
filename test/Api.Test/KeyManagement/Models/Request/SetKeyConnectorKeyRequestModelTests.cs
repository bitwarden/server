using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Request;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Models.Request;

public class SetKeyConnectorKeyRequestModelTests
{
    private const string _wrappedUserKey = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private const string _publicKey = "public-key";
    private const string _privateKey = "private-key";
    private const string _userKey = "user-key";
    private const string _orgIdentifier = "org-identifier";

    [Fact]
    public void Validate_V2Registration_Valid()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = _wrappedUserKey,
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = _publicKey,
                UserKeyEncryptedAccountPrivateKey = _privateKey
            },
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_V2Registration_WrappedUserKeyNotEncryptedString_Invalid()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = "not-encrypted-string",
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = _publicKey,
                UserKeyEncryptedAccountPrivateKey = _privateKey
            },
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results,
            r => r.ErrorMessage == "KeyConnectorKeyWrappedUserKey is not a valid encrypted string.");
    }

    [Fact]
    public void Validate_V1Registration_Valid()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            Key = _userKey,
            Keys = new KeysRequestModel
            {
                PublicKey = _publicKey,
                EncryptedPrivateKey = _privateKey
            },
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_V1Registration_MissingKey_Invalid()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            Key = null,
            Keys = new KeysRequestModel
            {
                PublicKey = _publicKey,
                EncryptedPrivateKey = _privateKey
            },
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "Key must be supplied.");
    }

    [Fact]
    public void Validate_V1Registration_MissingKeys_Invalid()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            Key = _userKey,
            Keys = null,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "Keys must be supplied.");
    }

    [Fact]
    public void Validate_V1Registration_MissingKdf_Invalid()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            Key = _userKey,
            Keys = new KeysRequestModel
            {
                PublicKey = _publicKey,
                EncryptedPrivateKey = _privateKey
            },
            Kdf = null,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "Kdf must be supplied.");
    }

    [Fact]
    public void Validate_V1Registration_MissingKdfIterations_Invalid()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            Key = _userKey,
            Keys = new KeysRequestModel
            {
                PublicKey = _publicKey,
                EncryptedPrivateKey = _privateKey
            },
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = null,
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "KdfIterations must be supplied.");
    }

    [Fact]
    public void Validate_V1Registration_Argon2id_MissingKdfMemory_Invalid()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            Key = _userKey,
            Keys = new KeysRequestModel
            {
                PublicKey = _publicKey,
                EncryptedPrivateKey = _privateKey
            },
            Kdf = KdfType.Argon2id,
            KdfIterations = AuthConstants.ARGON2_ITERATIONS.Default,
            KdfMemory = null,
            KdfParallelism = AuthConstants.ARGON2_PARALLELISM.Default,
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "KdfMemory must be supplied when Kdf is Argon2id.");
    }

    [Fact]
    public void Validate_V1Registration_Argon2id_MissingKdfParallelism_Invalid()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            Key = _userKey,
            Keys = new KeysRequestModel
            {
                PublicKey = _publicKey,
                EncryptedPrivateKey = _privateKey
            },
            Kdf = KdfType.Argon2id,
            KdfIterations = AuthConstants.ARGON2_ITERATIONS.Default,
            KdfMemory = AuthConstants.ARGON2_MEMORY.Default,
            KdfParallelism = null,
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "KdfParallelism must be supplied when Kdf is Argon2id.");
    }

    [Fact]
    public void ToKeyConnectorKeysData_EmptyKeyConnectorKeyWrappedUserKey_ThrowsException()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = "",
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = _publicKey,
                UserKeyEncryptedAccountPrivateKey = _privateKey
            },
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var exception = Assert.Throws<BadRequestException>(() => model.ToKeyConnectorKeysData());

        // Assert
        Assert.Equal("KeyConnectorKeyWrappedUserKey and AccountKeys must be supplied.", exception.Message);
    }

    [Fact]
    public void ToKeyConnectorKeysData_NullKeyConnectorKeyWrappedUserKey_ThrowsException()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = null,
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = _publicKey,
                UserKeyEncryptedAccountPrivateKey = _privateKey
            },
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var exception = Assert.Throws<BadRequestException>(() => model.ToKeyConnectorKeysData());

        // Assert
        Assert.Equal("KeyConnectorKeyWrappedUserKey and AccountKeys must be supplied.", exception.Message);
    }

    [Fact]
    public void ToKeyConnectorKeysData_NullAccountKeys_ThrowsException()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = _wrappedUserKey,
            AccountKeys = null,
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var exception = Assert.Throws<BadRequestException>(() => model.ToKeyConnectorKeysData());

        // Assert
        Assert.Equal("KeyConnectorKeyWrappedUserKey and AccountKeys must be supplied.", exception.Message);
    }

    [Fact]
    public void ToKeyConnectorKeysData_Valid_Success()
    {
        // Arrange
        var model = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = _wrappedUserKey,
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = _publicKey,
                UserKeyEncryptedAccountPrivateKey = _privateKey
            },
            OrgIdentifier = _orgIdentifier
        };

        // Act
        var data = model.ToKeyConnectorKeysData();

        // Assert
        Assert.Equal(_wrappedUserKey, data.KeyConnectorKeyWrappedUserKey);
        Assert.Equal(_publicKey, data.AccountKeys.AccountPublicKey);
        Assert.Equal(_privateKey, data.AccountKeys.UserKeyEncryptedAccountPrivateKey);
        Assert.Equal(_orgIdentifier, data.OrgIdentifier);
    }

    private static List<ValidationResult> Validate(SetKeyConnectorKeyRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
