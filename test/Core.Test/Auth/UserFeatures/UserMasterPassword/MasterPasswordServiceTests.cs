using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

[SutProviderCustomize]
public class MasterPasswordServiceTests
{
    private static SutProvider<MasterPasswordService> CreateSutProvider()
        => new SutProvider<MasterPasswordService>().WithFakeTimeProvider().Create();

    // Returns a KdfSettings that exactly matches the user's stored KDF values plus a salt derived
    // from the user's current GetMasterPasswordSalt() output.
    private static (KdfSettings kdf, string salt) GetMatchingKdfAndSalt(User user)
    {
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var salt = user.GetMasterPasswordSalt();
        return (kdf, salt);
    }

    private static SetInitialPasswordData BuildSetInitialData(User user, string? hint = null,
        bool validatePassword = false)
    {
        // Stage 1: salt == email while MasterPasswordSalt is null (PM-28143 separates them in Stage 3).
        var salt = user.GetMasterPasswordSalt();
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        return new SetInitialPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            MasterPasswordHint = hint,
            ValidatePassword = validatePassword,
            RefreshStamp = false
        };
    }

    private static UpdateExistingPasswordData BuildUpdateExistingData(User user, string? hint = null,
        bool validatePassword = false)
    {
        var (kdf, salt) = GetMatchingKdfAndSalt(user);
        return new UpdateExistingPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            MasterPasswordHint = hint,
            ValidatePassword = validatePassword,
            RefreshStamp = false
        };
    }

    private static UpdateExistingPasswordAndKdfData BuildUpdateExistingAndKdfData(User user,
        KdfSettings? newKdf = null, string? hint = null, bool validatePassword = false)
    {
        var salt = user.GetMasterPasswordSalt();
        var kdf = newKdf ?? new KdfSettings
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4
        };
        return new UpdateExistingPasswordAndKdfData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            MasterPasswordHint = hint,
            ValidatePassword = validatePassword,
            RefreshStamp = false
        };
    }

    // --- PrepareSetInitialMasterPassword ---

    [Theory, BitAutoData]
    public async Task PrepareSetInitialMasterPassword_Success(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var data = BuildSetInitialData(user);
        var expectedHash = "server-side-hash";
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(user, data.MasterPasswordAuthentication.MasterPasswordAuthenticationHash)
            .Returns(expectedHash);

        var result = await sutProvider.Sut.PrepareSetInitialMasterPasswordAsync(user, data);

        var expectedTime = sutProvider.GetDependency<TimeProvider>().GetUtcNow().UtcDateTime;

        Assert.True(result.IsT0);
        Assert.Same(user, result.AsT0);
        Assert.Equal(expectedHash, user.MasterPassword);
        Assert.Equal(data.MasterPasswordUnlock.MasterKeyWrappedUserKey, user.Key);
        Assert.Equal(data.MasterPasswordUnlock.Salt, user.MasterPasswordSalt);
        Assert.Equal(data.MasterPasswordUnlock.Kdf.KdfType, user.Kdf);
        Assert.Equal(data.MasterPasswordUnlock.Kdf.Iterations, user.KdfIterations);
        Assert.Equal(expectedTime, user.LastPasswordChangeDate);
        Assert.Equal(expectedTime, user.RevisionDate);
        Assert.Equal(user.RevisionDate, user.AccountRevisionDate);
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialMasterPassword_SetsMasterPasswordHint(User user, string hint)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var data = BuildSetInitialData(user, hint: hint);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        var result = await sutProvider.Sut.PrepareSetInitialMasterPasswordAsync(user, data);

        Assert.True(result.IsT0);
        Assert.Equal(hint, result.AsT0.MasterPasswordHint);
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialMasterPassword_ThrowsWhenUserNotHydrated(User user)
    {
        var sutProvider = CreateSutProvider();
        user.Id = default;
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var data = BuildSetInitialData(user);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.PrepareSetInitialMasterPasswordAsync(user, data));
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialMasterPassword_RefreshStampTrue_RotatesSecurityStamp(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        // Build data with RefreshStamp = true (the default — do not override).
        var salt = user.GetMasterPasswordSalt();
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var data = new SetInitialPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            ValidatePassword = false,
            RefreshStamp = true
        };
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        var originalStamp = user.SecurityStamp;

        await sutProvider.Sut.PrepareSetInitialMasterPasswordAsync(user, data);

        Assert.NotEqual(originalStamp, user.SecurityStamp);
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialMasterPassword_RefreshStampFalse_PreservesSecurityStamp(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var data = BuildSetInitialData(user); // RefreshStamp = false
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        var originalStamp = user.SecurityStamp;

        await sutProvider.Sut.PrepareSetInitialMasterPasswordAsync(user, data);

        Assert.Equal(originalStamp, user.SecurityStamp);
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialMasterPassword_ValidationFailure_ReturnsErrorsAsT1(User user)
    {
        var error = new IdentityError { Code = "test", Description = "test error" };
        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(error));

        var sutProvider = new SutProvider<MasterPasswordService>()
            .WithFakeTimeProvider()
            .SetDependency<IEnumerable<IPasswordValidator<User>>>(new[] { validator })
            .Create();

        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var data = BuildSetInitialData(user, validatePassword: true);

        var result = await sutProvider.Sut.PrepareSetInitialMasterPasswordAsync(user, data);

        Assert.True(result.IsT1);
        Assert.NotEmpty(result.AsT1);
    }

    // --- SaveSetInitialMasterPassword ---

    [Theory, BitAutoData]
    public async Task SaveSetInitialMasterPassword_PreparesAndPersists(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var data = BuildSetInitialData(user);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        var result = await sutProvider.Sut.SaveSetInitialMasterPasswordAsync(user, data);

        Assert.True(result.IsT0);
        Assert.NotNull(user.MasterPassword);
        await sutProvider.GetDependency<IUserRepository>().Received().ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task SaveSetInitialMasterPassword_WhenValidationFails_ReturnsErrorsAndDoesNotPersist(User user)
    {
        var error = new IdentityError { Code = "pwd-invalid", Description = "Password is too weak." };
        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(error));

        var sutProvider = new SutProvider<MasterPasswordService>()
            .WithFakeTimeProvider()
            .SetDependency<IEnumerable<IPasswordValidator<User>>>(new[] { validator })
            .Create();

        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var data = BuildSetInitialData(user, validatePassword: true);

        var result = await sutProvider.Sut.SaveSetInitialMasterPasswordAsync(user, data);

        Assert.True(result.IsT1);
        Assert.NotEmpty(result.AsT1);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    [Theory, BitAutoData]
    public async Task SaveSetInitialMasterPassword_ThrowsWhenUserNotHydrated(User user)
    {
        var sutProvider = CreateSutProvider();
        user.Id = default;

        var data = BuildSetInitialData(user);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.SaveSetInitialMasterPasswordAsync(user, data));
    }

    // --- PrepareUpdateExistingMasterPassword ---

    [Theory, BitAutoData]
    public async Task PrepareUpdateExistingMasterPassword_Success(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingData(user);
        var expectedHash = "new-server-hash";
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(user, data.MasterPasswordAuthentication.MasterPasswordAuthenticationHash)
            .Returns(expectedHash);

        var result = await sutProvider.Sut.PrepareUpdateExistingMasterPasswordAsync(user, data);

        var expectedTime = sutProvider.GetDependency<TimeProvider>().GetUtcNow().UtcDateTime;

        Assert.True(result.IsT0);
        Assert.Same(user, result.AsT0);
        Assert.Equal(expectedHash, user.MasterPassword);
        Assert.Equal(data.MasterPasswordUnlock.MasterKeyWrappedUserKey, user.Key);
        Assert.Equal(expectedTime, user.LastPasswordChangeDate);
        Assert.Equal(expectedTime, user.RevisionDate);
        Assert.Equal(user.RevisionDate, user.AccountRevisionDate);
    }

    [Theory, BitAutoData]
    public async Task PrepareUpdateExistingMasterPassword_SetsMasterPasswordHint(User user, string hint)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingData(user, hint: hint);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        var result = await sutProvider.Sut.PrepareUpdateExistingMasterPasswordAsync(user, data);

        Assert.True(result.IsT0);
        Assert.Equal(hint, result.AsT0.MasterPasswordHint);
    }

    [Theory, BitAutoData]
    public async Task PrepareUpdateExistingMasterPassword_ThrowsWhenUserNotHydrated(User user)
    {
        var sutProvider = CreateSutProvider();
        user.Id = default;
        user.MasterPassword = "existing-hash";

        var data = BuildUpdateExistingData(user);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.PrepareUpdateExistingMasterPasswordAsync(user, data));
    }

    // --- PrepareUpdateExistingMasterPassword — OneOf return shape ---

    [Theory, BitAutoData]
    public async Task PrepareUpdateExistingMasterPassword_ValidationFailure_ReturnsErrorsAsT1(User user)
    {
        var error = new IdentityError { Code = "test", Description = "test error" };
        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(error));

        var sutProvider = new SutProvider<MasterPasswordService>()
            .WithFakeTimeProvider()
            .SetDependency<IEnumerable<IPasswordValidator<User>>>(new[] { validator })
            .Create();

        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingData(user, validatePassword: true);

        var result = await sutProvider.Sut.PrepareUpdateExistingMasterPasswordAsync(user, data);

        Assert.True(result.IsT1);
        Assert.NotEmpty(result.AsT1);
    }

    // --- SaveUpdateExistingMasterPassword ---

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPassword_PreparesAndPersists(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingData(user);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("new-hash");

        var result = await sutProvider.Sut.SaveUpdateExistingMasterPasswordAsync(user, data);

        Assert.True(result.IsT0);
        Assert.Equal("new-hash", user.MasterPassword);
        await sutProvider.GetDependency<IUserRepository>().Received().ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPassword_WhenValidationFails_ReturnsErrorsAndDoesNotPersist(User user)
    {
        var error = new IdentityError { Code = "pwd-invalid", Description = "Password is too weak." };
        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(error));

        var sutProvider = new SutProvider<MasterPasswordService>()
            .WithFakeTimeProvider()
            .SetDependency<IEnumerable<IPasswordValidator<User>>>(new[] { validator })
            .Create();

        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingData(user, validatePassword: true);

        var result = await sutProvider.Sut.SaveUpdateExistingMasterPasswordAsync(user, data);

        Assert.True(result.IsT1);
        Assert.NotEmpty(result.AsT1);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    // --- PrepareSetInitialOrUpdateExistingMasterPassword — Dispatch routing ---

    [Theory, BitAutoData]
    public async Task PrepareSetInitialOrUpdateExisting_RoutesToSetInitial_WhenNoMasterPassword(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        // Stage 1: salt == email while MasterPasswordSalt is null (PM-28143 separates them in Stage 3).
        var salt = user.GetMasterPasswordSalt();
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var data = new SetInitialOrUpdateExistingPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            ValidatePassword = false,
            RefreshStamp = false
        };
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        var result = await sutProvider.Sut.PrepareSetInitialOrUpdateExistingMasterPasswordAsync(user, data);

        Assert.True(result.IsT0);
        // Set-initial path hashes the password and sets the wrapped key — proving it ran, not update.
        Assert.NotNull(user.MasterPassword);
        Assert.Equal("wrapped-key", user.Key);
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialOrUpdateExisting_RoutesToUpdateExisting_WhenHasMasterPassword(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var (kdf, salt) = GetMatchingKdfAndSalt(user);
        var data = new SetInitialOrUpdateExistingPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            ValidatePassword = false,
            RefreshStamp = false
        };
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("new-hash");

        var result = await sutProvider.Sut.PrepareSetInitialOrUpdateExistingMasterPasswordAsync(user, data);

        Assert.True(result.IsT0);
        // Update-existing path hashes the new password and sets the wrapped key.
        Assert.Equal("new-hash", user.MasterPassword);
        Assert.Equal("wrapped-key", user.Key);
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialOrUpdateExisting_ThrowsBadRequest_WhenUpdatePathSaltMismatch(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        // Use a salt that does not match the user's current salt to trigger BadRequestException
        var (kdf, _) = GetMatchingKdfAndSalt(user);
        var wrongSalt = "wrong-salt-value";
        var data = new SetInitialOrUpdateExistingPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = wrongSalt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = wrongSalt,
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            ValidatePassword = false,
            RefreshStamp = false
        };

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PrepareSetInitialOrUpdateExistingMasterPasswordAsync(user, data));
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialOrUpdateExisting_PropagatesValidationErrors_WhenSetInitialPathFails(User user)
    {
        var error = new IdentityError { Code = "pwd-invalid", Description = "Password is too weak." };
        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(error));

        var sutProvider = new SutProvider<MasterPasswordService>()
            .WithFakeTimeProvider()
            .SetDependency<IEnumerable<IPasswordValidator<User>>>(new[] { validator })
            .Create();

        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var (kdf, salt) = GetMatchingKdfAndSalt(user);
        var data = new SetInitialOrUpdateExistingPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            ValidatePassword = true,
            RefreshStamp = false
        };

        var result = await sutProvider.Sut.PrepareSetInitialOrUpdateExistingMasterPasswordAsync(user, data);

        Assert.True(result.IsT1);
        Assert.NotEmpty(result.AsT1);
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialOrUpdateExisting_ThrowsWhenUserNotHydrated(User user)
    {
        var sutProvider = CreateSutProvider();
        user.Id = default;

        var (kdf, salt) = GetMatchingKdfAndSalt(user);
        var data = new SetInitialOrUpdateExistingPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            ValidatePassword = false,
            RefreshStamp = false
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.PrepareSetInitialOrUpdateExistingMasterPasswordAsync(user, data));
    }

    // --- SaveUpdateExistingMasterPasswordAndKdf ---

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPasswordAndKdf_Success(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingAndKdfData(user);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("new-hash");

        var result = await sutProvider.Sut.SaveUpdateExistingMasterPasswordAndKdfAsync(user, data);

        var expectedTime = sutProvider.GetDependency<TimeProvider>().GetUtcNow().UtcDateTime;

        Assert.True(result.IsT0);
        Assert.Equal(data.MasterPasswordUnlock.Kdf.KdfType, user.Kdf);
        Assert.Equal(data.MasterPasswordUnlock.Kdf.Iterations, user.KdfIterations);
        Assert.Equal(expectedTime, user.LastKdfChangeDate);
        await sutProvider.GetDependency<IUserRepository>().Received().ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPasswordAndKdf_RotatesPbkdf2ToArgon2id(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;
        user.Kdf = KdfType.PBKDF2_SHA256;
        user.KdfIterations = 600000;
        user.KdfMemory = null;
        user.KdfParallelism = null;

        var newKdf = new KdfSettings
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4
        };
        var data = BuildUpdateExistingAndKdfData(user, newKdf: newKdf);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("new-hash");

        var result = await sutProvider.Sut.SaveUpdateExistingMasterPasswordAndKdfAsync(user, data);

        Assert.True(result.IsT0);
        Assert.Equal(KdfType.Argon2id, user.Kdf);
        Assert.Equal(3, user.KdfIterations);
        Assert.Equal(64, user.KdfMemory);
        Assert.Equal(4, user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPasswordAndKdf_RotatesArgon2idToPbkdf2(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;
        user.Kdf = KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;

        var newKdf = new KdfSettings
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 600000,
            Memory = null,
            Parallelism = null
        };
        var data = BuildUpdateExistingAndKdfData(user, newKdf: newKdf);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("new-hash");

        var result = await sutProvider.Sut.SaveUpdateExistingMasterPasswordAndKdfAsync(user, data);

        Assert.True(result.IsT0);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(600000, user.KdfIterations);
        Assert.Null(user.KdfMemory);
        Assert.Null(user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPasswordAndKdf_WhenValidationFails_ReturnsErrorsAndDoesNotPersist(User user)
    {
        var error = new IdentityError { Code = "pwd-invalid", Description = "Password is too weak." };
        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(error));

        var sutProvider = new SutProvider<MasterPasswordService>()
            .WithFakeTimeProvider()
            .SetDependency<IEnumerable<IPasswordValidator<User>>>(new[] { validator })
            .Create();

        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingAndKdfData(user, validatePassword: true);

        var result = await sutProvider.Sut.SaveUpdateExistingMasterPasswordAndKdfAsync(user, data);

        Assert.True(result.IsT1);
        Assert.NotEmpty(result.AsT1);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPasswordAndKdf_ThrowsWhenSaltChanged(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var salt = user.GetMasterPasswordSalt();
        var kdf = new KdfSettings
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4
        };
        var data = new UpdateExistingPasswordAndKdfData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = "wrong-salt",
                MasterPasswordAuthenticationHash = "test-hash",
                Kdf = kdf
            },
            ValidatePassword = false,
            RefreshStamp = false
        };

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUpdateExistingMasterPasswordAndKdfAsync(user, data));
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPasswordAndKdf_ThrowsWhenNoExistingPassword(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;

        var data = BuildUpdateExistingAndKdfData(user);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUpdateExistingMasterPasswordAndKdfAsync(user, data));
    }

    // --- BuildUpdateUserDelegateSetInitialMasterPassword ---

    public class BuildUpdateUserDelegateSetInitialMasterPasswordTests
    {
        [Theory, BitAutoData]
        public void BuildUpdateUserDelegate_ThrowsWhenUserNotHydrated(User user)
        {
            var sutProvider = new SutProvider<MasterPasswordService>().WithFakeTimeProvider().Create();
            user.Id = default;
            user.MasterPassword = null;
            user.Key = null;
            user.MasterPasswordSalt = null;
            user.UsesKeyConnector = false;

            var data = BuildSetInitialDataForUser(user);

            Assert.Throws<ArgumentException>(
                () => sutProvider.Sut.BuildUpdateUserDelegateSetInitialMasterPassword(user, data));
        }

        [Theory, BitAutoData]
        public void BuildUpdateUserDelegate_HappyPath_ReturnsNonNullDelegateAndDoesNotPersist(User user)
        {
            var sutProvider = new SutProvider<MasterPasswordService>().WithFakeTimeProvider().Create();
            user.MasterPassword = null;
            user.Key = null;
            user.MasterPasswordSalt = null;
            user.UsesKeyConnector = false;

            var data = BuildSetInitialDataForUser(user);

            var result = sutProvider.Sut.BuildUpdateUserDelegateSetInitialMasterPassword(user, data);

            // The Build* tier returns a delegate — it must not persist directly.
            Assert.NotNull(result);
            sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
        }

        [Theory, BitAutoData]
        public void BuildUpdateUserDelegate_ThrowsWhenUserAlreadyHasMasterPassword(User user)
        {
            var sutProvider = new SutProvider<MasterPasswordService>().WithFakeTimeProvider().Create();
            // User already has a master password — ValidateDataForUser must be called eagerly.
            user.MasterPassword = "existing-hash";

            var data = BuildSetInitialDataForUser(user);

            Assert.Throws<BadRequestException>(
                () => sutProvider.Sut.BuildUpdateUserDelegateSetInitialMasterPassword(user, data));
        }

        // Contract: when ValidatePassword is true and validation succeeds the delegate must complete
        // and pass the server-side hashed password to the repository.
        [Theory, BitAutoData]
        public async Task BuildUpdateUserDelegate_WhenValidatePasswordTrue_CompletesAndPersistsHash(User user)
        {
            var validator = Substitute.For<IPasswordValidator<User>>();
            validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
                .Returns(IdentityResult.Success);

            var sutProvider = new SutProvider<MasterPasswordService>()
                .WithFakeTimeProvider()
                .SetDependency<IEnumerable<IPasswordValidator<User>>>(new[] { validator })
                .Create();

            user.MasterPassword = null;
            user.Key = null;
            user.MasterPasswordSalt = null;
            user.UsesKeyConnector = false;

            var expectedHash = "expected-hash";
            sutProvider.GetDependency<IPasswordHasher<User>>()
                .HashPassword(Arg.Any<User>(), Arg.Any<string>())
                .Returns(expectedHash);

            UpdateUserData noopWrite = (_, _) => Task.CompletedTask;
            sutProvider.GetDependency<IUserRepository>()
                .SetMasterPassword(Arg.Any<Guid>(), Arg.Any<MasterPasswordUnlockData>(),
                    Arg.Any<string>(), Arg.Any<string?>())
                .Returns(noopWrite);

            var data = BuildSetInitialDataForUser(user);
            data.ValidatePassword = true;

            var write = sutProvider.Sut.BuildUpdateUserDelegateSetInitialMasterPassword(user, data);
            await write(null, null);

            // The Build* tier's output is the values handed to the repository — user.MasterPassword
            // is not mutated; the hash is passed through to SetMasterPassword directly.
            sutProvider.GetDependency<IUserRepository>().Received()
                .SetMasterPassword(user.Id, data.MasterPasswordUnlock, expectedHash, Arg.Any<string?>());
        }

        // Contract: validator failure must surface through the delegate — Build* callers composing
        // a batch transaction need the failure to roll the transaction back, not silently persist.
        [Theory, BitAutoData]
        public async Task BuildUpdateUserDelegate_WhenValidationFails_DelegateInvocationThrows(User user)
        {
            var error = new IdentityError { Code = "pwd-invalid", Description = "Password is too weak." };
            var validator = Substitute.For<IPasswordValidator<User>>();
            validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
                .Returns(IdentityResult.Failed(error));

            var sutProvider = new SutProvider<MasterPasswordService>()
                .WithFakeTimeProvider()
                .SetDependency<IEnumerable<IPasswordValidator<User>>>(new[] { validator })
                .Create();

            user.MasterPassword = null;
            user.Key = null;
            user.MasterPasswordSalt = null;
            user.UsesKeyConnector = false;

            UpdateUserData noopWrite = (_, _) => Task.CompletedTask;
            sutProvider.GetDependency<IUserRepository>()
                .SetMasterPassword(Arg.Any<Guid>(), Arg.Any<MasterPasswordUnlockData>(),
                    Arg.Any<string>(), Arg.Any<string?>())
                .Returns(noopWrite);

            var data = BuildSetInitialDataForUser(user);
            data.ValidatePassword = true;

            var write = sutProvider.Sut.BuildUpdateUserDelegateSetInitialMasterPassword(user, data);

            await Assert.ThrowsAsync<BadRequestException>(() => write(null, null));
        }

        // Contract: SetInitialPasswordData.RefreshStamp XML-docs say SecurityStamp rotates when true.
        // Prepare*/Save* honor this; Build* must too (rotation composed into the returned delegate).
        [Theory, BitAutoData]
        public async Task BuildUpdateUserDelegate_WhenRefreshStampTrue_RotatesSecurityStamp(User user)
        {
            var sutProvider = new SutProvider<MasterPasswordService>().WithFakeTimeProvider().Create();
            user.MasterPassword = null;
            user.Key = null;
            user.MasterPasswordSalt = null;
            user.UsesKeyConnector = false;
            user.SecurityStamp = "original-stamp";

            UpdateUserData noopWrite = (_, _) => Task.CompletedTask;
            sutProvider.GetDependency<IUserRepository>()
                .SetMasterPassword(Arg.Any<Guid>(), Arg.Any<MasterPasswordUnlockData>(),
                    Arg.Any<string>(), Arg.Any<string?>())
                .Returns(noopWrite);

            var data = BuildSetInitialDataForUser(user);
            data.RefreshStamp = true;

            var write = sutProvider.Sut.BuildUpdateUserDelegateSetInitialMasterPassword(user, data);
            await write(null, null);

            Assert.NotEqual("original-stamp", user.SecurityStamp);
        }

        private static SetInitialPasswordData BuildSetInitialDataForUser(User user)
        {
            var salt = user.GetMasterPasswordSalt();
            var kdf = new KdfSettings
            {
                KdfType = user.Kdf,
                Iterations = user.KdfIterations,
                Memory = user.KdfMemory,
                Parallelism = user.KdfParallelism
            };
            return new SetInitialPasswordData
            {
                MasterPasswordUnlock = new MasterPasswordUnlockData
                {
                    Salt = salt,
                    MasterKeyWrappedUserKey = "wrapped-key",
                    Kdf = kdf
                },
                MasterPasswordAuthentication = new MasterPasswordAuthenticationData
                {
                    Salt = salt,
                    MasterPasswordAuthenticationHash = "test-hash",
                    Kdf = kdf
                },
                ValidatePassword = false,
                RefreshStamp = false
            };
        }
    }

    // --- Data model validation: SetInitialPasswordData ---

    public class SetInitialPasswordDataTests
    {
        private static User BuildValidSetInitialUser()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                MasterPassword = null,
                Key = null,
                MasterPasswordSalt = null,
                UsesKeyConnector = false,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = 600000
            };
            return user;
        }

        private static SetInitialPasswordData BuildData(User user, string? saltOverride = null)
        {
            // Stage 1: salt == email while MasterPasswordSalt is null (PM-28143 separates them in Stage 3).
            var salt = saltOverride ?? user.GetMasterPasswordSalt();
            var kdf = new KdfSettings
            {
                KdfType = user.Kdf,
                Iterations = user.KdfIterations,
                Memory = user.KdfMemory,
                Parallelism = user.KdfParallelism
            };
            return new SetInitialPasswordData
            {
                MasterPasswordUnlock = new MasterPasswordUnlockData
                {
                    Salt = salt,
                    MasterKeyWrappedUserKey = "wrapped-key",
                    Kdf = kdf
                },
                MasterPasswordAuthentication = new MasterPasswordAuthenticationData
                {
                    Salt = salt,
                    MasterPasswordAuthenticationHash = "hash",
                    Kdf = kdf
                }
            };
        }

        [Fact]
        public void ValidateDataForUser_Accepts_WhenUserHasNoMasterPassword()
        {
            var user = BuildValidSetInitialUser();
            var data = BuildData(user);

            // Should not throw
            data.ValidateDataForUser(user);
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenUserHasMasterPassword()
        {
            var user = BuildValidSetInitialUser();
            user.MasterPassword = "existing-hash";
            var data = BuildData(user);

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenUserHasKey()
        {
            var user = BuildValidSetInitialUser();
            user.Key = "existing-key";
            var data = BuildData(user);

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenUserHasSalt()
        {
            var user = BuildValidSetInitialUser();
            user.MasterPasswordSalt = "existing-salt";
            var data = BuildData(user, saltOverride: "existing-salt");

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenUserIsKeyConnector()
        {
            var user = BuildValidSetInitialUser();
            user.UsesKeyConnector = true;
            var data = BuildData(user);

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenSaltMismatch()
        {
            var user = BuildValidSetInitialUser();
            var data = BuildData(user, saltOverride: "wrong-salt");

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenAuthenticationSaltMismatch_UnlockSaltCorrect()
        {
            var user = BuildValidSetInitialUser();
            var correctSalt = user.GetMasterPasswordSalt();
            var kdf = new KdfSettings
            {
                KdfType = user.Kdf,
                Iterations = user.KdfIterations,
                Memory = user.KdfMemory,
                Parallelism = user.KdfParallelism
            };
            // Authentication salt is wrong; Unlock salt is correct.
            var data = new SetInitialPasswordData
            {
                MasterPasswordUnlock = new MasterPasswordUnlockData
                {
                    Salt = correctSalt,
                    MasterKeyWrappedUserKey = "wrapped-key",
                    Kdf = kdf
                },
                MasterPasswordAuthentication = new MasterPasswordAuthenticationData
                {
                    Salt = "wrong-auth-salt",
                    MasterPasswordAuthenticationHash = "hash",
                    Kdf = kdf
                }
            };

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenUnlockSaltMismatch_AuthenticationSaltCorrect()
        {
            var user = BuildValidSetInitialUser();
            var correctSalt = user.GetMasterPasswordSalt();
            var kdf = new KdfSettings
            {
                KdfType = user.Kdf,
                Iterations = user.KdfIterations,
                Memory = user.KdfMemory,
                Parallelism = user.KdfParallelism
            };
            // Unlock salt is wrong; Authentication salt is correct.
            var data = new SetInitialPasswordData
            {
                MasterPasswordUnlock = new MasterPasswordUnlockData
                {
                    Salt = "wrong-unlock-salt",
                    MasterKeyWrappedUserKey = "wrapped-key",
                    Kdf = kdf
                },
                MasterPasswordAuthentication = new MasterPasswordAuthenticationData
                {
                    Salt = correctSalt,
                    MasterPasswordAuthenticationHash = "hash",
                    Kdf = kdf
                }
            };

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }
    }

    // --- Data model validation: UpdateExistingPasswordData ---

    public class UpdateExistingPasswordDataTests
    {
        private static User BuildValidUpdateUser()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                MasterPassword = "existing-hash",
                Key = "existing-key",
                MasterPasswordSalt = "stored-salt",
                UsesKeyConnector = false,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = 600000
            };
            return user;
        }

        private static UpdateExistingPasswordData BuildData(User user, string? saltOverride = null,
            KdfSettings? kdfOverride = null)
        {
            var salt = saltOverride ?? user.GetMasterPasswordSalt();
            var kdf = kdfOverride ?? new KdfSettings
            {
                KdfType = user.Kdf,
                Iterations = user.KdfIterations,
                Memory = user.KdfMemory,
                Parallelism = user.KdfParallelism
            };
            return new UpdateExistingPasswordData
            {
                MasterPasswordUnlock = new MasterPasswordUnlockData
                {
                    Salt = salt,
                    MasterKeyWrappedUserKey = "wrapped-key",
                    Kdf = kdf
                },
                MasterPasswordAuthentication = new MasterPasswordAuthenticationData
                {
                    Salt = salt,
                    MasterPasswordAuthenticationHash = "hash",
                    Kdf = kdf
                }
            };
        }

        [Fact]
        public void ValidateDataForUser_Accepts_WhenUserHasMasterPassword_KdfAndSaltMatch()
        {
            var user = BuildValidUpdateUser();
            var data = BuildData(user);

            // Should not throw
            data.ValidateDataForUser(user);
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenUserHasNoMasterPassword()
        {
            var user = BuildValidUpdateUser();
            user.MasterPassword = null;
            var data = BuildData(user);

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenUserIsKeyConnector()
        {
            var user = BuildValidUpdateUser();
            user.UsesKeyConnector = true;
            var data = BuildData(user);

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenKdfChanged()
        {
            var user = BuildValidUpdateUser();
            var mismatchedKdf = new KdfSettings
            {
                KdfType = KdfType.Argon2id,
                Iterations = 3,
                Memory = 64,
                Parallelism = 4
            };
            var data = BuildData(user, kdfOverride: mismatchedKdf);

            Assert.Throws<ArgumentException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenSaltChanged()
        {
            var user = BuildValidUpdateUser();
            var data = BuildData(user, saltOverride: "wrong-salt");

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }
    }

    // --- Data model validation: UpdateExistingPasswordAndKdfData ---

    public class UpdateExistingPasswordAndKdfDataTests
    {
        private static User BuildValidUser()
        {
            return new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                MasterPassword = "existing-hash",
                Key = "existing-key",
                MasterPasswordSalt = "stored-salt",
                UsesKeyConnector = false,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = 600000
            };
        }

        private static UpdateExistingPasswordAndKdfData BuildData(User user, string? saltOverride = null)
        {
            var salt = saltOverride ?? user.GetMasterPasswordSalt();
            var newKdf = new KdfSettings
            {
                KdfType = KdfType.Argon2id,
                Iterations = 3,
                Memory = 64,
                Parallelism = 4
            };
            return new UpdateExistingPasswordAndKdfData
            {
                MasterPasswordUnlock = new MasterPasswordUnlockData
                {
                    Salt = salt,
                    MasterKeyWrappedUserKey = "wrapped-key",
                    Kdf = newKdf
                },
                MasterPasswordAuthentication = new MasterPasswordAuthenticationData
                {
                    Salt = salt,
                    MasterPasswordAuthenticationHash = "hash",
                    Kdf = newKdf
                }
            };
        }

        [Fact]
        public void ValidateDataForUser_Accepts_WhenUserHasMasterPassword_SaltMatch_KdfChanged()
        {
            var user = BuildValidUser();
            var data = BuildData(user);

            // Should not throw — KDF change is permitted here
            data.ValidateDataForUser(user);
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenUserHasNoMasterPassword()
        {
            var user = BuildValidUser();
            user.MasterPassword = null;
            var data = BuildData(user);

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenUserIsKeyConnector()
        {
            var user = BuildValidUser();
            user.UsesKeyConnector = true;
            var data = BuildData(user);

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }

        [Fact]
        public void ValidateDataForUser_Throws_WhenSaltChanged()
        {
            var user = BuildValidUser();
            var data = BuildData(user, saltOverride: "wrong-salt");

            Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        }
    }

}
