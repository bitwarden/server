using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities
{
    public class CoreHelpersTests
    {
        [Fact]
        public void GenerateComb_Success()
        {
            // Arrange & Act
            var comb = CoreHelpers.GenerateComb();

            // Assert
            Assert.NotEqual(Guid.Empty, comb);
            // TODO: Add more asserts to make sure important aspects of
            // the comb are working properly
        }

        // TODO: Probably make this a Theory with some more possibilties.
        [Fact]
        public void Batch_Success()
        {
            // Arrange
            var source = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            // Act
            var batches = source.Batch(2);

            // Assert
            Assert.Equal(5, batches.Count());
            Assert.All(batches,
                collection => Assert.Equal(2, collection.Count()));
        }

        [Fact]
        public void ToGuidIdArrayTVP_Success()
        {
            // Arrange
            var item0 = Guid.NewGuid();
            var item1 = Guid.NewGuid();

            var ids = new[] { item0, item1 };

            // Act
            var dt = ids.ToGuidIdArrayTVP();

            // Assert
            Assert.Single(dt.Columns);
            Assert.Equal("GuidId", dt.Columns[0].ColumnName);
            Assert.Equal(2, dt.Rows.Count);
            Assert.Equal(item0, dt.Rows[0][0]);
            Assert.Equal(item1, dt.Rows[1][0]);
        }

        // TODO: Test the other ToArrayTVP Methods

        [Theory]
        [InlineData("12345&6789", "123456789")]
        [InlineData("abcdef", "ABCDEF")]
        [InlineData("1!@#$%&*()_+", "1")]
        public void CleanCertificateThumbprint_Success(string input, string output)
        {
            // Arrange & Act
            var sanitizedInput = CoreHelpers.CleanCertificateThumbprint(input);

            // Assert
            Assert.Equal(output, sanitizedInput);
        }

        // TODO: Add more tests
        [Theory]
        [InlineData("2020-12-30T11:49:12.0000000Z", 1609310952000L)]
        public void ToEpocMilliseconds_Success(string dateString, long milliseconds)
        {
            // Arrange
            var date = DateTime.Parse(dateString);

            // Act & Assert
            Assert.Equal(milliseconds, CoreHelpers.ToEpocMilliseconds(date));
        }

        [Theory]
        [InlineData(1609310952000L, "2020-12-30T11:49:12.0000000Z")]
        public void FromEpocMilliseconds(long milliseconds, string dateString)
        {
            // Arrange
            var date = DateTime.Parse(dateString);

            // Act & Assert
            Assert.Equal(date, CoreHelpers.FromEpocMilliseconds(milliseconds));
        }

        [Fact]
        public void SecureRandomString_Success()
        {
            // Arrange & Act
            var @string = CoreHelpers.SecureRandomString(8);

            // Assert
            // TODO: Should probably add more Asserts down the line
            Assert.Equal(8, @string.Length);
        }

        [Theory]
        // [InlineData(long.MinValue, "8589934592 GB")]
        // [InlineData(long.MinValue + 1, "Change")]
        [InlineData(1000L, "1000 Bytes")]
        [InlineData(1, "1 Bytes")]
        [InlineData(-5L, "5 Bytes")]
        [InlineData(long.MaxValue, "8589934592 GB")]
        public void ReadableBytesSize_Success(long size, string readable)
        {
            // Act & Assert
            Assert.Equal(readable, CoreHelpers.ReadableBytesSize(size));
        }

        [Fact]
        public void CloneObject_Success()
        {
            var orignial = new { Message = "Message" };

            var copy = CoreHelpers.CloneObject(orignial);

            Assert.Equal(orignial.Message, copy.Message);
        }

        [Fact]
        public void ExtendQuery_Success()
        {
            // Arrange
            var uri = new Uri("https://bitwarden.com/?param1=value1");

            // Act
            var newUri = CoreHelpers.ExtendQuery(uri,
                new Dictionary<string, string> { { "param2", "value2" } });

            // Assert
            Assert.Equal("https://bitwarden.com/?param1=value1&param2=value2", newUri.ToString());
        }

    }
}
