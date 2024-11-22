using Microsoft.Extensions.Configuration;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest;

[Obsolete("[DatabaseTheory] is no longer needed, you can just use [Theory]")]
public class DatabaseTheoryAttribute : TheoryAttribute
{
    public DatabaseTheoryAttribute()
    {

    }
}
