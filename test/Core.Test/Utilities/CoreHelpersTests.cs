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

        [Theory]
        [InlineData(2, 5, new[] { 1, 2, 3, 4, 5, 6, 7, 8 , 9, 0 })]
        [InlineData(2, 3, new[] { 1, 2, 3, 4, 5 })]
        [InlineData(2, 1, new[] { 1, 2 })]
        [InlineData(1, 1, new[] { 1 })]
        [InlineData(2, 2, new[] { 1, 2, 3 })]
        public void Batch_Success(int batchSize, int totalBatches, int[] collection)
        {
            // Arrange
            var remainder = collection.Length % batchSize;

            // Act
            var batches = collection.Batch(batchSize);

            // Assert
            Assert.Equal(totalBatches, batches.Count());

            foreach (var batch in batches.Take(totalBatches - 1))
            {
                Assert.Equal(batchSize, batch.Count());
            }

            Assert.Equal(batches.Last().Count(), remainder == 0 ? batchSize : remainder);
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
        [InlineData("\u00C6123abc\u00C7", "123ABC")]
        [InlineData("123\u00C6ABC", "123ABC")]
        [InlineData("\r\nHello", "E")]
        [InlineData("\tdef", "DEF")]
        [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUV1234567890", "ABCDEFABCDEF1234567890")]
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
        [InlineData(1, "1 Bytes")]
        [InlineData(-5L, "-5 Bytes")]
        [InlineData(1023L, "1023 Bytes")]
        [InlineData(1024L, "1 KB")]
        [InlineData(1025L, "1 KB")]
        [InlineData(-1023L, "-1023 Bytes")]
        [InlineData(-1024L, "-1 KB")]
        [InlineData(-1025L, "-1 KB")]
        [InlineData(1048575L, "1024 KB")]
        [InlineData(1048576L, "1 MB")]
        [InlineData(1048577L, "1 MB")]
        [InlineData(-1048575L, "-1024 KB")]
        [InlineData(-1048576L, "-1 MB")]
        [InlineData(-1048577L, "-1 MB")]
        [InlineData(1073741823L, "1024 MB")]
        [InlineData(1073741824L, "1 GB")]
        [InlineData(1073741825L, "1 GB")]
        [InlineData(-1073741823L, "-1024 MB")]
        [InlineData(-1073741824L, "-1 GB")]
        [InlineData(-1073741825L, "-1 GB")]
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
        public void ExtendQuery_AddNewParameter_Success()
        {
            // Arrange
            var uri = new Uri("https://bitwarden.com/?param1=value1");

            // Act
            var newUri = CoreHelpers.ExtendQuery(uri,
                new Dictionary<string, string> { { "param2", "value2" } });

            // Assert
            Assert.Equal("https://bitwarden.com/?param1=value1&param2=value2", newUri.ToString());
        }

        [Fact]
        public void ExtendQuery_AddTwoNewParameters_Success()
        {
            // Arrange
            var uri = new Uri("https://bitwarden.com/?param1=value1");

            // Act
            var newUri = CoreHelpers.ExtendQuery(uri,
                new Dictionary<string, string>
                {
                    { "param2", "value2" },
                    { "param3", "value3" }
                });

            // Assert
            Assert.Equal("https://bitwarden.com/?param1=value1&param2=value2&param3=value3", newUri.ToString());
        }

        [Fact]
        public void ExtendQuery_AddExistingParameter_Success()
        {
            // Arrange
            var uri = new Uri("https://bitwarden.com/?param1=value1&param2=value2");

            // Act
            var newUri = CoreHelpers.ExtendQuery(uri,
                new Dictionary<string, string> { { "param1", "test_value" } });

            // Assert
            Assert.Equal("https://bitwarden.com/?param1=test_value&param2=value2", newUri.ToString());
        }

        [Fact]
        public void ExtendQuery_AddNoParameters_Success()
        {
            // Arrange
            const string startingUri = "https://bitwarden.com/?param1=value1";

            var uri = new Uri(startingUri);

            // Act
            var newUri = CoreHelpers.ExtendQuery(uri, new Dictionary<string, string>());

            // Assert
            Assert.Equal(startingUri, newUri.ToString());
        }
    }
}
