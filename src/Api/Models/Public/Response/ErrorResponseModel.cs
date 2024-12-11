using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Api.Models.Public.Response;

public class ErrorResponseModel : IResponseModel
{
    public ErrorResponseModel(string message)
    {
        Message = message;
    }

    public ErrorResponseModel(ModelStateDictionary modelState)
    {
        Message = "The request's model state is invalid.";
        Errors = new Dictionary<string, IEnumerable<string>>();

        var keys = modelState.Keys.ToList();
        var values = modelState.Values.ToList();

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (keys.Count <= i)
            {
                // Keys not available for some reason.
                break;
            }

            var key = keys[i];
            if (value.ValidationState != ModelValidationState.Invalid || value.Errors.Count == 0)
            {
                continue;
            }

            var errors = value.Errors.Select(e => e.ErrorMessage);
            Errors.Add(key, errors);
        }
    }

    public ErrorResponseModel(Dictionary<string, IEnumerable<string>> errors)
        : this("Errors have occurred.", errors) { }

    public ErrorResponseModel(string errorKey, string errorValue)
        : this(errorKey, new string[] { errorValue }) { }

    public ErrorResponseModel(string errorKey, IEnumerable<string> errorValues)
        : this(new Dictionary<string, IEnumerable<string>> { { errorKey, errorValues } }) { }

    public ErrorResponseModel(string message, Dictionary<string, IEnumerable<string>> errors)
    {
        Message = message;
        Errors = errors;
    }

    /// <summary>
    /// String representing the object's type. Objects of the same type share the same properties.
    /// </summary>
    /// <example>error</example>
    [Required]
    public string Object => "error";

    /// <summary>
    /// A human-readable message providing details about the error.
    /// </summary>
    /// <example>The request model is invalid.</example>
    [Required]
    public string Message { get; set; }

    /// <summary>
    /// If multiple errors occurred, they are listed in dictionary. Errors related to a specific
    /// request parameter will include a dictionary key describing that parameter.
    /// </summary>
    public Dictionary<string, IEnumerable<string>> Errors { get; set; }
}
