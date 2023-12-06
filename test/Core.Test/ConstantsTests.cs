using Xunit;

namespace Bit.Core.Test;

public class ConstantsTests
{
    public class RangeConstantTests
    {
        [Fact]
        public void Constructor_WithValidValues_SetsProperties()
        {
            // Arrange
            const int min = 0;
            const int max = 10;
            const int defaultValue = 5;

            // Act
            var rangeConstant = new RangeConstant(min, max, defaultValue);

            // Assert
            Assert.Equal(min, rangeConstant.Min);
            Assert.Equal(max, rangeConstant.Max);
            Assert.Equal(defaultValue, rangeConstant.Default);
        }

        [Fact]
        public void Constructor_WithInvalidValues_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RangeConstant(10, 0, 5));
        }

        [Fact]
        public void Constructor_WithDefaultValueOutsideRange_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RangeConstant(0, 10, 20));
        }

        [Theory]
        [InlineData(5)]
        [InlineData(0)]
        [InlineData(10)]
        public void InsideRange_WithValidValues_ReturnsTrue(int number)
        {
            // Arrange
            var rangeConstant = new RangeConstant(0, 10, 5);

            // Act
            bool result = rangeConstant.InsideRange(number);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(11)]
        public void InsideRange_WithInvalidValues_ReturnsFalse(int number)
        {
            // Arrange
            var rangeConstant = new RangeConstant(0, 10, 5);

            // Act
            bool result = rangeConstant.InsideRange(number);

            // Assert
            Assert.False(result);
        }
    }
}
