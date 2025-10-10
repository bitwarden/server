#nullable enable

using Bit.Api.KeyManagement.Models.Requests;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Models.Request;

public class SignatureKeyPairRequestModelTests
{
    [Fact]
    public void ToSignatureKeyPairData_WrongAlgorithm_Rejects()
    {
        var model = new SignatureKeyPairRequestModel
        {
            SignatureAlgorithm = "abc",
            WrappedSigningKey = "wrappedKey",
            VerifyingKey = "verifyingKey"
        };

        Assert.Throws<ArgumentException>(() => model.ToSignatureKeyPairData());
    }
}
