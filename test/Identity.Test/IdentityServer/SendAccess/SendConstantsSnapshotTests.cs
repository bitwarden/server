using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.SendAccess;

/// <summary>
/// Snapshot tests to ensure the string constants in <see cref="SendAccessConstants"/> do not change unintentionally.
/// If you change any of these values, please ensure you understand the impact and update the SDK accordingly.
/// If you intentionally change any of these values, please update the tests to reflect the new expected values.
/// </summary>
public class SendConstantsSnapshotTests
{
    [Fact]
    public void SendAccessError_Constant_HasCorrectValue()
    {
        // Assert
        Assert.Equal("send_access_error_type", SendAccessConstants.SendAccessError);
    }

    [Fact]
    public void TokenRequest_Constants_HaveCorrectValues()
    {
        // Assert
        Assert.Equal("send_id", SendAccessConstants.TokenRequest.SendId);
        Assert.Equal("password_hash_b64", SendAccessConstants.TokenRequest.ClientB64HashedPassword);
        Assert.Equal("email", SendAccessConstants.TokenRequest.Email);
        Assert.Equal("otp", SendAccessConstants.TokenRequest.Otp);
    }

    [Fact]
    public void GrantValidatorResults_Constants_HaveCorrectValues()
    {
        // Assert
        Assert.Equal("valid_send_guid", SendAccessConstants.GrantValidatorResults.ValidGuid);
        Assert.Equal("send_id_required", SendAccessConstants.GrantValidatorResults.SendIdRequired);
        Assert.Equal("send_id_invalid", SendAccessConstants.GrantValidatorResults.InvalidSendId);
    }

    [Fact]
    public void PasswordValidatorResults_Constants_HaveCorrectValues()
    {
        // Assert
        Assert.Equal("password_hash_b64_invalid", SendAccessConstants.PasswordValidatorResults.RequestPasswordDoesNotMatch);
        Assert.Equal("password_hash_b64_required", SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired);
    }

    [Fact]
    public void EmailOtpValidatorResults_Constants_HaveCorrectValues()
    {
        // Assert
        Assert.Equal("email_invalid", SendAccessConstants.EmailOtpValidatorResults.EmailInvalid);
        Assert.Equal("email_required", SendAccessConstants.EmailOtpValidatorResults.EmailRequired);
        Assert.Equal("email_and_otp_required_otp_sent", SendAccessConstants.EmailOtpValidatorResults.EmailOtpSent);
        Assert.Equal("otp_invalid", SendAccessConstants.EmailOtpValidatorResults.EmailOtpInvalid);
        Assert.Equal("otp_generation_failed", SendAccessConstants.EmailOtpValidatorResults.OtpGenerationFailed);
    }

    [Fact]
    public void OtpToken_Constants_HaveCorrectValues()
    {
        // Assert
        Assert.Equal("send_access", SendAccessConstants.OtpToken.TokenProviderName);
        Assert.Equal("email_otp", SendAccessConstants.OtpToken.Purpose);
        Assert.Equal("{0}_{1}", SendAccessConstants.OtpToken.TokenUniqueIdentifier);
    }

    [Fact]
    public void OtpEmail_Constants_HaveCorrectValues()
    {
        // Assert
        Assert.Equal("Your Bitwarden Send verification code is {0}", SendAccessConstants.OtpEmail.Subject);
    }
}
