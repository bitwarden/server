using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Core.Utilities;

public static class ModelStateExtensions
{
    public static string GetErrorMessage(this ModelStateDictionary modelState)
    {
        var errors = modelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        return string.Join("; ", errors);
    }
}
