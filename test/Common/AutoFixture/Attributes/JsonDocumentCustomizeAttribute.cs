using AutoFixture;
using Bit.Test.Common.AutoFixture.JsonDocumentFixtures;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class JsonDocumentCustomizeAttribute : BitCustomizeAttribute
{
    public string Json { get; set; }
    public override ICustomization GetCustomization() => new JsonDocumentCustomization() { Json = Json };
}
