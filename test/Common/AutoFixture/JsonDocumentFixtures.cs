using System.Text.Json;
using AutoFixture;
using AutoFixture.Kernel;

namespace Bit.Test.Common.AutoFixture.JsonDocumentFixtures;

public class JsonDocumentCustomization : ICustomization, ISpecimenBuilder
{
    public string Json { get; set; }

    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(this);
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        var type = request as Type;
        if (type == null || (type != typeof(JsonDocument)))
        {
            return new NoSpecimen();
        }

        return JsonDocument.Parse(Json ?? "{}");
    }
}
