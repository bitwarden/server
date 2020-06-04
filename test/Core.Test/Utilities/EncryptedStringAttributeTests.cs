using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities
{
    public class EncryptedStringAttributeTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("aXY=|Y3Q=")] // Valid AesCbc256_B64
        [InlineData("aXY=|Y3Q=|cnNhQ3Q=")] // Valid AesCbc128_HmacSha256_B64
        [InlineData("Rsa2048_OaepSha256_B64.cnNhQ3Q=")]
        public void IsValid_ReturnsTrue_WhenValid(string input)
        {
            var sut = new EncryptedStringAttribute();

            var actual = sut.IsValid(input);
            
            Assert.True(actual);
        }
        
        [Theory]
        [InlineData("")]
        [InlineData(".")]
        [InlineData("|")]
        [InlineData("!|!")] // Invalid base 64
        [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.1")] // Invalid length
        [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.|")] // Empty iv & ct
        [InlineData("AesCbc128_HmacSha256_B64.1")] // Invalid length
        [InlineData("AesCbc128_HmacSha256_B64.aXY=|Y3Q=|")] // Empty mac
        [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.aXY=|Y3Q=|")] // Empty mac
        [InlineData("Rsa2048_OaepSha256_B64.1|2")] // Invalid length
        [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.aXY=|")] // Empty mac
        public void IsValid_ReturnsFalse_WhenInvalid(string input)
        {
            var sut = new EncryptedStringAttribute();

            var actual = sut.IsValid(input);
            
            Assert.False(actual);
        }
    }
}
