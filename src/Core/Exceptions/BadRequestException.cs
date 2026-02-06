using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Core.Exceptions;

#nullable enable

public class BadRequestException : Exception
{
    public BadRequestException() : base()
    { }

    public BadRequestException(string message)
        : base(message)
    { }

    public BadRequestException(string key, string errorMessage)
        : base("The model state is invalid.")
    {
        ModelState = new ModelStateDictionary();
        ModelState.AddModelError(key, errorMessage);
    }

    public BadRequestException(ModelStateDictionary modelState)
        : base("The model state is invalid.")
    {
        if (modelState.IsValid || modelState.ErrorCount == 0)
        {
            return;
        }

        ModelState = modelState;
    }

    public BadRequestException(IEnumerable<IdentityError> identityErrors)
    : base("The model state is invalid.")
    {
        ModelState = new ModelStateDictionary();

        foreach (var error in identityErrors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }
    }

    public ModelStateDictionary? ModelState { get; set; }
}
