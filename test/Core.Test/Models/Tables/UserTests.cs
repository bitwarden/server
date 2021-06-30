using Bit.Core.Models.Table;
using Xunit;

namespace Bit.Core.Test.Models.Tables
{
    public class UserTests
    {
        //                              KB     MB     GB
        public const long Multiplier = 1024 * 1024 * 1024;

        [Fact]
        public void StorageBytesRemaining_HasMax_DoesNotHaveStorage_ReturnsMaxAsBytes()
        {
            short maxStorageGb = 1;

            var user = new User
            {
                MaxStorageGb = maxStorageGb,
                Storage = null,
            };

            var bytesRemaining = user.StorageBytesRemaining();

            Assert.Equal(bytesRemaining, maxStorageGb * Multiplier);
        }

        [Theory]
        [InlineData(2, 1 * Multiplier, 1 * Multiplier)]

        public void StorageBytesRemaining_HasMax_HasStorage_ReturnRemainingStorage(short maxStorageGb, long storageBytes, long expectedRemainingBytes)
        {
            var user = new User
            {
                MaxStorageGb = maxStorageGb,
                Storage = storageBytes,
            };

            var bytesRemaining = user.StorageBytesRemaining();

            Assert.Equal(expectedRemainingBytes, bytesRemaining);
        }
    }
}