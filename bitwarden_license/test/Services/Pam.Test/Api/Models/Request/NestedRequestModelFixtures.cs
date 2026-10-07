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
