using Xunit;

namespace Bit.Infrastructure.IntegrationTest;

[Obsolete("This attribute is no longer needed and can be replaced with a [Theory]")]
public class DatabaseTheoryAttribute : TheoryAttribute
{
    public DatabaseTheoryAttribute()
    {

    }
}
