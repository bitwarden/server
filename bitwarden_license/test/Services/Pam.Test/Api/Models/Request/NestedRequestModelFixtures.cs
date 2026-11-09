using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Test.Api.Models.Request;

// PamValidationEndpointFilter walks any type in a Bit.Services.Pam.*.Api.Models.Request namespace, so these fixtures
// reach the collection and cycle branches that no shipped model does.

public class ParentWithChildrenRequestModel
{
    public List<ChildRequestModel> Children { get; set; } = [];
}

public class ChildRequestModel
{
    [Range(1, 10)]
    public int Value { get; set; }
}

public class CyclicRequestModel
{
    [Range(1, 10)]
    public int Value { get; set; }

    public CyclicRequestModel? Other { get; set; }
}

public class ParentWithNestedRequestModel
{
    public string Name { get; set; } = null!;

    public NestedRangeRequestModel? Nested { get; set; }
}

public class NestedRangeRequestModel : IValidatableObject
{
    [Range(1, 128)]
    public int MinLength { get; set; }

    [Range(1, 128)]
    public int MaxLength { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinLength > MaxLength)
        {
            yield return new ValidationResult(
                "MinLength must not be greater than MaxLength.", [nameof(MinLength), nameof(MaxLength)]);
        }
    }
}
