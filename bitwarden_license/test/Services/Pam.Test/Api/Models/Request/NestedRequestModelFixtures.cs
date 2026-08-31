using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Test.Api.Models.Request;

// PamValidationEndpointFilter treats any type in a Bit.Services.Pam.*.Api.Models.Request namespace as a request
// model to walk into, so these fixtures reach the collection and cycle branches that no shipped PAM model does yet.

/// <summary>Holds a collection of nested request models.</summary>
public class ParentWithChildrenRequestModel
{
    public List<ChildRequestModel> Children { get; set; } = [];
}

/// <summary>A nested element carrying a constraint the filter's element walk has to run.</summary>
public class ChildRequestModel
{
    [Range(1, 10)]
    public int Value { get; set; }
}

/// <summary>Reachable from itself, so the walk only terminates via the filter's cycle guard.</summary>
public class CyclicRequestModel
{
    [Range(1, 10)]
    public int Value { get; set; }

    public CyclicRequestModel? Other { get; set; }
}
