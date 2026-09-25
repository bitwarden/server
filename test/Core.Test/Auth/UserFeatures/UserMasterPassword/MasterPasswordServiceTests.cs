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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

[SutProviderCustomize]
public class MasterPasswordServiceTests
{
    // MasterPasswordService is internal; NSubstitute cannot proxy ILogger<MasterPasswordService>
    // from the DynamicProxyGenAssembly2 runtime assembly without InternalsVisibleTo. Provide
    // NullLogger explicitly so Castle never needs to generate a proxy for the type parameter.
    private static SutProvider<MasterPasswordService> CreateSutProvider()
        => new SutProvider<MasterPasswordService>()
            .WithFakeTimeProvider()
            .SetDependency<ILogger<MasterPasswordService>>(NullLogger<MasterPasswordService>.Instance)
            .Create();

    private static SutProvider<MasterPasswordService> CreateSutProviderWithValidator(
        IPasswordValidator<User> validator)
        => new SutProvider<MasterPasswordService>()
            .WithFakeTimeProvider()
            .SetDependency<ILogger<MasterPasswordService>>(NullLogger<MasterPasswordService>.Instance)
            .SetDependency<IEnumerable<IPasswordValidator<User>>>(new[] { validator })
            .Create();

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
        bool validatePassword = false, bool refreshStamp = false)
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
            RefreshStamp = refreshStamp
        };
    }

    private static UpdateExistingPasswordData BuildUpdateExistingPasswordData(User user, string? hint = null,
        bool validatePassword = false, bool refreshStamp = false)
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
            RefreshStamp = refreshStamp
        };
    }

    private static UpdateExistingKdfConfigurationData BuildUpdateExistingKdfConfigurationData(User user,
        KdfSettings? newKdf = null, string? hint = null, bool validatePassword = false, bool refreshStamp = false)
    {
        var salt = user.GetMasterPasswordSalt();
        var kdf = newKdf ?? new KdfSettings
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4
        };
        return new UpdateExistingKdfConfigurationData
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
            RefreshStamp = refreshStamp
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
        Assert.Equal(data.MasterPasswordUnlock.Kdf.Memory, user.KdfMemory);
        Assert.Equal(data.MasterPasswordUnlock.Kdf.Parallelism, user.KdfParallelism);
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
    public async Task PrepareSetInitialMasterPassword_NullHint_OverwritesExistingHint(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;
        user.MasterPasswordHint = "existing-hint";

        var data = BuildSetInitialData(user, hint: null);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        var result = await sutProvider.Sut.PrepareSetInitialMasterPasswordAsync(user, data);

        Assert.True(result.IsT0);
        Assert.Null(result.AsT0.MasterPasswordHint);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrepareSetInitialMasterPassword_SecurityStampRotation_HonorsRefreshStampFlag(bool refreshStamp)
    {
        var sutProvider = CreateSutProvider();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            MasterPassword = null,
            Key = null,
            MasterPasswordSalt = null,
            UsesKeyConnector = false,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000,
            SecurityStamp = "original-stamp"
        };
        var data = BuildSetInitialData(user, refreshStamp: refreshStamp);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        await sutProvider.Sut.PrepareSetInitialMasterPasswordAsync(user, data);

        Assert.Equal(refreshStamp, user.SecurityStamp != "original-stamp");
    }

    [Theory, BitAutoData]
    public async Task PrepareSetInitialMasterPassword_ValidationFailure_ReturnsErrorsAsArrayOfIdentityError(User user)
    {
        var error = new IdentityError { Code = "test", Description = "test error" };
        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(error));

        var sutProvider = CreateSutProviderWithValidator(validator);

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

        var sutProvider = CreateSutProviderWithValidator(validator);

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

        var data = BuildUpdateExistingPasswordData(user);
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

        var data = BuildUpdateExistingPasswordData(user, hint: hint);
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

        var data = BuildUpdateExistingPasswordData(user);

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

        var sutProvider = CreateSutProviderWithValidator(validator);

        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingPasswordData(user, validatePassword: true);

        var result = await sutProvider.Sut.PrepareUpdateExistingMasterPasswordAsync(user, data);

        Assert.True(result.IsT1);
        Assert.NotEmpty(result.AsT1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrepareUpdateExistingMasterPassword_SecurityStampRotation_HonorsRefreshStampFlag(bool refreshStamp)
    {
        var sutProvider = CreateSutProvider();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            MasterPassword = "existing-hash",
            MasterPasswordSalt = "stored-salt",
            UsesKeyConnector = false,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000,
            SecurityStamp = "original-stamp"
        };
        var data = BuildUpdateExistingPasswordData(user, refreshStamp: refreshStamp);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        await sutProvider.Sut.PrepareUpdateExistingMasterPasswordAsync(user, data);

        Assert.Equal(refreshStamp, user.SecurityStamp != "original-stamp");
    }

    [Theory]
    [InlineData(600_000)] // current minimum
    [InlineData(100_000)] // legacy — below the current 600k minimum; must not be blocked
    public async Task PrepareUpdateExistingMasterPassword_SucceedsRegardlessOfPbkdf2IterationCount(int kdfIterations)
    {
        var sutProvider = CreateSutProvider();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            MasterPassword = "existing-hash",
            MasterPasswordSalt = "stored-salt",
            UsesKeyConnector = false,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = kdfIterations
        };
        var data = BuildUpdateExistingPasswordData(user);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("new-hash");

        var result = await sutProvider.Sut.PrepareUpdateExistingMasterPasswordAsync(user, data);

        // Password change must succeed for any existing iteration count — legacy users
        // below the current 600k minimum must not be locked out of changing their password.
        Assert.True(result.IsT0);
        // KDF must remain unchanged — this path updates the hash only, never the KDF.
        Assert.Equal(kdfIterations, user.KdfIterations);
    }

    // --- SaveUpdateExistingMasterPassword ---

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPassword_PreparesAndPersists(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingPasswordData(user);
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

        var sutProvider = CreateSutProviderWithValidator(validator);

        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingPasswordData(user, validatePassword: true);

        var result = await sutProvider.Sut.SaveUpdateExistingMasterPasswordAsync(user, data);

        Assert.True(result.IsT1);
        Assert.NotEmpty(result.AsT1);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingMasterPassword_ThrowsWhenUserNotHydrated(User user)
    {
        var sutProvider = CreateSutProvider();
        user.Id = default;
        user.MasterPassword = "existing-hash";

        var data = BuildUpdateExistingPasswordData(user);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.SaveUpdateExistingMasterPasswordAsync(user, data));
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

        var sutProvider = CreateSutProviderWithValidator(validator);

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
    public async Task PrepareSetInitialOrUpdateExisting_PropagatesValidationErrors_WhenUpdateExistingPathFails(User user)
    {
        var error = new IdentityError { Code = "pwd-invalid", Description = "Password is too weak." };
        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(error));

        var sutProvider = CreateSutProviderWithValidator(validator);

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

    // --- SaveUpdateExistingKdfConfiguration ---

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingKdfConfiguration_Success(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingKdfConfigurationData(user, hint: "test-hint");
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("new-hash");
        var originalLastPasswordChangeDate = user.LastPasswordChangeDate;

        var result = await sutProvider.Sut.SaveUpdateExistingKdfConfigurationAsync(user, data);

        var expectedTime = sutProvider.GetDependency<TimeProvider>().GetUtcNow().UtcDateTime;

        Assert.True(result.IsT0);
        Assert.Equal(data.MasterPasswordUnlock.MasterKeyWrappedUserKey, user.Key);
        Assert.Equal(data.MasterPasswordUnlock.Kdf.KdfType, user.Kdf);
        Assert.Equal(data.MasterPasswordUnlock.Kdf.Iterations, user.KdfIterations);
        Assert.Equal("test-hint", user.MasterPasswordHint);
        Assert.Equal(expectedTime, user.LastKdfChangeDate);
        // LastPasswordChangeDate marks a user's action to change the password;
        // the fact that the hash-of-hash which is stored in the database changes
        // as a result of KDF update does not affect a change to this date;
        // doing so would confuse the data. LastKdfChangeDate neatly separates
        // this concern.
        Assert.Equal(originalLastPasswordChangeDate, user.LastPasswordChangeDate);
        Assert.Equal(expectedTime, user.RevisionDate);
        Assert.Equal(user.RevisionDate, user.AccountRevisionDate);
        await sutProvider.GetDependency<IUserRepository>().Received().ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingKdfConfiguration_RotatesPbkdf2ToArgon2id(User user)
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
        var data = BuildUpdateExistingKdfConfigurationData(user, newKdf: newKdf);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("new-hash");

        var result = await sutProvider.Sut.SaveUpdateExistingKdfConfigurationAsync(user, data);

        Assert.True(result.IsT0);
        Assert.Equal(KdfType.Argon2id, user.Kdf);
        Assert.Equal(3, user.KdfIterations);
        Assert.Equal(64, user.KdfMemory);
        Assert.Equal(4, user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingKdfConfiguration_RotatesArgon2idToPbkdf2(User user)
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
        var data = BuildUpdateExistingKdfConfigurationData(user, newKdf: newKdf);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("new-hash");

        var result = await sutProvider.Sut.SaveUpdateExistingKdfConfigurationAsync(user, data);

        Assert.True(result.IsT0);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(600000, user.KdfIterations);
        Assert.Null(user.KdfMemory);
        Assert.Null(user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingKdfConfiguration_WhenValidationFails_ReturnsErrorsAndDoesNotPersist(User user)
    {
        var error = new IdentityError { Code = "pwd-invalid", Description = "Password is too weak." };
        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(Arg.Any<UserManager<User>>(), Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(error));

        var sutProvider = CreateSutProviderWithValidator(validator);

        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;

        var data = BuildUpdateExistingKdfConfigurationData(user, validatePassword: true);

        var result = await sutProvider.Sut.SaveUpdateExistingKdfConfigurationAsync(user, data);

        Assert.True(result.IsT1);
        Assert.NotEmpty(result.AsT1);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingKdfConfiguration_ThrowsWhenSaltChanged(User user)
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
        var data = new UpdateExistingKdfConfigurationData
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
            () => sutProvider.Sut.SaveUpdateExistingKdfConfigurationAsync(user, data));
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingKdfConfiguration_ThrowsWhenNoExistingPassword(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;

        var data = BuildUpdateExistingKdfConfigurationData(user);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUpdateExistingKdfConfigurationAsync(user, data));
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingKdfConfiguration_ThrowsWhenUserNotHydrated(User user)
    {
        var sutProvider = CreateSutProvider();
        user.Id = default;
        user.MasterPassword = "existing-hash";

        var data = BuildUpdateExistingKdfConfigurationData(user);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.SaveUpdateExistingKdfConfigurationAsync(user, data));
    }

    [Theory, BitAutoData]
    public async Task SaveUpdateExistingKdfConfiguration_ThrowsForKeyConnectorUser(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = true;

        var data = BuildUpdateExistingKdfConfigurationData(user);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUpdateExistingKdfConfigurationAsync(user, data));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveUpdateExistingKdfConfiguration_SecurityStampRotation_HonorsRefreshStampFlag(bool refreshStamp)
    {
        var sutProvider = CreateSutProvider();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            MasterPassword = "existing-hash",
            MasterPasswordSalt = "stored-salt",
            UsesKeyConnector = false,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000,
            SecurityStamp = "original-stamp"
        };
        var data = BuildUpdateExistingKdfConfigurationData(user, refreshStamp: refreshStamp);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hash");

        await sutProvider.Sut.SaveUpdateExistingKdfConfigurationAsync(user, data);

        Assert.Equal(refreshStamp, user.SecurityStamp != "original-stamp");
    }

    // --- BuildUpdateUserDelegateSetInitialMasterPassword ---

    public class BuildUpdateUserDelegateSetInitialMasterPasswordTests
    {
        [Theory, BitAutoData]
        public void BuildUpdateUserDelegate_ThrowsWhenUserNotHydrated(User user)
        {
            var sutProvider = CreateSutProvider();
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
        public async Task BuildUpdateUserDelegate_HappyPath_ReturnsNonNullDelegateAndDoesNotPersist(User user)
        {
            var sutProvider = CreateSutProvider();
            user.MasterPassword = null;
            user.Key = null;
            user.MasterPasswordSalt = null;
            user.UsesKeyConnector = false;

            sutProvider.GetDependency<IPasswordHasher<User>>()
                .HashPassword(Arg.Any<User>(), Arg.Any<string>())
                .Returns("hashed");

            UpdateUserData noopWrite = (_, _) => Task.CompletedTask;
            sutProvider.GetDependency<IUserRepository>()
                .SetMasterPassword(Arg.Any<Guid>(), Arg.Any<MasterPasswordUnlockData>(),
                    Arg.Any<string>(), Arg.Any<string?>())
                .Returns(noopWrite);

            var data = BuildSetInitialDataForUser(user);

            var write = sutProvider.Sut.BuildUpdateUserDelegateSetInitialMasterPassword(user, data);

            // The Build* tier returns a delegate — it must not persist directly.
            Assert.NotNull(write);
            await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());

            // Invoking the delegate must write via SetMasterPassword, not ReplaceAsync.
            await write(null, null);
            sutProvider.GetDependency<IUserRepository>().Received()
                .SetMasterPassword(user.Id, data.MasterPasswordUnlock, "hashed", data.MasterPasswordHint);
        }

        [Theory, BitAutoData]
        public void BuildUpdateUserDelegate_ThrowsWhenUserAlreadyHasMasterPassword(User user)
        {
            var sutProvider = CreateSutProvider();
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

            var sutProvider = CreateSutProviderWithValidator(validator);

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

            var sutProvider = CreateSutProviderWithValidator(validator);

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
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BuildUpdateUserDelegate_WhenRefreshStamp_SecurityStampRotation_HonorsFlag(bool refreshStamp)
        {
            var sutProvider = CreateSutProvider();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                MasterPassword = null,
                Key = null,
                MasterPasswordSalt = null,
                UsesKeyConnector = false,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = 600000,
                SecurityStamp = "original-stamp"
            };

            UpdateUserData noopWrite = (_, _) => Task.CompletedTask;
            sutProvider.GetDependency<IUserRepository>()
                .SetMasterPassword(Arg.Any<Guid>(), Arg.Any<MasterPasswordUnlockData>(),
                    Arg.Any<string>(), Arg.Any<string?>())
                .Returns(noopWrite);

            var data = BuildSetInitialDataForUser(user);
            data.RefreshStamp = refreshStamp;

            var write = sutProvider.Sut.BuildUpdateUserDelegateSetInitialMasterPassword(user, data);
            await write(null, null);

            Assert.Equal(refreshStamp, user.SecurityStamp != "original-stamp");
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

    // --- PrepareClearMasterPassword ---

    [Theory, BitAutoData]
    public void PrepareClearMasterPassword_HydratedUser_ClearsCredentialAndHintAndUpdatesRevisionDates(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.MasterPasswordSalt = "existing-salt";
        user.MasterPasswordHint = "existing-hint";
        var originalLastPasswordChangeDate = user.LastPasswordChangeDate;

        var result = sutProvider.Sut.PrepareClearMasterPassword(user);

        var expectedTime = sutProvider.GetDependency<TimeProvider>().GetUtcNow().UtcDateTime;

        Assert.Same(user, result);
        Assert.Null(user.MasterPassword);
        Assert.Null(user.MasterPasswordSalt);
        Assert.Null(user.MasterPasswordHint);
        Assert.Equal(expectedTime, user.RevisionDate);
        Assert.Equal(expectedTime, user.AccountRevisionDate);
        Assert.Equal(originalLastPasswordChangeDate, user.LastPasswordChangeDate);
    }

    [Theory, BitAutoData]
    public void PrepareClearMasterPassword_ThrowsWhenUserNotHydrated(User user)
    {
        var sutProvider = CreateSutProvider();
        user.Id = default;

        Assert.Throws<ArgumentException>(
            () => sutProvider.Sut.PrepareClearMasterPassword(user));
    }

    /// <summary>
    /// The Key Connector conversion flow depends on these surviving — the caller may re-wrap
    /// <see cref="User.Key"/> and must not lose the other user state when the credential is cleared.
    /// </summary>
    [Theory, BitAutoData]
    public void PrepareClearMasterPassword_PreservesUnrelatedUserState(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.MasterPasswordSalt = "existing-salt";
        user.Key = "wrapped-user-key";
        user.Kdf = KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;

        sutProvider.Sut.PrepareClearMasterPassword(user);

        Assert.Equal("wrapped-user-key", user.Key);
        Assert.Equal(KdfType.Argon2id, user.Kdf);
        Assert.Equal(3, user.KdfIterations);
        Assert.Equal(64, user.KdfMemory);
        Assert.Equal(4, user.KdfParallelism);
    }

    /// <summary>
    /// Unlike every other write path in this service, <c>PrepareClearMasterPassword</c> takes no
    /// <c>RefreshStamp</c> flag — Key Connector conversion preserves the user-key capability so
    /// sessions stay valid. Locking this in stops a future contributor from quietly adding
    /// rotation without revisiting the contract.
    /// </summary>
    [Theory, BitAutoData]
    public void PrepareClearMasterPassword_DoesNotRotateSecurityStamp(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.MasterPasswordSalt = "existing-salt";
        var originalStamp = user.SecurityStamp;

        sutProvider.Sut.PrepareClearMasterPassword(user);

        Assert.Equal(originalStamp, user.SecurityStamp);
    }

    /// <summary>
    /// The XML doc claims no constraints on the user's current master password state. Calling
    /// on an already-cleared user must succeed and still bump revision dates.
    /// </summary>
    [Theory, BitAutoData]
    public void PrepareClearMasterPassword_AlreadyCleared_SucceedsAndUpdatesRevisionDates(User user)
    {
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.MasterPasswordSalt = null;
        user.MasterPasswordHint = null;

        var result = sutProvider.Sut.PrepareClearMasterPassword(user);

        var expectedTime = sutProvider.GetDependency<TimeProvider>().GetUtcNow().UtcDateTime;

        Assert.Same(user, result);
        Assert.Null(user.MasterPassword);
        Assert.Null(user.MasterPasswordSalt);
        Assert.Null(user.MasterPasswordHint);
        Assert.Equal(expectedTime, user.RevisionDate);
        Assert.Equal(expectedTime, user.AccountRevisionDate);
    }
}
