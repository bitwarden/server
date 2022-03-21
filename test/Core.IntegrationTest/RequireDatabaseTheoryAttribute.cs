using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.IntegrationTest
{
    public class RequireDatabaseTheoryAttribute : RequiredEnvironmentTheoryAttribute
    {
        public RequireDatabaseTheoryAttribute()
            : base("INTTEST_CONNSTR", "INTTEST_PROVIDER")
        {
            
        }
    }
}
