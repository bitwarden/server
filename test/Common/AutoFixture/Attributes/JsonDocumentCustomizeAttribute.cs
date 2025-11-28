// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoFixture;
using Bit.Test.Common.AutoFixture.JsonDocumentFixtures;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class JsonDocumentCustomizeAttribute : BitCustomizeAttribute
{
    public string Json { get; set; }
    public override ICustomization GetCustomization() => new JsonDocumentCustomization() { Json = Json };
}
