using Bit.Core.Entities;
using Bit.Core.KeyManagement.Entities;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Bit.Test.Common.Constants;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Queries;

public class IsV2EncryptionUserQueryTests
{
    private class FakeUserSignatureKeyPairRepository : IUserSignatureKeyPairRepository
    {
        private readonly bool _hasKeys;
        public FakeUserSignatureKeyPairRepository(bool hasKeys) { _hasKeys = hasKeys; }
        public Task<SignatureKeyPairData?> GetByUserIdAsync(Guid userId)
            => Task.FromResult(_hasKeys ? new SignatureKeyPairData(SignatureAlgorithm.Ed25519, TestEncryptionConstants.V2WrappedSigningKey, TestEncryptionConstants.V2VerifyingKey) : null);

        // Unused in tests
        public Task<IEnumerable<UserSignatureKeyPair>> GetManyAsync(IEnumerable<Guid> ids) => throw new NotImplementedException();
        public Task<UserSignatureKeyPair> GetByIdAsync(Guid id) => throw new NotImplementedException();
        public Task<UserSignatureKeyPair> CreateAsync(UserSignatureKeyPair obj) => throw new NotImplementedException();
        public Task ReplaceAsync(UserSignatureKeyPair obj) => throw new NotImplementedException();
        public Task UpsertAsync(UserSignatureKeyPair obj) => throw new NotImplementedException();
        public Task DeleteAsync(UserSignatureKeyPair obj) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id) => throw new NotImplementedException();
        public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId, SignatureKeyPairData signatureKeyPair) => throw new NotImplementedException();
        public UpdateEncryptedDataForKeyRotation SetUserSignatureKeyPair(Guid userId, SignatureKeyPairData signatureKeyPair) => throw new NotImplementedException();
    }

    [Fact]
    public async Task Run_ReturnsTrue_ForV2State()
    {
        var user = new User { Id = Guid.NewGuid(), PrivateKey = TestEncryptionConstants.V2PrivateKey };
        var sut = new IsV2EncryptionUserQuery(new FakeUserSignatureKeyPairRepository(true));

        var result = await sut.Run(user);

        Assert.True(result);
    }

    [Fact]
    public async Task Run_ReturnsFalse_ForV1State()
    {
        var user = new User { Id = Guid.NewGuid(), PrivateKey = TestEncryptionConstants.V1EncryptedBase64 };
        var sut = new IsV2EncryptionUserQuery(new FakeUserSignatureKeyPairRepository(false));

        var result = await sut.Run(user);

        Assert.False(result);
    }

    [Fact]
    public async Task Run_ThrowsForInvalidMixedState()
    {
        var user = new User { Id = Guid.NewGuid(), PrivateKey = TestEncryptionConstants.V2PrivateKey };
        var sut = new IsV2EncryptionUserQuery(new FakeUserSignatureKeyPairRepository(false));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.Run(user));
    }
}
